using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    public sealed class SkatePlayerSettings : MonoBehaviour
    {
        public int maxHealth = 3;
        public int slideDamage = 1, punchDamage = 3;
        public float slideRagdollTime = .8f, slideForce = 2.5f, slideUpwardForce = .2f;
        public float punchRagdollTime = 1f, punchForce = 4.5f, punchUpwardForce = .5f;
        public Transform throwPoint;
        public float bombForwardSpeed = 12, bombUpwardSpeed = 4, minePlaceDistance = .6f;
        [Min(.1f)] public float slideCooldown = 2.2f, punchCooldown = 1f;
    }
}
