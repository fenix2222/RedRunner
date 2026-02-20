const express = require('express');
const db = require('../db');
const authMiddleware = require('../middleware/auth');

const router = express.Router();

const getUserById = db.prepare('SELECT * FROM users WHERE id = ?');

// GET /api/user/profile — authenticated
router.get('/profile', authMiddleware, (req, res) => {
  const user = getUserById.get(req.user.userId);
  if (!user) {
    return res.status(404).json({ error: 'User not found' });
  }
  res.json(user);
});

module.exports = router;
