namespace RedRunner.Networking
{
    public static class ApiConfig
    {
        private static string _baseUrl = "http://localhost:8081/api/";

        /// <summary>
        /// Base URL for all API calls. Defaults to RedRunner backend for standalone dev.
        /// Overridden at runtime in platform mode via INIT_SESSION.apiBaseUrl.
        /// </summary>
        public static string BaseUrl
        {
            get => _baseUrl;
            set
            {
                _baseUrl = value.EndsWith("/") ? value : value + "/";
                // Recalculate all derived URLs
                AuthVerify = _baseUrl + "auth/verify";
                LeaderboardGet = _baseUrl + "leaderboard";
                LeaderboardSubmit = _baseUrl + "leaderboard";
                UserProfile = _baseUrl + "user/profile";
                SessionValidate = _baseUrl + "session/validate";
                SessionStart = _baseUrl + "session/start";
                SessionComplete = _baseUrl + "session/complete";
            }
        }

        public static string AuthVerify = _baseUrl + "auth/verify";
        public static string LeaderboardGet = _baseUrl + "leaderboard";
        public static string LeaderboardSubmit = _baseUrl + "leaderboard";
        public static string UserProfile = _baseUrl + "user/profile";

        // Session endpoints (Xertra Play platform integration)
        public static string SessionValidate = _baseUrl + "session/validate";
        public static string SessionStart = _baseUrl + "session/start";
        public static string SessionComplete = _baseUrl + "session/complete";
    }
}
