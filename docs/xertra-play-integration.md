# Xertra Play Platform Integration — Dual-Mode Auth for Red Runner

## Context

Red Runner currently has Web3Auth login built directly into the game. It now needs to **also** support being embedded as an iframe inside the **Xertra Play** gaming portal, where auth happens at the platform level and a session is injected into the game via `postMessage`.

**Key requirements from the Xertra Play brief:**
- 3 free plays per account (no leaderboard), then 1 STRAX burn per paid play (leaderboard eligible)
- Platform handles Web3Auth login and STRAX burn transaction
- Game receives session data (no double login)
- Game backend verifies the burn tx on-chain as defense in depth

**User decisions:**
- **Keep both modes**: standalone Web3Auth + platform-embedded session injection
- **Platform doesn't exist yet**: we define the session contract using signed JWTs
- **Burn verification**: platform burns, game backend double-checks on Xertra RPC

---

## Architecture

```
MODE A — Standalone (existing):
  User → Game (Web3Auth modal) → Backend (verify idToken) → JWT → play

MODE B — Platform (new):
  User → Xertra Play (Web3Auth) → burn tx → postMessage(INIT_SESSION) → Game
    → Backend (validate platform JWT + verify burn tx) → JWT → play
```

**Dual-mode detection:** On WebGL startup, a JS `postMessage` listener waits for `INIT_SESSION` from parent frame. If received within 3 seconds → Platform Mode. If timeout → Standalone Mode (existing Web3Auth flow). In Editor → bypass mode.

**Key design decision:** Platform mode injects auth into the existing `Web3AuthManager.SetExternalSession()` so all downstream code (`ApiManager.GetJwt()`, `GameManager.DeathCrt()`, `StartScreen.UpdateUserInfo()`) works unchanged.

---

## Platform Session Contract

### Parent → Game (`postMessage`):
```json
{
  "type": "INIT_SESSION",
  "sessionToken": "<JWT signed with shared secret>",
  "walletAddress": "0x...",
  "displayName": "username",
  "playType": "free|paid",
  "burnTxHash": "0x..."
}
```

### Game → Parent (`postMessage`):
```json
{ "type": "GAME_READY" }
{ "type": "GAME_STARTED", "playType": "free|paid" }
{ "type": "GAME_ENDED", "score": 1234, "leaderboardEligible": true }
{ "type": "SESSION_ERROR", "message": "..." }
```

---

## Phase 1: Backend Changes

### Step 1 — Database migration
**Modify:** `backend/src/db.js`
- Add `free_plays_used INTEGER DEFAULT 0` to `users` table
- Add `play_type TEXT DEFAULT 'paid'` to `leaderboard` table
- New table `burn_transactions` (tx_hash UNIQUE, wallet_address, verified_at) — prevents replay
- New table `game_sessions` (session_id UNIQUE, user_id FK, play_type, burn_tx_hash, status, score, created_at, completed_at)
- Use `PRAGMA table_info` to safely migrate existing schema

### Step 2 — Burn verifier service
**New file:** `backend/src/services/burnVerifier.js`
- `verifyBurn(txHash, expectedSender)` → calls Xertra RPC (`eth_getTransactionByHash` + `eth_getTransactionReceipt`)
- Validates: `to === 0x000...000`, `value >= 1e18 wei`, `from === expectedSender`, `status === 0x1`
- Checks `burn_transactions` table for replay, inserts on success
- Uses `fetch` for JSON-RPC calls to `https://rpc.xertra.com`

### Step 3 — Backend config additions
**Modify:** `backend/src/config.js`
- Add: `platformSecret`, `xertraRpcUrl`, `burnAddress`, `burnAmount` (1e18), `maxFreePlays` (3)

### Step 4 — Session routes
**New file:** `backend/src/routes/session.js`

| Endpoint | Auth | Body | Returns |
|---|---|---|---|
| `POST /api/session/validate` | None (platform JWT in body) | `{ sessionToken, walletAddress, playType, burnTxHash? }` | `{ token, sessionId, freePlaysRemaining, leaderboardEligible }` |
| `POST /api/session/start` | Bearer JWT | `{ playType, burnTxHash? }` | `{ sessionId, success, freePlaysRemaining }` |
| `POST /api/session/complete` | Bearer JWT | `{ sessionId, score }` | `{ success, leaderboardEligible }` |

- `/validate`: verifies platform JWT with shared secret, if paid → verify burn, if free → check `free_plays_used < 3`, upserts user, creates game_sessions record, issues app JWT
- `/start`: for standalone mode — same validation but user already has app JWT
- `/complete`: marks session complete, inserts into leaderboard (with play_type), updates high_score only for paid

