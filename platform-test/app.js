/**
 * Xertra Play — Test Platform App
 *
 * Simulates the Xertra Play gaming portal for testing platform session
 * injection into Red Runner (iframe-embedded game).
 *
 * Flow:
 *   1. User logs in via Web3Auth (same config as game)
 *   2. Dashboard shows wallet, balance, free plays
 *   3. User clicks Play Free or Play for 1 STRAX
 *   4. For paid: sends burn tx, waits for confirmation
 *   5. Opens game in iframe, sends INIT_SESSION via postMessage
 *   6. Listens for GAME_ENDED from game, shows score overlay
 */

// ---- Configuration ----
const CONFIG = {
    web3authClientId: 'BMJIf8AYFltty8i-VNd6sqXeHEOQNeyP8QBxFrGZfVN3jtTT-3zUSrOn4Jvv59QxzRQ-3zl8JBsMnSr0Z4vMp84',
    chainId: '0x19a91',
    rpcTarget: 'https://rpc.xertra.com',
    blockExplorerUrl: 'https://explorer.xertra.com',
    chainDisplayName: 'Stratis Mainnet',
    tickerName: 'STRAX',
    ticker: 'STRAX',
    burnAddress: '0x0000000000000000000000000000000000000000',
    burnAmountWei: '0xDE0B6B3A7640000', // 1 STRAX = 1e18 wei in hex
    backendUrl: 'http://localhost:8081',
    // Path to the built game (relative or absolute URL)
    // Change this to match your game build output location
    gameUrl: 'http://localhost:8083/index.html?v=' + Date.now(),
    platformApiKey: 'dev-platform-api-key',
};

// ---- State ----
let web3auth = null;
let provider = null;
let currentWallet = null;
let displayName = null;
let freePlaysRemaining = 3;
let currentPlayType = null;
let currentBurnTxHash = null;

// ---- Web3Auth Init ----
async function initWeb3Auth() {
    try {
        const chainConfig = {
            chainNamespace: 'eip155',
            chainId: CONFIG.chainId,
            rpcTarget: CONFIG.rpcTarget,
            blockExplorerUrl: CONFIG.blockExplorerUrl,
            displayName: CONFIG.chainDisplayName,
            tickerName: CONFIG.tickerName,
            ticker: CONFIG.ticker,
            decimals: 18,
        };

        const privateKeyProvider = new EthereumProvider.EthereumPrivateKeyProvider({
            config: { chainConfig: chainConfig }
        });

        web3auth = new Modal.Web3Auth({
            clientId: CONFIG.web3authClientId,
            web3AuthNetwork: 'sapphire_devnet',
            chainConfig: chainConfig,
            privateKeyProvider: privateKeyProvider,
            uiConfig: {
                appName: 'Xertra Play',
                theme: { primary: '#38023b' },
                mode: 'dark',
                logoDark: 'https://stratispherestaging.blob.core.windows.net/images/Xertra_Logo_White_Transparent.png',
                logoLight: 'https://stratispherestaging.blob.core.windows.net/images/Xertra_Logo_Transparent.png',
                defaultLanguage: 'en',
                loginGridCol: 3,
                primaryButton: 'externalLogin',
            },
        });

        // Add external wallet adapters (match game's pattern)
        if (typeof DefaultEvmAdapter !== 'undefined' && DefaultEvmAdapter.getDefaultExternalAdapters) {
            try {
                const adapters = DefaultEvmAdapter.getDefaultExternalAdapters({ options: web3auth.options });
                adapters.forEach(adapter => web3auth.configureAdapter(adapter));
                console.log('[Platform] Configured ' + adapters.length + ' external wallet adapters');
            } catch (adapterErr) {
                console.warn('[Platform] Failed to configure external adapters:', adapterErr);
            }
        }

        await web3auth.initModal();
        console.log('[Platform] Web3Auth initialized');

        // Check existing session
        if (web3auth.connected && web3auth.provider) {
            provider = web3auth.provider;
            await onLoginSuccess();
        }
    } catch (err) {
        console.error('[Platform] Web3Auth init error:', err);
        setLoginStatus('Init error: ' + err.message, 'error');
    }
}

// ---- Login / Logout ----
async function login() {
    try {
        setLoginStatus('Connecting...', 'info');
        provider = await web3auth.connect();
        await onLoginSuccess();
    } catch (err) {
        console.error('[Platform] Login error:', err);
        setLoginStatus('Login failed: ' + err.message, 'error');
    }
}

async function logout() {
    try {
        await web3auth.logout();
    } catch (e) {
        console.warn('[Platform] Logout error:', e);
    }
    provider = null;
    currentWallet = null;
    displayName = null;
    showLoginView();
}

async function onLoginSuccess() {
    try {
        // Get wallet address
        const accounts = await provider.request({ method: 'eth_accounts' });
        currentWallet = accounts[0];
        console.log('[Platform] Wallet:', currentWallet);

        // Check display name
        displayName = localStorage.getItem('xertraPlay_displayName');
        if (!displayName) {
            showNameModal();
            return;
        }

        await showDashboard();
    } catch (err) {
        console.error('[Platform] onLoginSuccess error:', err);
        setLoginStatus('Error: ' + err.message, 'error');
    }
}

