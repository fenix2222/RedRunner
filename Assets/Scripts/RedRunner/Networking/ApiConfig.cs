namespace RedRunner.Networking
{
    public static class ApiConfig
    {
#if UNITY_EDITOR
        public static readonly string BaseUrl = "http://localhost:8081/api/";
#else
        public static readonly string BaseUrl = "http://localhost:8081/api/";
#endif

        public static readonly string AuthVerify = BaseUrl + "auth/verify";
        public static readonly string LeaderboardGet = BaseUrl + "leaderboard";
        public static readonly string LeaderboardSubmit = BaseUrl + "leaderboard";
        public static readonly string UserProfile = BaseUrl + "user/profile";

        // Session endpoints (Xertra Play platform integration)
        public static readonly string SessionValidate = BaseUrl + "session/validate";
        public static readonly string SessionStart = BaseUrl + "session/start";
        public static readonly string SessionComplete = BaseUrl + "session/complete";
    }
}
