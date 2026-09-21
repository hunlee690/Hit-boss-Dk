using System;
using System.Collections.Generic;

namespace HitBoss.Social
{
    [Serializable]
    public class PlayerProfile
    {
        public int schemaVersion = 1;
        public string gamePlayerId;
        public string username;
        public int totalKills;
        public int totalDeaths;
        public int experience;
        public int coins;
        public int gems;
        public bool currencyInitialized;
        public List<string> appliedBatches = new List<string>();

        // 100 XP for level 2, then 50 more XP for each subsequent level.
        public int Level { get { int level = 1; long remaining = Math.Max(0, experience); while (remaining >= XpForLevel(level)) { remaining -= XpForLevel(level); level++; } return level; } }
        public static int XpForLevel(int level) => 100 + (Math.Max(1, level) - 1) * 50;
        public int XpIntoLevel { get { int level = Level; return (int)(Math.Max(0L, experience) - ((long)(level - 1) * 100 + (long)(level - 1) * (level - 2) * 25)); } }
        public int XpToNextLevel => XpForLevel(Level);
        public float Progress => (float)XpIntoLevel / XpToNextLevel;
        public PlayerProfile Copy() => UnityEngine.JsonUtility.FromJson<PlayerProfile>(UnityEngine.JsonUtility.ToJson(this));
    }

    public class ProfileRecord
    {
        public string playerId;
        public string writeLock;
        public PlayerProfile profile;
    }

    [Serializable]
    public class ProgressBatch
    {
        public string id = Guid.NewGuid().ToString("N");
        public int kills;
        public int deaths;
        public int xp;
        public int coins;
        public int gems;
    }

    public enum SocialList { Friends, Requests, Sent, Search }
    public class SocialPlayer
    {
        public string playerId;
        public string username;
        public string presence;
        public bool online;
    }
}