function showNameModal() {
    document.getElementById('nameModal').style.display = 'flex';
}

function saveName() {
    const name = document.getElementById('nameInput').value.trim();
    if (!name) return;
    displayName = name;
    localStorage.setItem('xertraPlay_displayName', name);
    document.getElementById('nameModal').style.display = 'none';
    showDashboard();
}

// ---- Views ----
function showLoginView() {
    document.getElementById('loginView').classList.remove('hidden');
    document.getElementById('dashboardView').classList.add('hidden');
    document.getElementById('headerRight').classList.add('hidden');
}

async function showDashboard() {
    document.getElementById('loginView').classList.add('hidden');
    document.getElementById('dashboardView').classList.remove('hidden');
    document.getElementById('headerRight').classList.remove('hidden');

    // Header
    const short = shortenAddress(currentWallet);
    document.getElementById('headerWallet').textContent = displayName + ' (' + short + ')';
    document.getElementById('statWallet').textContent = short;

    // Fetch balance
    try {
        const balHex = await provider.request({
            method: 'eth_getBalance',
            params: [currentWallet, 'latest'],
        });
        const balWei = BigInt(balHex);
        const balStrax = Number(balWei) / 1e18;
        document.getElementById('statBalance').textContent = balStrax.toFixed(4) + ' STRAX';
    } catch (e) {
        document.getElementById('statBalance').textContent = 'Error';
        console.warn('[Platform] Balance fetch error:', e);
    }

    // Fetch free plays from backend
    await fetchFreePlays();
    updatePlayButtons();
}

async function fetchFreePlays() {
    try {
        const res = await fetch(CONFIG.backendUrl + '/api/session/validate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                sessionToken: 'check-only',
                walletAddress: currentWallet,
                playType: 'free',
            }),
        });
        // This will fail validation but we can try a different approach
        // Use the create-token + validate flow to check free plays
        // For now, just use localStorage as a rough tracker
    } catch (e) {
        console.warn('[Platform] Could not fetch free plays:', e);
    }
    // Use local tracking (backend will enforce the real limit)
    const used = parseInt(localStorage.getItem('xertraPlay_freePlaysUsed_' + currentWallet) || '0');
    freePlaysRemaining = Math.max(0, CONFIG.maxFreePlays - used);
    document.getElementById('statFreePlays').textContent = freePlaysRemaining + ' / ' + CONFIG.maxFreePlays;
}

// Not in CONFIG — add it
CONFIG.maxFreePlays = 3;

function updatePlayButtons() {
    const freeBtn = document.getElementById('freePlayBtn');
    const paidBtn = document.getElementById('paidPlayBtn');

    if (freePlaysRemaining <= 0) {
        freeBtn.disabled = true;
        freeBtn.textContent = 'No free plays left';
    } else {
        freeBtn.disabled = false;
        freeBtn.textContent = 'Play Free (Practice) — ' + freePlaysRemaining + ' left';
    }
}

function setLoginStatus(msg, type) {
    const el = document.getElementById('loginStatus');
    el.textContent = msg;
    el.className = 'tx-status';
    if (type) el.style.color = type === 'error' ? '#ff8a80' : type === 'info' ? '#8888ff' : '#69f0ae';
}

function setGameStatus(msg, type) {
    const el = document.getElementById('gameStatus');
    el.textContent = msg;
    el.className = 'tx-status';
    if (type) el.style.color = type === 'error' ? '#ff8a80' : type === 'info' ? '#8888ff' : '#69f0ae';
}

function addStatusMsg(msg, type) {
    const area = document.getElementById('statusArea');
    const div = document.createElement('div');
    div.className = 'status-msg ' + (type || 'info');
    div.textContent = msg;
    area.prepend(div);
    // Remove after 10s
    setTimeout(() => div.remove(), 10000);
}