### Step 5 — Leaderboard play_type filtering
**Modify:** `backend/src/routes/leaderboard.js`
- GET: filter to `play_type = 'paid'` by default
- POST: accept `sessionId`, look up session, derive `play_type` from session record

### Step 6 — Register session routes
**Modify:** `backend/src/index.js`
- Add `app.use('/api/session', sessionRoutes)`

---

## Phase 2: Unity Data Layer

### Step 7 — Session data models
**New file:** `Assets/Scripts/RedRunner/Networking/PlatformSessionData.cs`
- `PlatformSessionData` — maps the `INIT_SESSION` JSON
- `PlatformSessionValidateRequest/Response` — for backend calls
- `GameStartRequest/Response` — for standalone game start

### Step 8 — API config additions
**Modify:** `Assets/Scripts/RedRunner/Networking/ApiConfig.cs`
- Add: `SessionValidate`, `SessionStart`, `SessionComplete` endpoint URLs

### Step 9 — ApiManager session methods
**Modify:** `Assets/Scripts/RedRunner/Networking/ApiManager.cs`
- Add: `ValidatePlatformSession()`, `StartGameSession()`, `CompleteGameSession()` coroutines
- Existing `SubmitScore()` and `VerifyAuth()` kept for standalone mode

---

## Phase 3: JavaScript Bridge

### Step 10 — Platform bridge jslib
**New file:** `Assets/Plugins/WebGL/PlatformBridge.jslib`
- `PlatformBridge_StartListening(gameObjectName)` — adds `window.addEventListener('message')`, checks for buffered `_pendingPlatformSession`, sends `GAME_READY` to parent
- `PlatformBridge_SendToParent(json)` — `window.parent.postMessage()`

### Step 11 — Early postMessage buffer in template
**Modify:** `Assets/WebGLTemplates/Web3Auth/index.html`
- Add script before Unity loads that captures `INIT_SESSION` into `window._pendingPlatformSession` so it's not lost while Unity boots

---

## Phase 4: Core Dual-Mode Logic

### Step 12 — SessionManager singleton
**New file:** `Assets/Scripts/RedRunner/Networking/SessionManager.cs`
- `AuthMode` enum: `Standalone, Platform, Editor`
- `PlayType` enum: `Free, Paid`
- On `Start()`: call `PlatformBridge_StartListening`, start 3-second timeout coroutine
- If `OnPlatformSessionReceived` fires → Platform Mode: parse JSON, call `ApiManager.ValidatePlatformSession()`, on success call `Web3AuthManager.SetExternalSession()`
- If timeout → Standalone Mode: call `Web3AuthManager.InitializeStandalone()`
- In Editor → Editor Mode (existing bypass)
- Tracks: `CurrentMode`, `CurrentPlayType`, `SessionId`, `BurnTxHash`, `IsLeaderboardEligible`
- `NotifyParent(type, payload)` — sends postMessage to parent iframe

### Step 13 — Web3AuthManager refactor
**Modify:** `Assets/Scripts/RedRunner/Networking/Web3AuthManager.cs`
- Add `SetExternalSession(AuthData data)` — sets m_CurrentUser, IsAuthenticated, fires OnAuthStateChanged
- Extract Web3Auth init from `Start()` into `InitializeStandalone()` — called by SessionManager after timeout
- `Start()` only does editor bypass; WebGL init deferred to SessionManager

---

## Phase 5: Game Flow Integration

### Step 14 — GameManager session-aware flow
**Modify:** `Assets/Scripts/RedRunner/GameManager.cs`
- `Load()`: wait for `SessionManager.IsSessionReady` instead of checking auth directly
- `StartGame()`: in standalone mode, call `ApiManager.StartGameSession()` first; in platform mode, session already exists. Notify parent with `GAME_STARTED`.
- `DeathCrt()`: call `ApiManager.CompleteGameSession(sessionId, score)` instead of `SubmitScore()`. Backend handles leaderboard insertion. Notify parent with `GAME_ENDED`.

### Step 15 — StartScreen platform-aware UI
**Modify:** `Assets/Scripts/RedRunner/UI/UIScreen/StartScreen.cs`
- In Platform Mode: hide LogoutButton (platform manages sessions), show displayName from session
- Show free plays remaining indicator if `playType == free`
- Display "Practice Mode — not on leaderboard" badge for free plays

