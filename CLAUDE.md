# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Red Runner is a free, open-source 2D platformer game built with **Unity 6 (version 6000.2.6f2)** in C#. The player runs through procedurally generated terrain, collecting coins and avoiding enemies. The repository also contains an extended blockchain-enabled monorepo in `tempproj/red-runner-monorepo/` (see its own CLAUDE.md for details).

## Build & Run

This is a Unity project — there is no CLI build command. Open the project in **Unity 6000.2.6f2**, then build or play from the editor.

- **Main gameplay scene**: `Assets/Scenes/Play.unity`
- **Level creation scene**: `Assets/Scenes/Creation.unity`
- **Solution file**: `RedRunner.slnx`
- **WebGL export path**: `Exports/export/`

There are no unit tests in the core Unity project. The monorepo backend (`tempproj/red-runner-monorepo/backend/`) has Jest tests runnable via `yarn test`.

## Architecture

### Core Singletons (Manager Pattern)

The game uses singleton managers accessed via static `Singleton` properties:

- **GameManager** (`Assets/Scripts/RedRunner/GameManager.cs`) — Central game state (started/running/ended), score tracking, coin persistence via SaveGame, reset logic. Entry point for game flow.
- **UIManager** (`Assets/Scripts/RedRunner/UIManager.cs`) — Manages screen transitions (Loading, Start, End, Pause, InGame). Handles cursor visibility.
- **AudioManager** (`Assets/Scripts/RedRunner/AudioManager.cs`) — Sound effect playback (coins, jumping, footsteps, water).
- **TerrainGenerator** (`Assets/Scripts/RedRunner/TerrainGeneration/TerrainGenerator.cs`) — Procedurally generates and destroys terrain blocks based on player position. Manages parallax background layers.

### Key Systems

- **Characters** (`Assets/Scripts/RedRunner/Characters/`) — `Character` base class, `RedCharacter` player class with platformer mechanics (run, jump, guard, block, roll), particle effects, and skeleton ragdoll on death.
- **Enemies** (`Assets/Scripts/RedRunner/Enemies/`) — Collision-based enemy types: Eye, Saw, Mace, Spike, Water.
- **Collectables** (`Assets/Scripts/RedRunner/Collectables/`) — Coins and Chests implementing `ICollectable` interface.
- **ObjectPool** (`Assets/Scripts/RedRunner/ObjectPool/`) — Dictionary-based tag system for memory-efficient object reuse.
- **Utilities** (`Assets/Scripts/RedRunner/Utilities/`) — CameraController (smooth follow + shake), GroundCheck, PathFollower.
- **Property System** (`Assets/Scripts/Utils/`) — Custom `Property<T>` and event system with auto-unsubscription.

### Networking & Auth (`Assets/Scripts/RedRunner/Networking/`)

- **Web3AuthManager** — Singleton managing Web3Auth login/logout via .jslib bridge to the Web3Auth JS SDK. Uses `Property<bool> IsAuthenticated` for reactive auth state. In editor mode, auth can be skipped via `m_SkipAuthInEditor` flag.
- **ApiManager** — Singleton REST client (coroutine + UnityWebRequest). Handles backend auth verification, score submission, and leaderboard fetching.
- **ApiConfig** — Static URLs for backend endpoints. `#if UNITY_EDITOR` toggle for dev/prod.
- **AuthData** — Data model for user session (walletAddress, email, name, idToken, appJwtToken).

### WebGL Bridge (`Assets/Plugins/WebGL/Web3AuthBridge.jslib`)

JavaScript bridge using Web3Auth Modal SDK v9 (loaded via CDN in the WebGL template). Exposes `Web3Auth_Init`, `Web3Auth_Login`, `Web3Auth_Logout`, `Web3Auth_CheckSession`. Communicates back to Unity via `SendMessage`. Configured for Stratis/Xertra chain (EVM, chain ID 0x19a91).

### WebGL Template (`Assets/WebGLTemplates/Web3Auth/`)

Custom Unity WebGL template that loads Web3Auth and EthereumProvider SDKs from CDN before Unity initializes. Select this template in Player Settings > WebGL > Resolution and Presentation.

### Namespace Convention

All game scripts use the `RedRunner` namespace hierarchy: `RedRunner.Characters`, `RedRunner.Collectables`, `RedRunner.TerrainGeneration`, `RedRunner.UI`, `RedRunner.Utilities`, `RedRunner.Enemies`, `RedRunner.Networking`.

### Game Flow

`LoadingScreen → LoginScreen (Web3Auth) → StartScreen (shows user info) → InGameScreen → EndScreen (submits score)`

If Web3Auth session exists from a previous visit, LoginScreen is skipped. In Unity Editor with `m_SkipAuthInEditor`, login is bypassed entirely.

### Save System

Uses BayatGames SaveGameFree (`SaveGame.Save`/`SaveGame.Load`) for persisting coins and score locally. Scores are also submitted to the backend leaderboard on death when authenticated.

## Backend (`backend/`)

Lightweight Express.js server with SQLite (better-sqlite3).

```bash
cd backend
npm install
npm run dev    # Dev server with --watch on port 3001
npm start      # Production
```

**Endpoints:**
- `POST /api/auth/verify` — Verify Web3Auth idToken via JWKS, upsert user, return app JWT
- `GET /api/leaderboard` — Top 100 scores (public)
- `POST /api/leaderboard` — Submit score (requires Bearer JWT)
- `GET /api/user/profile` — User profile (requires Bearer JWT)
- `GET /api/health` — Health check

### Dependencies

- Unity Standard Assets (CrossPlatformInput)
- Post-Processing Stack
- 2D packages (Sprite, Tilemap, SpriteShape)
- Web3Auth Modal SDK v9 (loaded via CDN in WebGL builds)
