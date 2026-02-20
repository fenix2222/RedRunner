# Web3Auth Login Integration for Red Runner

## Context

Red Runner is a Unity 6 WebGL 2D platformer with zero auth/networking. We're adding Web3Auth login so users can sign in via social logins (Google, Apple, etc.), email/phone, or blockchain wallets — all through Web3Auth's built-in modal UI. The target chain is **Stratis/Xertra** (EVM-based). A new lightweight backend handles user persistence and leaderboards.

**Key architecture decision:** Since the target is **WebGL only** and Web3Auth's Unity SDK lacks wallet adapter support, we use the **Web3Auth Web (JavaScript) SDK** via a `.jslib` bridge. This gives full modal functionality (social + email + wallets) natively in the browser. The .jslib pattern is proven in tempproj (`game/Assets/Plugin/google_sign_zabi.jslib`).

## Configuration (from STRAXDashboard project)

**Web3Auth:**
- **Client ID:** `BMJIf8AYFltty8i-VNd6sqXeHEOQNeyP8QBxFrGZfVN3jtTT-3zUSrOn4Jvv59QxzRQ-3zl8JBsMnSr0Z4vMp84`
- **Network:** `SAPPHIRE_DEVNET` (switch to `SAPPHIRE_MAINNET` for production)
- **App Name:** "Xertra Passport"
- **Theme:** Dark mode, primary color `#38023b`
- **Logos:** `https://stratispherestaging.blob.core.windows.net/images/Xertra_Logo_White_Transparent.png`
- **Allowed Origin (dev):** `http://localhost:8080`

**Stratis Mainnet (Xertra):**
- **Chain ID:** `105105` (hex: `0x19a91`)
- **Currency:** STRAX (18 decimals)
- **RPC URL:** `https://rpc.xertra.com`
- **Block Explorer:** `https://explorer.xertra.com`

**Auroria Testnet:**
- **Chain ID:** `205205` (hex: `0x321cd`)
- **Currency:** tSTRAX (18 decimals)
- **RPC URL:** `https://auroria.rpc.xertra.com`
- **Block Explorer:** `https://auroria.explorer.xertra.com`

**Reference file:** `tempproj/STRAXDashboard/src/config/web3auth.ts`

---

## Phase 1: Unity Infrastructure (Auth Manager + API Client)

### Step 1 — Create `AuthData.cs` model
**New file:** `Assets/Scripts/RedRunner/Networking/AuthData.cs`
- Serializable data class: `walletAddress`, `email`, `name`, `profileImage`, `idToken` (Web3Auth), `appJwtToken` (our backend JWT)

### Step 2 — Create `ApiConfig.cs`
**New file:** `Assets/Scripts/RedRunner/Networking/ApiConfig.cs`
- Static config class with backend URLs (auth/verify, leaderboard, user profile)
- `#if UNITY_EDITOR` toggle for localhost vs production URL

### Step 3 — Create `Web3AuthManager.cs` singleton
**New file:** `Assets/Scripts/RedRunner/Networking/Web3AuthManager.cs`
- Singleton following GameManager/UIManager pattern
- `[DllImport("__Internal")]` declarations for .jslib: `Web3Auth_Init`, `Web3Auth_Login`, `Web3Auth_Logout`, `Web3Auth_CheckSession`
- `#if UNITY_WEBGL && !UNITY_EDITOR` guards + `[SerializeField] bool m_SkipAuthInEditor` for editor testing
- Uses project's `Property<bool> IsAuthenticated` and `Property<AuthData> CurrentUser` (reuse existing reactive system from `Assets/Scripts/Utils/Property.cs`)
- Callback methods for JS→Unity: `OnLoginSuccess(string json)`, `OnLoginFailed(string error)`, `OnLogoutComplete(string)`, `OnInitComplete(string)`, `OnSessionFound(string json)`, `OnNoSession(string)`
- On login success: calls `ApiManager.VerifyAuth()` to exchange Web3Auth idToken for app JWT

