using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RedRunner.Networking
{
    public sealed class Web3AuthManager : MonoBehaviour
    {
        public delegate void AuthStateHandler(bool authenticated, AuthData data);
        public static event AuthStateHandler OnAuthStateChanged;

        private static Web3AuthManager m_Singleton;

        public static Web3AuthManager Singleton
        {
            get
            {
                return m_Singleton;
            }
        }

        [Header("Web3Auth Configuration")]
        [SerializeField]
        private string m_ClientId = "BMJIf8AYFltty8i-VNd6sqXeHEOQNeyP8QBxFrGZfVN3jtTT-3zUSrOn4Jvv59QxzRQ-3zl8JBsMnSr0Z4vMp84";
        [SerializeField]
        private string m_ChainId = "0x19a91";
        [SerializeField]
        private string m_RpcTarget = "https://rpc.xertra.com";
        [SerializeField]
        private string m_BlockExplorerUrl = "https://explorer.xertra.com";
        [SerializeField]
        private string m_ChainDisplayName = "Stratis Mainnet";
        [SerializeField]
        private string m_TickerName = "STRAX";

        [Header("Editor Settings")]
        [SerializeField]
        private bool m_SkipAuthInEditor = true;

        public Property<bool> IsAuthenticated = new Property<bool>(false);
        private AuthData m_CurrentUser;

        public AuthData CurrentUser
        {
            get { return m_CurrentUser; }
        }

        private bool m_Initializing = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Web3Auth_Init(string clientId, string chainId, string rpcTarget,
            string blockExplorerUrl, string chainDisplayName, string tickerName, string gameObjectName);

        [DllImport("__Internal")]
        private static extern void Web3Auth_Login();

        [DllImport("__Internal")]
        private static extern void Web3Auth_Logout();

        [DllImport("__Internal")]
        private static extern void Web3Auth_CheckSession();
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
#if UNITY_WEBGL && !UNITY_EDITOR
            m_Initializing = true;
            Web3Auth_Init(m_ClientId, m_ChainId, m_RpcTarget, m_BlockExplorerUrl,
                m_ChainDisplayName, m_TickerName, gameObject.name);
#elif UNITY_EDITOR
            if (m_SkipAuthInEditor)
            {
                m_CurrentUser = new AuthData
                {
                    walletAddress = "0xEditorTestWallet",
                    email = "editor@test.com",
                    name = "Editor User"
                };
                IsAuthenticated.Value = true;
                OnAuthStateChanged?.Invoke(true, m_CurrentUser);
            }
#endif
        }

        public void Login()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Web3Auth_Login();
#elif UNITY_EDITOR
            if (m_SkipAuthInEditor)
            {
                m_CurrentUser = new AuthData
                {
                    walletAddress = "0xEditorTestWallet",
                    email = "editor@test.com",
                    name = "Editor User"
                };
                IsAuthenticated.Value = true;
                OnAuthStateChanged?.Invoke(true, m_CurrentUser);
            }
#endif
        }

        public void Logout()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Web3Auth_Logout();
#elif UNITY_EDITOR
            m_CurrentUser = null;
            IsAuthenticated.Value = false;
            OnAuthStateChanged?.Invoke(false, null);
#endif
        }

        // --- JS Callbacks (called via SendMessage from Web3AuthBridge.jslib) ---

        public void OnInitComplete(string _)
        {
            m_Initializing = false;
            Debug.Log("[Web3Auth] Initialized successfully");
#if UNITY_WEBGL && !UNITY_EDITOR
            Web3Auth_CheckSession();
#endif
        }

        public void OnInitFailed(string error)
        {
            m_Initializing = false;
            Debug.LogError("[Web3Auth] Init failed: " + error);
        }

        public void OnLoginSuccess(string jsonData)
        {
            Debug.Log("[Web3Auth] Login success");
            m_CurrentUser = JsonUtility.FromJson<AuthData>(jsonData);
            IsAuthenticated.Value = true;

            if (ApiManager.Singleton != null && !string.IsNullOrEmpty(m_CurrentUser.idToken))
            {
                ApiManager.Singleton.VerifyAuth(m_CurrentUser, OnBackendAuthComplete);
            }
            else
            {
                OnAuthStateChanged?.Invoke(true, m_CurrentUser);
            }
        }

        public void OnLoginFailed(string error)
        {
            Debug.LogError("[Web3Auth] Login failed: " + error);
            OnAuthStateChanged?.Invoke(false, null);
        }

        public void OnLogoutComplete(string _)
        {
            Debug.Log("[Web3Auth] Logged out");
            m_CurrentUser = null;
            IsAuthenticated.Value = false;
            OnAuthStateChanged?.Invoke(false, null);
        }

        public void OnSessionFound(string jsonData)
        {
            Debug.Log("[Web3Auth] Existing session found");
            OnLoginSuccess(jsonData);
        }

        public void OnNoSession(string _)
        {
            Debug.Log("[Web3Auth] No existing session");
            OnAuthStateChanged?.Invoke(false, null);
        }

        private void OnBackendAuthComplete(bool success, string jwtToken)
        {
            if (success && m_CurrentUser != null)
            {
                m_CurrentUser.appJwtToken = jwtToken;
                Debug.Log("[Web3Auth] Backend auth complete");
            }
            else
            {
                Debug.LogWarning("[Web3Auth] Backend auth failed, continuing without JWT");
            }
            OnAuthStateChanged?.Invoke(true, m_CurrentUser);
        }

        public static string ShortenAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 10)
                return address ?? "";
            return address.Substring(0, 6) + "..." + address.Substring(address.Length - 4);
        }
    }
}
