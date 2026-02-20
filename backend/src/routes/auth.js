const express = require('express');
const { createRemoteJWKSet, jwtVerify } = require('jose');
const jwt = require('jsonwebtoken');
const db = require('../db');
const config = require('../config');

const router = express.Router();

const jwks = createRemoteJWKSet(new URL(config.web3auth.jwksUrl));

const upsertUser = db.prepare(`
  INSERT INTO users (wallet_address, email, name, profile_image)
  VALUES (?, ?, ?, ?)
  ON CONFLICT(wallet_address) DO UPDATE SET
    email = COALESCE(excluded.email, users.email),
    name = COALESCE(excluded.name, users.name),
    profile_image = COALESCE(excluded.profile_image, users.profile_image),
    updated_at = CURRENT_TIMESTAMP
`);

const getUserByWallet = db.prepare('SELECT * FROM users WHERE wallet_address = ?');

router.post('/verify', async (req, res) => {
  try {
    const { idToken } = req.body;
    if (!idToken) {
      return res.status(400).json({ error: 'idToken is required' });
    }

    // Verify the Web3Auth idToken using their JWKS
    const { payload } = await jwtVerify(idToken, jwks, {
      algorithms: ['ES256'],
    });

    // Extract wallet address from the token claims
    const walletAddress = payload.wallets && payload.wallets.length > 0
      ? payload.wallets[0].address
      : null;

    if (!walletAddress) {
      return res.status(400).json({ error: 'No wallet address found in token' });
    }

    const email = payload.email || null;
    const name = payload.name || null;
    const profileImage = payload.profileImage || null;

    // Upsert user
    upsertUser.run(walletAddress, email, name, profileImage);
    const user = getUserByWallet.get(walletAddress);

    // Issue our own JWT
    const appToken = jwt.sign(
      { userId: user.id, wallet: walletAddress },
      config.jwtSecret,
      { expiresIn: '7d' }
    );

    res.json({ user, token: appToken });
  } catch (err) {
    console.error('Auth verify error:', err.message);
    res.status(401).json({ error: 'Invalid token: ' + err.message });
  }
});

module.exports = router;
