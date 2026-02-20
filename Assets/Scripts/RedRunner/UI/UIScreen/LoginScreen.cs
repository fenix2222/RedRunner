using UnityEngine;
using UnityEngine.UI;
using RedRunner.Networking;

namespace RedRunner.UI
{
    public class LoginScreen : UIScreen
    {
        [SerializeField]
        protected Button LoginButton = null;
        [SerializeField]
        protected Text StatusText = null;

        private void Start()
        {
            LoginButton.SetButtonAction(() =>
            {
                if (StatusText != null)
                    StatusText.text = "Connecting...";
                LoginButton.interactable = false;
                Web3AuthManager.Singleton.Login();
            });

            Web3AuthManager.OnAuthStateChanged += OnAuthChanged;
        }

        public override void UpdateScreenStatus(bool open)
        {
            // Directly control visibility since programmatic animations may not work
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = open ? 1f : 0f;
                m_CanvasGroup.interactable = open;
                m_CanvasGroup.blocksRaycasts = open;
            }
            if (m_Animator != null)
                m_Animator.SetBool("Open", open);
            IsOpen = open;
        }

        private void OnAuthChanged(bool authenticated, AuthData data)
        {
            if (authenticated)
            {
                // Just close the login overlay — StartScreen is already visible underneath
                UIManager.Singleton.CloseScreen(this);
            }
            else
            {
                if (StatusText != null)
                    StatusText.text = "Sign in to play";
                if (LoginButton != null)
                    LoginButton.interactable = true;
            }
        }

        private void OnDestroy()
        {
            Web3AuthManager.OnAuthStateChanged -= OnAuthChanged;
        }
    }
}
