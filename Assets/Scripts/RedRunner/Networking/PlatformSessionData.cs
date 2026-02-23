using System;

namespace RedRunner.Networking
{
    [Serializable]
    public class PlatformSessionData
    {
        public string type;
        public string sessionToken;
        public string walletAddress;
        public string displayName;
        public string playType; // "free" or "paid"
        public string burnTxHash;
    }

    [Serializable]
    public class PlatformSessionValidateRequest
    {
        public string sessionToken;
        public string walletAddress;
        public string playType;
        public string burnTxHash;
    }

    [Serializable]
    public class PlatformSessionValidateResponse
    {
        public string token;
        public string sessionId;
        public int freePlaysRemaining;
        public bool leaderboardEligible;
    }

    [Serializable]
    public class GameStartRequest
    {
        public string playType;
        public string burnTxHash;
    }

    [Serializable]
    public class GameStartResponse
    {
        public string sessionId;
        public bool success;
        public string error;
        public int freePlaysRemaining;
        public bool leaderboardEligible;
    }

    [Serializable]
    public class GameCompleteRequest
    {
        public string sessionId;
        public int score;
    }

    [Serializable]
    public class GameCompleteResponse
    {
        public bool success;
        public bool leaderboardEligible;
    }

    [Serializable]
    public class ParentMessage
    {
        public string type;
        public string playType;
        public int score;
        public bool leaderboardEligible;
        public string message;
    }
}
