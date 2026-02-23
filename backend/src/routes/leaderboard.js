const express = require('express');
const db = require('../db');
const authMiddleware = require('../middleware/auth');

const router = express.Router();

const getTopScores = db.prepare(`
  SELECT u.name, u.wallet_address, l.score, l.created_at
  FROM leaderboard l
  JOIN users u ON l.user_id = u.id
  WHERE l.play_type = 'paid'
  ORDER BY l.score DESC
  LIMIT 100
`);

const insertScore = db.prepare(
  'INSERT INTO leaderboard (user_id, score) VALUES (?, ?)'
);

const updateHighScore = db.prepare(
  'UPDATE users SET high_score = MAX(high_score, ?) WHERE id = ?'
);

// GET /api/leaderboard — public
router.get('/', (req, res) => {
  const entries = getTopScores.all();
  res.json(entries);
});

// POST /api/leaderboard — authenticated
router.post('/', authMiddleware, (req, res) => {
  const { score } = req.body;
  if (typeof score !== 'number' || score < 0) {
    return res.status(400).json({ error: 'Invalid score' });
  }

  insertScore.run(req.user.userId, score);
  updateHighScore.run(score, req.user.userId);

  res.json({ success: true });
});

module.exports = router;
