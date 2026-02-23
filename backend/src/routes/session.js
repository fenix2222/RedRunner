const express = require('express');
const jwt = require('jsonwebtoken');
const crypto = require('crypto');
const db = require('../db');
const config = require('../config');
const authMiddleware = require('../middleware/auth');
const { verifyBurn } = require('../services/burnVerifier');

const router = express.Router();

// Prepared statements
const findUserByWallet = db.prepare('SELECT * FROM users WHERE wallet_address = ?');
const upsertUser = db.prepare(`
  INSERT INTO users (wallet_address, display_name, name) VALUES (?, ?, ?)
  ON CONFLICT(wallet_address) DO UPDATE SET
    display_name = COALESCE(excluded.display_name, display_name),
    name = COALESCE(excluded.name, name),
    updated_at = CURRENT_TIMESTAMP
  RETURNING *
`);
const createSession = db.prepare(`
  INSERT INTO game_sessions (session_id, user_id, play_type, burn_tx_hash)
  VALUES (?, ?, ?, ?)
`);
const getSession = db.prepare('SELECT * FROM game_sessions WHERE session_id = ?');
const completeSession = db.prepare(`
  UPDATE game_sessions SET status = 'completed', score = ?, completed_at = CURRENT_TIMESTAMP
  WHERE session_id = ? AND status = 'active'
`);
const incrementFreePlays = db.prepare('UPDATE users SET free_plays_used = free_plays_used + 1 WHERE id = ?');
const insertScore = db.prepare("INSERT INTO leaderboard (user_id, score, play_type, session_id) VALUES (?, ?, ?, ?)");
const updateHighScore = db.prepare('UPDATE users SET high_score = MAX(high_score, ?) WHERE id = ?');

function generateSessionId() {
  return 'sess_' + crypto.randomBytes(16).toString('hex');
}

function issueAppJwt(user) {
  return jwt.sign(
    { userId: user.id, wallet: user.wallet_address },
    config.jwtSecret,
    { expiresIn: '7d' }
  );
}

// Shared validation logic
async function validatePlay(walletAddress, displayName, playType, burnTxHash) {
  // Upsert user
  const user = upsertUser.get(walletAddress.toLowerCase(), displayName || null, displayName || null);

  if (playType === 'free') {
    if (user.free_plays_used >= config.maxFreePlays) {
      return { error: 'No free plays remaining', freePlaysRemaining: 0 };
    }
    incrementFreePlays.run(user.id);
  } else if (playType === 'paid') {
    if (!burnTxHash) {
      return { error: 'Burn transaction hash required for paid play' };
    }
    const burnResult = await verifyBurn(burnTxHash, walletAddress);
    if (!burnResult.valid) {
      return { error: burnResult.error };
    }
  } else {
    return { error: 'Invalid playType. Must be "free" or "paid"' };
  }

  const sessionId = generateSessionId();
  createSession.run(sessionId, user.id, playType, burnTxHash || null);

  const freePlaysRemaining = Math.max(0, config.maxFreePlays - (playType === 'free' ? user.free_plays_used + 1 : user.free_plays_used));

  return {
    user,
    sessionId,
    freePlaysRemaining,
    leaderboardEligible: playType === 'paid',
  };
}

// POST /api/session/create-token — called by platform app to create signed platform JWT
// Protected by platform API key
router.post('/create-token', (req, res) => {
  const apiKey = req.headers['x-platform-api-key'];
  if (apiKey !== config.platformApiKey) {
    return res.status(403).json({ error: 'Invalid platform API key' });
  }

  const { walletAddress, displayName, playType, burnTxHash } = req.body;
  if (!walletAddress || !playType) {
    return res.status(400).json({ error: 'walletAddress and playType required' });
  }

  const sessionToken = jwt.sign(
    { walletAddress, displayName, playType, burnTxHash: burnTxHash || null },
    config.platformSecret,
    { expiresIn: '1h' }
  );

  res.json({ sessionToken });
});

// POST /api/session/validate — called by game in platform mode
// Receives platform JWT, validates it, creates game session
router.post('/validate', async (req, res) => {
  const { sessionToken, walletAddress, playType, burnTxHash } = req.body;
  if (!sessionToken) {
    return res.status(400).json({ error: 'sessionToken required' });
  }

  // Verify platform JWT
  let claims;
  try {
    claims = jwt.verify(sessionToken, config.platformSecret);
  } catch (err) {
    return res.status(401).json({ error: 'Invalid or expired session token' });
  }

  // Use claims from JWT (more trusted than body params)
  const result = await validatePlay(
    claims.walletAddress || walletAddress,
    claims.displayName,
    claims.playType || playType,
    claims.burnTxHash || burnTxHash
  );

  if (result.error) {
    return res.status(400).json({ error: result.error, freePlaysRemaining: result.freePlaysRemaining });
  }

  const token = issueAppJwt(result.user);

  res.json({
    token,
    sessionId: result.sessionId,
    freePlaysRemaining: result.freePlaysRemaining,
    leaderboardEligible: result.leaderboardEligible,
  });
});

// POST /api/session/start — called by game in standalone mode
// User already has app JWT from auth/verify
router.post('/start', authMiddleware, async (req, res) => {
  const { playType, burnTxHash } = req.body;
  if (!playType) {
    return res.status(400).json({ error: 'playType required' });
  }

  const user = findUserByWallet.get(req.user.wallet);
  if (!user) {
    return res.status(404).json({ error: 'User not found' });
  }

  const result = await validatePlay(user.wallet_address, user.display_name || user.name, playType, burnTxHash);

  if (result.error) {
    return res.status(400).json({ error: result.error, freePlaysRemaining: result.freePlaysRemaining });
  }

  res.json({
    sessionId: result.sessionId,
    success: true,
    freePlaysRemaining: result.freePlaysRemaining,
    leaderboardEligible: result.leaderboardEligible,
  });
});

// POST /api/session/complete — marks session done, submits score
router.post('/complete', authMiddleware, (req, res) => {
  const { sessionId, score } = req.body;
  if (!sessionId || typeof score !== 'number') {
    return res.status(400).json({ error: 'sessionId and score required' });
  }

  const session = getSession.get(sessionId);
  if (!session) {
    return res.status(404).json({ error: 'Session not found' });
  }
  if (session.status !== 'active') {
    return res.status(400).json({ error: 'Session already completed' });
  }
  if (session.user_id !== req.user.userId) {
    return res.status(403).json({ error: 'Session does not belong to user' });
  }

  // Complete the session
  completeSession.run(score, sessionId);

  // Insert into leaderboard
  insertScore.run(req.user.userId, score, session.play_type, sessionId);

  // Only update high score for paid plays
  const leaderboardEligible = session.play_type === 'paid';
  if (leaderboardEligible) {
    updateHighScore.run(score, req.user.userId);
  }

  res.json({ success: true, leaderboardEligible });
});

module.exports = router;
