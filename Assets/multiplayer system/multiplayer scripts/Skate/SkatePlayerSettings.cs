using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    public sealed class SkatePlayerSettings : MonoBehaviour
    {
        public int maxHealth = 3, maxStamina = 3;
        public int slideStaminaCost = 2, slideDamage = 1, punchStaminaCost = 3, punchDamage = 3;
        public float slideRagdollTime = 2, slideForce = 2.5f, slideUpwardForce = .2f;
        public float punchRagdollTime = 1.5f, punchForce = 4.5f, punchUpwardForce = .5f;
        public Transform throwPoint;
        public float bombForwardSpeed = 12, bombUpwardSpeed = 4, minePlaceDistance = .6f;
        public LayerMask groundLayers;
    }
}