### Step 16 — EndScreen leaderboard eligibility
**Modify:** `Assets/Scripts/RedRunner/UI/UIScreen/EndScreen.cs`
- Show "Score submitted to leaderboard!" for paid plays
- Show "Free play — not on leaderboard" for free plays

### Step 17 — LoginScreen platform bypass
**Modify:** `Assets/Scripts/RedRunner/UI/UIScreen/LoginScreen.cs`
- In Platform Mode: never shown (session comes from parent). If somehow shown, display "Returning to Xertra Play..." and notify parent with `SESSION_ERROR`.

---

## Phase 6: Test Platform App (Xertra Play Placeholder)

### Step 18 — Test platform web app
**New directory:** `platform-test/`

A simple single-page web app that acts as a placeholder for the real Xertra Play portal. Allows end-to-end testing of the platform session injection flow.

**Structure:**
```
platform-test/
  index.html          # Main portal page
  style.css           # Xertra-themed dark styling
  app.js              # Web3Auth login + game launcher + postMessage bridge
  package.json        # just for serve script
```

**Features:**
- **Web3Auth login** using the same client ID and Xertra chain config
- After login, shows a **dashboard** with:
  - Wallet address (shortened) + STRAX balance (read from Xertra RPC)
  - Display name (prompt on first login, stored in localStorage)
  - Free plays remaining (fetched from game backend `GET /api/user/profile`)
  - Game card for "Red Runner" with Play button
- **Play button flow:**
  1. If free plays remaining > 0 → offer "Play Free (Practice)" or "Play for 1 STRAX"
  2. If free plays exhausted → only "Play for 1 STRAX" option
  3. For paid play: initiate burn tx (send 1 STRAX to 0x000...000 via Web3Auth provider), wait for confirmation
  4. Open game in an **iframe** on the same page
  5. Send `INIT_SESSION` postMessage to iframe with: signed platform JWT, walletAddress, displayName, playType, burnTxHash
- **postMessage listener** for game events:
  - `GAME_READY` → send session
  - `GAME_ENDED` → show score, offer "Play Again", remove iframe
  - `SESSION_ERROR` → show error, offer re-login
- **Platform JWT signing:** Uses the same `PLATFORM_SECRET` as the game backend. The test app calls a backend endpoint (`POST /api/session/create-token`) that signs the JWT server-side (the secret never goes to the browser).
- **Logout button** → Web3Auth logout, clear localStorage

### Step 19 — Backend platform token endpoint
**Modify:** `backend/src/routes/session.js`
- Add `POST /api/session/create-token` — called by the test platform app
  - Receives: `{ walletAddress, displayName, playType, burnTxHash? }` with a special platform API key header
  - Signs and returns a platform JWT containing all session data
  - This endpoint simulates what the real Xertra Play backend would do

---

## Phase 7: Scene Setup

### Step 20 — Add SessionManager to scene
**Modify:** `Assets/Editor/Web3AuthSceneSetup.cs`
- Add `SessionManager` GameObject creation alongside existing Web3AuthManager/ApiManager

---

## Implementation Order

```
Phase 1 (Backend):     [1-6, 19] — independent, no Unity changes needed
Phase 2-3 (Unity+JS):  [7-11] — parallel with Phase 1
Phase 4 (Core logic):  [12-13] — depends on Phases 2-3
Phase 5 (Game flow):   [14-17] — depends on Phase 4
Phase 6 (Test app):    [18] — depends on Phase 1 (backend)
Phase 7 (Scene):       [20] — after all scripts exist
```

---

## Verification

1. **Backend**: `cd backend && npm start`, test new endpoints with curl:
   - `POST /api/session/create-token` → get platform JWT
   - `POST /api/session/validate` with that JWT
   - `POST /api/session/start` with playType=free (should work 3 times, fail on 4th)
   - `POST /api/session/complete` with score
   - `GET /api/leaderboard` should only show paid play scores
2. **Test platform app**: `cd platform-test && npx serve -l 8081`
   - Login with Web3Auth → see dashboard with wallet + balance
   - Set display name on first login
   - Click "Play Free" → game loads in iframe, no login prompt, shows practice mode badge
   - Play and die → score shown but not on leaderboard
   - After 3 free plays → only "Play for 1 STRAX" available
   - Click "Play for 1 STRAX" → burn tx confirmation → game loads with paid session
   - Play and die → score on leaderboard
   - `GAME_ENDED` event received by parent, shows score overlay
3. **Standalone mode**: Serve game directly (`npx http-server -c-1 Web/`) — existing Web3Auth flow works, no platform session
4. **Editor**: Game bypasses both modes with test data, plays normally
