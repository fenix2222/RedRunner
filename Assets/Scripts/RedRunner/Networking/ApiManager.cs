using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RedRunner.Networking
{
    public sealed class ApiManager : MonoBehaviour
    {
        private static ApiManager m_Singleton;

        public static ApiManager Singleton
        {
            get
            {
                return m_Singleton;
            }
        }

        void Awake()
        {
            if (m_Singleton != null)
            {
                Destroy(gameObject);
                return;
            }
            m_Singleton = this;
        }

        private string GetJwt()
        {
            if (Web3AuthManager.Singleton != null && Web3AuthManager.Singleton.CurrentUser != null)
                return Web3AuthManager.Singleton.CurrentUser.appJwtToken ?? "";
            return "";
        }

        public void VerifyAuth(AuthData authData, Action<bool, string> callback)
        {
            StartCoroutine(VerifyAuthCoroutine(authData, callback));
        }

        private IEnumerator VerifyAuthCoroutine(AuthData authData, Action<bool, string> callback)
        {
            var requestBody = new AuthVerifyRequest { idToken = authData.idToken };
            string json = JsonUtility.ToJson(requestBody);

            using (var request = new UnityWebRequest(ApiConfig.AuthVerify, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<AuthVerifyResponse>(request.downloadHandler.text);
                    callback(true, response.token);
                }
                else
                {
                    Debug.LogWarning("[ApiManager] VerifyAuth failed: " + request.error);
                    callback(false, request.error);
                }
            }
        }

        public void SubmitScore(int score, Action<bool, string> callback)
        {
            StartCoroutine(SubmitScoreCoroutine(score, callback));
        }

        private IEnumerator SubmitScoreCoroutine(int score, Action<bool, string> callback)
        {
            var requestBody = new ScoreSubmitRequest { score = score };
            string json = JsonUtility.ToJson(requestBody);

            using (var request = new UnityWebRequest(ApiConfig.LeaderboardSubmit, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + GetJwt());

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    callback(true, request.downloadHandler.text);
                }
                else
                {
                    Debug.LogWarning("[ApiManager] SubmitScore failed: " + request.error);
                    callback(false, request.error);
                }
            }
        }

        // --- Session endpoints (Xertra Play platform integration) ---

        public void ValidatePlatformSession(PlatformSessionValidateRequest data, Action<bool, PlatformSessionValidateResponse> callback)
        {
            StartCoroutine(ValidatePlatformSessionCoroutine(data, callback));
        }

        private IEnumerator ValidatePlatformSessionCoroutine(PlatformSessionValidateRequest data, Action<bool, PlatformSessionValidateResponse> callback)
        {
            string json = JsonUtility.ToJson(data);

            using (var request = new UnityWebRequest(ApiConfig.SessionValidate, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<PlatformSessionValidateResponse>(request.downloadHandler.text);
                    callback(true, response);
                }
                else
                {
                    Debug.LogWarning("[ApiManager] ValidatePlatformSession failed: " + request.error);
                    callback(false, null);
                }
            }
        }

        public void StartGameSession(GameStartRequest data, Action<bool, GameStartResponse> callback)
        {
            StartCoroutine(StartGameSessionCoroutine(data, callback));
        }

        private IEnumerator StartGameSessionCoroutine(GameStartRequest data, Action<bool, GameStartResponse> callback)
        {
            string json = JsonUtility.ToJson(data);

            using (var request = new UnityWebRequest(ApiConfig.SessionStart, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + GetJwt());

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<GameStartResponse>(request.downloadHandler.text);
                    callback(true, response);
                }
                else
                {
                    Debug.LogWarning("[ApiManager] StartGameSession failed: " + request.error);
                    var errorResponse = new GameStartResponse { success = false, error = request.error };
                    callback(false, errorResponse);
                }
            }
        }

        public void CompleteGameSession(string sessionId, int score, Action<bool, GameCompleteResponse> callback)
        {
            StartCoroutine(CompleteGameSessionCoroutine(sessionId, score, callback));
        }

        private IEnumerator CompleteGameSessionCoroutine(string sessionId, int score, Action<bool, GameCompleteResponse> callback)
        {
            var data = new GameCompleteRequest { sessionId = sessionId, score = score };
            string json = JsonUtility.ToJson(data);

            using (var request = new UnityWebRequest(ApiConfig.SessionComplete, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + GetJwt());

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<GameCompleteResponse>(request.downloadHandler.text);
                    callback(true, response);
                }
                else
                {
                    Debug.LogWarning("[ApiManager] CompleteGameSession failed: " + request.error);
                    callback(false, null);
                }
            }
        }

        // --- Leaderboard ---

        public void GetLeaderboard(Action<bool, LeaderboardEntry[]> callback)
        {
            StartCoroutine(GetLeaderboardCoroutine(callback));
        }

        private IEnumerator GetLeaderboardCoroutine(Action<bool, LeaderboardEntry[]> callback)
        {
            using (var request = UnityWebRequest.Get(ApiConfig.LeaderboardGet))
            {
                string jwt = GetJwt();
                if (!string.IsNullOrEmpty(jwt))
                    request.SetRequestHeader("Authorization", "Bearer " + jwt);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string wrappedJson = "{\"entries\":" + request.downloadHandler.text + "}";
                    var response = JsonUtility.FromJson<LeaderboardResponse>(wrappedJson);
                    callback(true, response.entries);
                }
                else
                {
                    Debug.LogWarning("[ApiManager] GetLeaderboard failed: " + request.error);
                    callback(false, null);
                }
            }
        }
    }
}