### Step 4 — Create `ApiManager.cs` singleton
**New file:** `Assets/Scripts/RedRunner/Networking/ApiManager.cs`
- Singleton, coroutine-based REST client (same pattern as tempproj's `API_Manager.cs`)
- `VerifyAuth(AuthData, callback)` — POST idToken to backend, receive app JWT
- `SubmitScore(int score, callback)` — POST score with Bearer JWT
- `GetLeaderboard(callback)` — GET leaderboard entries
- All requests use `UnityWebRequest` + JSON serialization

---

## Phase 2: Web3Auth JavaScript Bridge

### Step 5 — Create WebGL template
**New file:** `Assets/WebGLTemplates/Web3Auth/index.html`
- Standard Unity WebGL template with `<script>` tag loading Web3Auth Modal SDK from CDN
- Captures `unityInstance` from `createUnityInstance()` on `window.unityInstance` for SendMessage
- Dispatches `UnityReady` event after Unity loads

### Step 6 — Create .jslib bridge
**New file:** `Assets/Plugins/WebGL/Web3AuthBridge.jslib`
- `Web3Auth_Init(clientId, chainId, rpcTarget, gameObjectName)` — initializes Web3Auth Modal SDK with Xertra chain config, stores unity callback target name
- `Web3Auth_Login()` — calls `web3auth.connect()`, opens modal, on success extracts wallet address + userInfo + idToken, sends JSON to Unity via `SendMessage(gameObjectName, 'OnLoginSuccess', json)`
- `Web3Auth_Logout()` — calls `web3auth.logout()`, notifies Unity
- `Web3Auth_CheckSession()` — checks if Web3Auth has an active session (localStorage persistence), auto-notifies Unity with `OnSessionFound` or `OnNoSession`

---

## Phase 3: UI Changes

### Step 7 — Add `LOGIN_SCREEN` to UIScreenInfo enum
**Modify:** `Assets/Scripts/RedRunner/UIManager.cs`
- Add `LOGIN_SCREEN` entry to the `UIScreenInfo` enum (between LOADING_SCREEN and START_SCREEN)

### Step 8 — Create `LoginScreen.cs`
**New file:** `Assets/Scripts/RedRunner/UI/UIScreen/LoginScreen.cs`
- Extends `UIScreen` base class (same as StartScreen, EndScreen, etc.)
- Has a Login button that calls `Web3AuthManager.Singleton.Login()`
- Status text for "Connecting..." / error messages
- Subscribes to `Web3AuthManager.OnAuthStateChanged` — on success, transitions to StartScreen

### Step 9 — Modify `StartScreen.cs` to show user info
**Modify:** `Assets/Scripts/RedRunner/UI/UIScreen/StartScreen.cs`
- Add serialized fields: `WalletAddressText`, `UsernameText`, `LogoutButton`
- Override `UpdateScreenStatus(bool open)` to populate user info from `Web3AuthManager.CurrentUser` when screen opens
- Wire LogoutButton to call `Web3AuthManager.Logout()` and navigate to LoginScreen
- Existing PlayButton/ExitButton logic unchanged

### Step 10 — Modify `GameManager.cs` game flow
**Modify:** `Assets/Scripts/RedRunner/GameManager.cs`
- Change `Load()` coroutine: after 3s wait, check `Web3AuthManager.IsAuthenticated` → if true go to StartScreen, else go to LoginScreen
- Add public getters for `m_LastScore` and `m_HighScore` (needed by EndScreen/ApiManager)
- In `DeathCrt()`: after score calculation, call `ApiManager.SubmitScore()` if authenticated (fire-and-forget, don't block EndScreen display)

### Step 11 — Modify `EndScreen.cs` for score display
**Modify:** `Assets/Scripts/RedRunner/UI/UIScreen/EndScreen.cs`
- Score submission happens in GameManager.DeathCrt() (Step 10), so EndScreen just needs minimal changes
- Optionally show "Score submitted!" confirmation text

---

## Phase 4: Lightweight Backend

### Step 12 — Create Express.js backend
**New directory:** `backend/` at repo root

**Structure:**
```
backend/
  package.json           # express, better-sqlite3, jsonwebtoken, jose, cors, dotenv
  .env.example           # JWT_SECRET, PORT
  src/
    index.js             # Express app, CORS, routes
    config.js            # env vars
    db.js                # SQLite setup + schema init
    middleware/
      auth.js            # JWT Bearer verification middleware
    routes/
      auth.js            # POST /api/auth/verify (verify Web3Auth idToken via JWKS, upsert user, issue app JWT)
      leaderboard.js     # GET /api/leaderboard, POST /api/leaderboard (authenticated)
      user.js            # GET /api/user/profile (authenticated)
```

**Key endpoints:**
- `POST /api/auth/verify` — receives `{ idToken }`, verifies against Web3Auth JWKS (`https://api-auth.web3auth.io/jwks`, ES256), extracts wallet + user info, upserts user in SQLite, returns `{ user, token }` (app JWT, 7-day expiry)
- `GET /api/leaderboard` — top 100 scores, public
- `POST /api/leaderboard` — submit score, requires Bearer JWT
- `GET /api/user/profile` — user data, requires Bearer JWT

**Database:** SQLite via `better-sqlite3` (zero-config, no separate DB server). Tables: `users` (wallet_address unique, email, name, high_score, total_coins), `leaderboard` (user_id FK, score, created_at)

---

## Phase 5: Scene Setup (Manual in Unity Editor)

### Step 13 — Unity scene configuration
- Create LoginScreen UI in Play.unity scene (Canvas with Animator + CanvasGroup, matching existing screen pattern)
- Create animation controller with "Open" bool parameter
- Add LoginScreen component, wire button references in Inspector
- Add LoginScreen to UIManager's `m_Screens` list
- Add `Web3AuthManager` and `ApiManager` GameObjects to scene
- Configure Web3Auth client ID and Xertra chain config in Web3AuthManager inspector fields
- Set WebGL template to "Web3Auth" in Player Settings → WebGL → Resolution and Presentation
- Add wallet address / username Text objects + Logout Button to StartScreen, wire in Inspector

---

## Implementation Order

Steps 1-4 (Unity infra) and Step 12 (backend) can be built **in parallel**.
Steps 5-6 (JS bridge) depend on understanding Step 3's callback API.
Steps 7-11 (UI changes) depend on Steps 1-6 being complete.
Step 13 (scene setup) is manual Editor work after all scripts exist.

```
Parallel track A:  [1] → [2] → [3] → [4] → [5] → [6] → [7-11] → [13]
Parallel track B:  [12 backend] ─────────────────────────────────────┘
```

---

## Verification

1. **Backend standalone:** Run `cd backend && npm install && npm start`, test with curl:
   - `curl -X POST localhost:3001/api/auth/verify -H "Content-Type: application/json" -d '{"idToken":"..."}'`
   - `curl localhost:3001/api/leaderboard`
2. **Unity WebGL build:** Build with Web3Auth template, serve locally, verify:
   - Web3Auth modal opens on Login button click
   - Social login completes and returns to game
   - StartScreen shows wallet address and username
   - Playing and dying submits score to backend
   - Page refresh auto-restores session (skips LoginScreen)
   - Logout returns to LoginScreen
3. **Editor testing:** With `m_SkipAuthInEditor = true`, game flow bypasses login and works as before

---

## Files Summary

**New files (8):**
- `Assets/Scripts/RedRunner/Networking/AuthData.cs`
- `Assets/Scripts/RedRunner/Networking/ApiConfig.cs`
- `Assets/Scripts/RedRunner/Networking/Web3AuthManager.cs`
- `Assets/Scripts/RedRunner/Networking/ApiManager.cs`
- `Assets/Scripts/RedRunner/UI/UIScreen/LoginScreen.cs`
- `Assets/Plugins/WebGL/Web3AuthBridge.jslib`
- `Assets/WebGLTemplates/Web3Auth/index.html`
- `backend/` (entire directory)

**Modified files (4):**
- `Assets/Scripts/RedRunner/UIManager.cs` — add LOGIN_SCREEN to enum
- `Assets/Scripts/RedRunner/GameManager.cs` — load flow, score submission, public getters
- `Assets/Scripts/RedRunner/UI/UIScreen/StartScreen.cs` — user info display, logout button
- `Assets/Scripts/RedRunner/UI/UIScreen/EndScreen.cs` — minor score submission feedback

