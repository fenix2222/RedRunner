const express = require('express');
const cors = require('cors');
const config = require('./config');

// Initialize database (creates tables on first run)
require('./db');

const authRoutes = require('./routes/auth');
const leaderboardRoutes = require('./routes/leaderboard');
const userRoutes = require('./routes/user');
const sessionRoutes = require('./routes/session');

const app = express();

app.use(cors());
app.use(express.json());

// Routes
app.use('/api/auth', authRoutes);
app.use('/api/leaderboard', leaderboardRoutes);
app.use('/api/user', userRoutes);
app.use('/api/session', sessionRoutes);

// Health check
app.get('/api/health', (req, res) => {
  res.json({ status: 'ok' });
});

app.listen(config.port, () => {
  console.log(`Red Runner backend running on port ${config.port}`);
});
