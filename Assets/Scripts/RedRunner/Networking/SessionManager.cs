using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RedRunner.Networking
{
    public enum AuthMode { Standalone, Platform, Editor }
    public enum PlayType { Free, Paid }

    public sealed class SessionManager : MonoBehaviour
    {
        private static SessionManager m_Singleton;

        public static SessionManager Singleton
        {
            get { return m_Singleton; }
        }

        [Header("Settings")]
        [SerializeField]
        private float m_PlatformDetectTimeout = 3f;

        public AuthMode CurrentMode { get; private set; }
        public PlayType CurrentPlayType { get; private set; }
        public string SessionId { get; private set; }
        public string BurnTxHash { get; private set; }
        public string DisplayName { get; private set; }
        public int FreePlaysRemaining { get; private set; }
        public bool IsLeaderboardEligible => CurrentPlayType == PlayType.Paid;
        public Property<bool> IsSessionReady = new Property<bool>(false);

        private bool m_SessionReceived = false;
        private Coroutine m_DetectCoroutine;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PlatformBridge_StartListening(string gameObjectName);

        [DllImport("__Internal")]
        private static extern void PlatformBridge_SendToParent(string json);
#endif

        void Awake()
        {
            if (m_Singleton != null)
            {
                Destroy(gameObject);
                return;
            }
            m_Singleton = this;
        }

        void Start()
        {
            Debug.Log("[SessionManager] v2 — Start() called on " + gameObject.name);
#if UNITY_EDITOR
            // Editor mode: bypass everything, use Web3AuthManager's editor bypass
            CurrentMode = AuthMode.Editor;
            CurrentPlayType = PlayType.Paid; // Default to paid so leaderboard works in editor
            IsSessionReady.Value = true;
            Debug.Log("[SessionManager] Editor mode — session ready");
#elif UNITY_WEBGL
            // Start listening for platform session + timeout fallback
            Debug.Log("[SessionManager] WebGL mode — starting platform detection (timeout: " + m_PlatformDetectTimeout + "s)");
            PlatformBridge_StartListening(gameObject.name);
            m_DetectCoroutine = StartCoroutine(DetectModeCoroutine());
#endif
        }

        private IEnumerator DetectModeCoroutine()
        {
            float elapsed = 0f;
            while (!m_SessionReceived && elapsed < m_PlatformDetectTimeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!m_SessionReceived)
            {
                // No platform session received — enter standalone mode
                CurrentMode = AuthMode.Standalone;
                Debug.Log("[SessionManager] No platform session — entering Standalone mode");

                if (Web3AuthManager.Singleton != null)
                {
                    Web3AuthManager.Singleton.InitializeStandalone();
                }

                IsSessionReady.Value = true;
            }
        }

        // Called from PlatformBridge.jslib via SendMessage
        public void OnPlatformSessionReceived(string json)
        {
            Debug.Log("[SessionManager] OnPlatformSessionReceived called with: " + json);
            if (m_SessionReceived) return;
            m_SessionReceived = true;

            if (m_DetectCoroutine != null)
            {
                StopCoroutine(m_DetectCoroutine);
                m_DetectCoroutine = null;
            }

            CurrentMode = AuthMode.Platform;
            Debug.Log("[SessionManager] Platform session received");

            var sessionData = JsonUtility.FromJson<PlatformSessionData>(json);

            // Override API base URL if provided by the portal
            if (!string.IsNullOrEmpty(sessionData.apiBaseUrl))
            {
                Debug.Log("[SessionManager] Overriding ApiConfig.BaseUrl → " + sessionData.apiBaseUrl);
                ApiConfig.BaseUrl = sessionData.apiBaseUrl;
            }

            DisplayName = sessionData.displayName;
            BurnTxHash = sessionData.burnTxHash;
            CurrentPlayType = sessionData.playType == "paid" ? PlayType.Paid : PlayType.Free;

            // Validate session with backend
            var validateRequest = new PlatformSessionValidateRequest
            {
                sessionToken = sessionData.sessionToken,
                walletAddress = sessionData.walletAddress,
                playType = sessionData.playType,
                burnTxHash = sessionData.burnTxHash
            };

            if (ApiManager.Singleton != null)
            {
                ApiManager.Singleton.ValidatePlatformSession(validateRequest, OnSessionValidated);
            }
            else
            {
                Debug.LogError("[SessionManager] ApiManager not available");
                NotifyParent("SESSION_ERROR", "Backend not available");
            }
        }

        private void OnSessionValidated(bool success, PlatformSessionValidateResponse response)
        {
            Debug.Log("[SessionManager] OnSessionValidated — success: " + success);
            if (success && response != null)
            {
                SessionId = response.sessionId;
                FreePlaysRemaining = response.freePlaysRemaining;

                // Inject auth into Web3AuthManager so all downstream code works
                if (Web3AuthManager.Singleton != null)
                {
                    var authData = new AuthData
                    {
                        walletAddress = DisplayName, // Will be overwritten below
                        name = DisplayName,
                        appJwtToken = response.token
                    };

                    // Get wallet from the original session data
                    // Re-parse isn't needed since we can use what Web3AuthManager gets
                    Web3AuthManager.Singleton.SetExternalSession(authData);
                }

                IsSessionReady.Value = true;
                Debug.Log("[SessionManager] Platform session validated. SessionId: " + SessionId);
            }
            else
            {
                Debug.LogError("[SessionManager] Platform session validation failed");
                NotifyParent("SESSION_ERROR", "Session validation failed");
            }
        }

        public void SetSessionId(string sessionId)
        {
            SessionId = sessionId;
        }

        public void SetPlayType(PlayType playType)
        {
            CurrentPlayType = playType;
        }

        public void SetFreePlaysRemaining(int remaining)
        {
            FreePlaysRemaining = remaining;
        }

        public void NotifyParent(string type, string extraData = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (CurrentMode != AuthMode.Platform) return;

            var msg = new ParentMessage { type = type };

            if (type == "GAME_STARTED")
            {
                msg.playType = CurrentPlayType == PlayType.Paid ? "paid" : "free";
            }
            else if (type == "GAME_ENDED")
            {
                msg.playType = CurrentPlayType == PlayType.Paid ? "paid" : "free";
                msg.leaderboardEligible = IsLeaderboardEligible;
                // score is set by caller
            }
            else if (type == "SESSION_ERROR")
            {
                msg.message = extraData ?? "";
            }

            string json = JsonUtility.ToJson(msg);
            PlatformBridge_SendToParent(json);
#endif
        }

        public void NotifyGameEnded(int score)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (CurrentMode != AuthMode.Platform) return;

            var msg = new ParentMessage
            {
                type = "GAME_ENDED",
                score = score,
                playType = CurrentPlayType == PlayType.Paid ? "paid" : "free",
                leaderboardEligible = IsLeaderboardEligible
            };

            string json = JsonUtility.ToJson(msg);
            PlatformBridge_SendToParent(json);
#endif
        }
    }
}
