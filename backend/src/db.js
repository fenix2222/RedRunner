const Database = require('better-sqlite3');
const path = require('path');

const db = new Database(path.join(__dirname, '..', 'redrunner.db'));

db.pragma('journal_mode = WAL');
db.pragma('foreign_keys = ON');

db.exec(`
  CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    wallet_address TEXT UNIQUE NOT NULL,
    email TEXT,
    name TEXT,
    profile_image TEXT,
    high_score INTEGER DEFAULT 0,
    total_coins INTEGER DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
  );

  CREATE TABLE IF NOT EXISTS leaderboard (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    score INTEGER NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id)
  );

  CREATE INDEX IF NOT EXISTS idx_leaderboard_score ON leaderboard(score DESC);
  CREATE INDEX IF NOT EXISTS idx_users_wallet ON users(wallet_address);
`);

// Safe column additions using PRAGMA table_info
function hasColumn(table, column) {
  const cols = db.prepare(`PRAGMA table_info(${table})`).all();
  return cols.some(c => c.name === column);
}

if (!hasColumn('users', 'free_plays_used')) {
  db.exec('ALTER TABLE users ADD COLUMN free_plays_used INTEGER DEFAULT 0');
}
if (!hasColumn('users', 'display_name')) {
  db.exec('ALTER TABLE users ADD COLUMN display_name TEXT');
}
if (!hasColumn('leaderboard', 'play_type')) {
  db.exec("ALTER TABLE leaderboard ADD COLUMN play_type TEXT DEFAULT 'paid'");
}
if (!hasColumn('leaderboard', 'session_id')) {
  db.exec('ALTER TABLE leaderboard ADD COLUMN session_id TEXT');
}

db.exec(`
  CREATE TABLE IF NOT EXISTS burn_transactions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    tx_hash TEXT UNIQUE NOT NULL,
    wallet_address TEXT NOT NULL,
    verified_at DATETIME DEFAULT CURRENT_TIMESTAMP
  );

  CREATE TABLE IF NOT EXISTS game_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT UNIQUE NOT NULL,
    user_id INTEGER NOT NULL,
    play_type TEXT NOT NULL CHECK(play_type IN ('free', 'paid')),
    burn_tx_hash TEXT,
    status TEXT DEFAULT 'active' CHECK(status IN ('active', 'completed', 'expired')),
    score INTEGER,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    completed_at DATETIME,
    FOREIGN KEY (user_id) REFERENCES users(id)
  );

  CREATE INDEX IF NOT EXISTS idx_burn_tx_hash ON burn_transactions(tx_hash);
  CREATE INDEX IF NOT EXISTS idx_sessions_session_id ON game_sessions(session_id);
  CREATE INDEX IF NOT EXISTS idx_sessions_user ON game_sessions(user_id, status);
`);

module.exports = db;
