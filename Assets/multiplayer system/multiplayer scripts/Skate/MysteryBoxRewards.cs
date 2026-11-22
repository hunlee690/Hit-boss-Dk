using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    public enum MysteryBoxReward { Bomb, Mine, Boost }

    [CreateAssetMenu(menuName = "Hit Boss/Mystery Box Rewards")]
    public sealed class MysteryBoxRewards : ScriptableObject
    {
        [Header("Reward percentages — set the total to 100")]
        [Tooltip("Use a total of 100 for percentages. Other totals are normalized; zero disables a reward.")]
        [Range(0f, 100f)] public float bombPercent = 45f;
        [Range(0f, 100f)] public float minePercent = 30f;
        [Range(0f, 100f)] public float boostPercent = 25f;

        public MysteryBoxReward Roll()
        {
            float bomb = Mathf.Max(0f, bombPercent);
            float mine = Mathf.Max(0f, minePercent);
            float boost = Mathf.Max(0f, boostPercent);
            float total = bomb + mine + boost;
            if (total <= 0f) return MysteryBoxReward.Bomb;
            float value = Random.Range(0f, total);
            if (value < bomb) return MysteryBoxReward.Bomb;
            if (value < bomb + mine) return MysteryBoxReward.Mine;
            return boost > 0f ? MysteryBoxReward.Boost : mine > 0f ? MysteryBoxReward.Mine : MysteryBoxReward.Bomb;
        }

        void OnValidate()
        {
            bombPercent = Mathf.Clamp(bombPercent, 0f, 100f);
            minePercent = Mathf.Clamp(minePercent, 0f, 100f);
            boostPercent = Mathf.Clamp(boostPercent, 0f, 100f);
            if (bombPercent + minePercent + boostPercent <= 0f) bombPercent = 100f;
        }
    }
}