// ---- Game Flow ----
async function startGame(playType) {
    currentPlayType = playType;
    currentBurnTxHash = null;

    if (playType === 'paid') {
        // Send burn transaction
        setGameStatus('Sending burn transaction (1 STRAX)...', 'info');
        try {
            const txHash = await provider.request({
                method: 'eth_sendTransaction',
                params: [{
                    from: currentWallet,
                    to: CONFIG.burnAddress,
                    value: CONFIG.burnAmountWei,
                    gas: '0x5208', // 21000
                }],
            });
            currentBurnTxHash = txHash;
            setGameStatus('Burn tx sent: ' + txHash.substring(0, 16) + '... Waiting for confirmation...', 'info');

            // Wait for receipt
            let receipt = null;
            for (let i = 0; i < 30; i++) {
                await new Promise(r => setTimeout(r, 2000));
                receipt = await provider.request({
                    method: 'eth_getTransactionReceipt',
                    params: [txHash],
                });
                if (receipt) break;
            }

            if (!receipt || receipt.status !== '0x1') {
                setGameStatus('Burn transaction failed or timed out', 'error');
                return;
            }

            setGameStatus('Burn confirmed! Launching game...', 'success');
        } catch (err) {
            console.error('[Platform] Burn tx error:', err);
            setGameStatus('Burn failed: ' + (err.message || err), 'error');
            return;
        }
    } else {
        // Free play
        if (freePlaysRemaining <= 0) {
            setGameStatus('No free plays remaining', 'error');
            return;
        }
        setGameStatus('Launching free play...', 'info');
    }

    // Get platform token from backend
    try {
        const tokenRes = await fetch(CONFIG.backendUrl + '/api/session/create-token', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'x-platform-api-key': CONFIG.platformApiKey,
            },
            body: JSON.stringify({
                walletAddress: currentWallet,
                displayName: displayName,
                playType: playType,
                burnTxHash: currentBurnTxHash,
            }),
        });

        if (!tokenRes.ok) {
            const errText = await tokenRes.text();
            setGameStatus('Failed to create session token: ' + errText, 'error');
            return;
        }

        const tokenData = await tokenRes.json();
        launchGame(tokenData.sessionToken);
    } catch (err) {
        console.error('[Platform] Token creation error:', err);
        setGameStatus('Backend error: ' + err.message, 'error');
    }
}

function launchGame(sessionToken) {
    const container = document.getElementById('gameContainer');
    const frame = document.getElementById('gameFrame');

    // Set game URL
    frame.src = CONFIG.gameUrl;
    container.style.display = 'block';

    // Listen for messages from game
    window.addEventListener('message', onGameMessage);

    // Wait for GAME_READY, then send session
    window._pendingSessionToken = sessionToken;

    setGameStatus('Game loading...', 'info');
}

function onGameMessage(event) {
    const data = event.data;
    if (!data || !data.type) return;

    console.log('[Platform] Received from game:', data.type, data);

    switch (data.type) {
        case 'GAME_READY':
            console.log('[Platform] Game ready — sending session');
            sendSessionToGame();
            break;

        case 'GAME_STARTED':
            console.log('[Platform] Game started, playType:', data.playType);
            addStatusMsg('Game started (' + data.playType + ')', 'info');
            break;

        case 'GAME_ENDED':
            console.log('[Platform] Game ended, score:', data.score);
            showGameEndOverlay(data.score, data.leaderboardEligible);

            // Track free play usage locally
            if (currentPlayType === 'free') {
                const key = 'xertraPlay_freePlaysUsed_' + currentWallet;
                const used = parseInt(localStorage.getItem(key) || '0');
                localStorage.setItem(key, String(used + 1));
            }
            break;

        case 'SESSION_ERROR':
            console.error('[Platform] Session error from game:', data.message);
            addStatusMsg('Game session error: ' + data.message, 'error');
            closeGame();
            break;
    }
}

function sendSessionToGame() {
    const frame = document.getElementById('gameFrame');
    console.log('[Platform] sendSessionToGame — frame.contentWindow:', !!frame.contentWindow, 'token:', !!window._pendingSessionToken);

    if (!frame.contentWindow || !window._pendingSessionToken) {
        console.warn('[Platform] Cannot send session — contentWindow or token missing');
        return;
    }

    const sessionData = {
        type: 'INIT_SESSION',
        sessionToken: window._pendingSessionToken,
        walletAddress: currentWallet,
        displayName: displayName,
        playType: currentPlayType,
        burnTxHash: currentBurnTxHash || '',
    };

    console.log('[Platform] Posting INIT_SESSION to iframe:', JSON.stringify(sessionData).substring(0, 200));
    frame.contentWindow.postMessage(sessionData, '*');
    console.log('[Platform] Sent INIT_SESSION to game');
    window._pendingSessionToken = null;
}

function showGameEndOverlay(score, leaderboardEligible) {
    const overlay = document.getElementById('gameOverlay');
    document.getElementById('overlayScore').textContent = score || 0;

    const lbMsg = document.getElementById('overlayLeaderboard');
    if (leaderboardEligible) {
        lbMsg.textContent = 'Score submitted to leaderboard!';
        lbMsg.style.color = '#69f0ae';
    } else {
        lbMsg.textContent = 'Free play — not on leaderboard';
        lbMsg.style.color = '#ffd740';
    }

    overlay.style.display = 'flex';
}

function closeGameOverlay() {
    document.getElementById('gameOverlay').style.display = 'none';
    closeGame();
    showDashboard(); // Refresh stats
}

function closeGame() {
    const container = document.getElementById('gameContainer');
    const frame = document.getElementById('gameFrame');
    frame.src = 'about:blank';
    container.style.display = 'none';
    window.removeEventListener('message', onGameMessage);
    setGameStatus('', '');
}

// ---- Utilities ----
function shortenAddress(addr) {
    if (!addr || addr.length < 10) return addr || '';
    return addr.substring(0, 6) + '...' + addr.substring(addr.length - 4);
}

// ---- Initialize ----
document.addEventListener('DOMContentLoaded', () => {
    showLoginView();
    initWeb3Auth();
});
