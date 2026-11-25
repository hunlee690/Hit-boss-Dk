using UnityEngine;
using UnityEngine.Rendering;

namespace HitBoss.Multiplayer.Skate
{
    public static class SkateGrenadeLaunch
    {
        public static void Calculate(SkateCombat skater, SkatePlayerSettings inventory, float charge, Vector3 aim,
            out Vector3 origin, out Vector3 velocity)
        {
            aim = SafeAim(aim, skater.Player.movement.transform.forward);
            Vector3 forward = Vector3.ProjectOnPlane(aim, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f)
                forward = Vector3.ProjectOnPlane(skater.Player.movement.transform.forward, Vector3.up).normalized;
            origin = skater.Position + Vector3.up * .6f + forward * .85f;
            var rules = MatchConnection.Instance?.ActiveRules as SkateMatchRules;
            float radius = ProjectileRadius(rules != null ? rules.bomb : null);
            Vector3 hand = skater.Position + Vector3.up * .6f;
            float clearDistance = .85f;
            foreach (var hit in Physics.SphereCastAll(hand, radius, forward, .85f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<NetworkPlayer>() != null ||
                    hit.collider.GetComponentInParent<SkateNetworkItem>() != null) continue;
                clearDistance = Mathf.Min(clearDistance, Mathf.Max(0f, hit.distance - .03f));
            }
            origin = hand + forward * clearDistance;
            velocity = CalculateVelocity(inventory, skater.ThrowMomentum, charge, aim, forward);
        }

        public static float ProjectileRadius(SkateNetworkItem prefab)
        {
            var sphere = prefab != null ? prefab.GetComponent<SphereCollider>() : null;
            if (sphere == null) return .2f;
            Vector3 scale = sphere.transform.lossyScale;
            return sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        // The preview and the host use the same direction, charge and inherited momentum.
        public static Vector3 CalculateVelocity(SkatePlayerSettings inventory, Vector3 momentum, float charge,
            Vector3 aim, Vector3 fallbackForward)
        {
            aim = SafeAim(aim, fallbackForward);
            Vector3 forward = Vector3.ProjectOnPlane(aim, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f)
                forward = SafeAim(Vector3.ProjectOnPlane(fallbackForward, Vector3.up), Vector3.forward);
            if (float.IsNaN(charge) || float.IsInfinity(charge)) charge = 0f;
            charge = Mathf.Clamp01(charge);
            float speed = inventory.bombForwardSpeed * Mathf.Lerp(.7f, 2.05f, charge);
            float vertical = Mathf.Clamp(aim.y, Mathf.Sin(-65f * Mathf.Deg2Rad), Mathf.Sin(35f * Mathf.Deg2Rad));
            Vector3 direction = forward * Mathf.Sqrt(1f - vertical * vertical) + Vector3.up * vertical;
            float lift = inventory.bombUpwardSpeed * Mathf.Lerp(.25f, .6f, charge) * Mathf.Clamp01(1f + vertical);
            return Vector3.ClampMagnitude(momentum, 50f) + direction * speed + Vector3.up * lift;
        }

        static Vector3 SafeAim(Vector3 aim, Vector3 fallback)
        {
            float magnitude = aim.sqrMagnitude;
            if (magnitude < .001f || float.IsNaN(magnitude) || float.IsInfinity(magnitude))
                aim = fallback.sqrMagnitude > .001f ? fallback : Vector3.forward;
            return aim.normalized;
        }
    }

    [DefaultExecutionOrder(50)]
    internal sealed class SkateGrenadeAim : MonoBehaviour
    {
        const int MaxPoints = 81;
        const float SampleTime = .05f;
        readonly RaycastHit[] hits = new RaycastHit[16];
        SkateCombat skater;
        SkatePlayerSettings inventory;
        LineRenderer trajectory;
        float projectileRadius = .2f;

        void Awake()
        {
            skater = GetComponentInParent<SkateCombat>();
            var networkPlayer = GetComponentInParent<NetworkPlayer>();
            if (networkPlayer != null && networkPlayer.movement != null)
                inventory = networkPlayer.movement.GetComponent<SkatePlayerSettings>();
            var rules = MatchConnection.Instance?.ActiveRules as SkateMatchRules;
            projectileRadius = SkateGrenadeLaunch.ProjectileRadius(rules != null ? rules.bomb : null);
            var line = new GameObject("Grenade aim arc", typeof(LineRenderer));
            line.transform.SetParent(transform, false);
            trajectory = line.GetComponent<LineRenderer>();
            trajectory.sharedMaterial = SkateCombatEffects.EffectMaterial();
            trajectory.startWidth = .055f;
            trajectory.endWidth = .085f;
            trajectory.startColor = new Color(.6f, .94f, 1f, .88f);
            trajectory.endColor = new Color(1f, .62f, .22f, .98f);
            trajectory.shadowCastingMode = ShadowCastingMode.Off;
            trajectory.receiveShadows = false;
            trajectory.useWorldSpace = true;
            trajectory.enabled = false;
        }

        void LateUpdate()
        {
            bool show = skater != null && skater.IsSpawned && skater.CanAct && !skater.Player.IsPaused
                && skater.IsChargingThrow && inventory != null;
            trajectory.enabled = show;
            if (!show) return;
            SkateGrenadeLaunch.Calculate(skater, inventory, skater.ThrowCharge, skater.GrenadeAimDirection(),
                out var origin, out var velocity);

            trajectory.positionCount = MaxPoints;
            trajectory.SetPosition(0, origin);
            Vector3 previous = origin;
            for (int i = 1; i < MaxPoints; i++)
            {
                float time = i * SampleTime;
                Vector3 point = origin + velocity * time + .5f * Physics.gravity * time * time;
                Vector3 segment = point - previous;
                float distance = segment.magnitude;
                int count = Physics.SphereCastNonAlloc(previous, projectileRadius, segment.normalized, hits, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float impact = distance;
                for (int j = 0; j < count; j++)
                {
                    if (hits[j].collider.transform.root == skater.transform.root ||
                        hits[j].collider.GetComponentInParent<SkateNetworkItem>() != null) continue;
                    impact = Mathf.Min(impact, hits[j].distance);
                }
                if (impact < distance)
                {
                    trajectory.SetPosition(i, previous + segment.normalized * impact);
                    trajectory.positionCount = i + 1;
                    break;
                }
                trajectory.SetPosition(i, point);
                previous = point;
            }
        }
    }
}
