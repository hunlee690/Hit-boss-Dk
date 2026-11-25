using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    [CreateAssetMenu(menuName = "Hit Boss/Race UI Theme")]
    public sealed class RaceUITheme : ScriptableObject
    {
        [Header("Panels and buttons")]
        public Sprite panel, leaderboard, abilityPanel, greenButton, redButton;
        public Sprite purpleButton, iconFrame, row, selectedRow, statBar, titleBanner, titleWing;
        [Header("Race HUD")]
        public Sprite health, fullHeart, emptyHeart, lap, slash, boostTrack, boostFill, boostIcon, boostEmpty, itemEmpty, punch;
        [Tooltip("Digits in order: 0 through 9")]
        public Sprite[] digits = new Sprite[10];
        public Sprite go, spectator;
        [Header("Pause")]
        public Sprite paused, resume, exit;
        [Header("Results")]
        public Sprite congratulations, you, finished, first, second, third, fourth, leave, match;
        public Sprite trophy, timeIcon, coinIcon, timeWord, killsWord, coinsWord, exitIcon, playIcon;
        public Sprite[] leftLaurels = new Sprite[3], rightLaurels = new Sprite[3];
        [Tooltip("First, second, third")]
        public Sprite[] podiumBanners = new Sprite[3], medals = new Sprite[3];
    }
}
