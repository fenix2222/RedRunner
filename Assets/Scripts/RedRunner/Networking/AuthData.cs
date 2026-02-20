using System;

namespace RedRunner.Networking
{
    [Serializable]
    public class AuthData
    {
        public string walletAddress;
        public string email;
        public string name;
        public string profileImage;
        public string idToken;
        public string appJwtToken;
    }

    [Serializable]
    public class AuthVerifyRequest
    {
        public string idToken;
    }

    [Serializable]
    public class AuthVerifyResponse
    {
        public string token;
        public UserData user;
    }

    [Serializable]
    public class UserData
    {
        public int id;
        public string wallet_address;
        public string email;
        public string name;
        public int high_score;
        public int total_coins;
    }

    [Serializable]
    public class ScoreSubmitRequest
    {
        public int score;
    }

    [Serializable]
    public class LeaderboardResponse
    {
        public LeaderboardEntry[] entries;
    }

    [Serializable]
    public class LeaderboardEntry
    {
        public string name;
        public string wallet_address;
        public int score;
        public string created_at;
    }
}
