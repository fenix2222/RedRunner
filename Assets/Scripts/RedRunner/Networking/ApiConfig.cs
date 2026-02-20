namespace RedRunner.Networking
{
    public static class ApiConfig
    {
#if UNITY_EDITOR
        public static readonly string BaseUrl = "http://localhost:3001/api/";
#else
        public static readonly string BaseUrl = "http://localhost:3001/api/";
#endif

        public static readonly string AuthVerify = BaseUrl + "auth/verify";
        public static readonly string LeaderboardGet = BaseUrl + "leaderboard";
        public static readonly string LeaderboardSubmit = BaseUrl + "leaderboard";
        public static readonly string UserProfile = BaseUrl + "user/profile";
    }
}
