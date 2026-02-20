using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RedRunner.Networking;

namespace RedRunner.UI
{
    public class StartScreen : UIScreen
    {
        [SerializeField]
        protected Button PlayButton = null;
        [SerializeField]
        protected Button HelpButton = null;
        [SerializeField]
        protected Button InfoButton = null;
        [SerializeField]
        protected Button ExitButton = null;

        [Header("User Info")]
        [SerializeField]
        protected Text WalletAddressText = null;
        [SerializeField]
        protected Text UsernameText = null;
        [SerializeField]
        protected Button LogoutButton = null;

        private void Start()
        {
            PlayButton.SetButtonAction(() =>
            {
                var uiManager = UIManager.Singleton;
                var InGameScreen = uiManager.UISCREENS.Find(el => el.ScreenInfo == UIScreenInfo.IN_GAME_SCREEN);
                if (InGameScreen != null)
                {
                    uiManager.OpenScreen(InGameScreen);
                    GameManager.Singleton.StartGame();
                }
            });

            ExitButton.SetButtonAction(() =>
            {
                GameManager.Singleton.ExitGame();
            });

            if (LogoutButton != null)
            {
                LogoutButton.SetButtonAction(() =>
                {
                    Web3AuthManager.Singleton.Logout();
                });
            }

            // Subscribe to auth changes so UI updates when logout completes asynchronously
            Web3AuthManager.OnAuthStateChanged += OnAuthStateChanged;
        }

        private void OnDestroy()
        {
            Web3AuthManager.OnAuthStateChanged -= OnAuthStateChanged;
        }

        private void OnAuthStateChanged(bool authenticated, AuthData data)
        {
            UpdateUserInfo();

            if (!authenticated)
            {
                // Show login overlay on top of the menu (same as initial app load)
                var loginScreen = UIManager.Singleton.GetUIScreen(UIScreenInfo.LOGIN_SCREEN);
                if (loginScreen != null && !loginScreen.IsOpen)
                    UIManager.Singleton.OpenScreenOverlay(loginScreen);
            }
        }

        public override void UpdateScreenStatus(bool open)
        {
            base.UpdateScreenStatus(open);
            if (open)
            {
                UpdateUserInfo();
            }
        }

        private void UpdateUserInfo()
        {
            bool isLoggedIn = Web3AuthManager.Singleton != null
                && Web3AuthManager.Singleton.IsAuthenticated.Value
                && Web3AuthManager.Singleton.CurrentUser != null;

            // Hide wallet address and logout button when not logged in
            if (WalletAddressText != null)
                WalletAddressText.gameObject.SetActive(isLoggedIn);
            if (LogoutButton != null)
                LogoutButton.gameObject.SetActive(isLoggedIn);
            // Username not used for wallet logins — always hide
            if (UsernameText != null)
                UsernameText.gameObject.SetActive(false);

            if (!isLoggedIn)
                return;

            var user = Web3AuthManager.Singleton.CurrentUser;

            if (WalletAddressText != null)
                WalletAddressText.text = Web3AuthManager.ShortenAddress(user.walletAddress);
        }
    }
}
