using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RedRunner.Networking;

namespace RedRunner.UI
{
    public class EndScreen : UIScreen
    {
        [SerializeField]
        protected Button ResetButton = null;
        [SerializeField]
        protected Button HomeButton = null;
        [SerializeField]
        protected Button ExitButton = null;

        [Header("Leaderboard Status")]
        [SerializeField]
        protected Text LeaderboardStatusText = null;

        private void Start()
        {
            ResetButton.SetButtonAction(() =>
            {
                GameManager.Singleton.Reset();
                var ingameScreen = UIManager.Singleton.GetUIScreen(UIScreenInfo.IN_GAME_SCREEN);
                UIManager.Singleton.OpenScreen(ingameScreen);
                GameManager.Singleton.StartGame();
            });
        }

        public override void UpdateScreenStatus(bool open)
        {
            base.UpdateScreenStatus(open);

            if (open)
            {
                UpdateLeaderboardStatus();
            }
        }

        private void UpdateLeaderboardStatus()
        {
            if (LeaderboardStatusText == null)
                return;

            bool isPlatformMode = SessionManager.Singleton != null
                && SessionManager.Singleton.CurrentMode == AuthMode.Platform;

            if (!isPlatformMode)
            {
                // Standalone mode — no special indicator needed
                LeaderboardStatusText.gameObject.SetActive(false);
                return;
            }

            LeaderboardStatusText.gameObject.SetActive(true);

            if (SessionManager.Singleton.IsLeaderboardEligible)
            {
                LeaderboardStatusText.text = "Score submitted to leaderboard!";
                LeaderboardStatusText.color = new Color(0.4f, 1f, 0.4f); // green
            }
            else
            {
                LeaderboardStatusText.text = "Free play \u2014 not on leaderboard";
                LeaderboardStatusText.color = new Color(1f, 0.8f, 0.3f); // yellow
            }
        }
    }
}