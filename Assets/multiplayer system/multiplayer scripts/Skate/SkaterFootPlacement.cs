using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    // The generic rig has an independent foot/skate joint on each side.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerController), typeof(CapsuleCollider))]
    public sealed class SkaterFootPlacement : MonoBehaviour
    {
        sealed class Leg
        {
            public Transform Thigh, Shin, Ankle, Foot;
            public Quaternion ThighRotation, ShinRotation, FootRotation;
            public Vector3 FootPosition;
            public float Weight, Offset;
            public Vector3 Normal = Vector3.up;
        }

        readonly RaycastHit[] groundHits = new RaycastHit[16];
        readonly Leg[] legs = { new Leg(), new Leg() };
        PlayerController skater;
        SkateCombat combat;
        RagdollController ragdoll;
        CapsuleCollider capsule;
        Transform pelvis;
        Vector3 pelvisPosition;
        bool applied;

        void Awake()
        {
            skater = GetComponent<PlayerController>();
            combat = GetComponentInParent<SkateCombat>();
            ragdoll = GetComponent<RagdollController>();
            capsule = GetComponent<CapsuleCollider>();
            if (skater.animator == null) { enabled = false; return; }

            foreach (Transform joint in skater.animator.GetComponentsInChildren<Transform>(true))
            {
                Bind(legs[0], joint, ".L");
                Bind(legs[1], joint, ".R");
            }
            foreach (Leg leg in legs)
            {
                if (leg.Thigh != null && leg.Shin != null && leg.Ankle != null && leg.Foot != null) continue;
                enabled = false;
                return;
            }
            pelvis = legs[0].Thigh.parent;
        }

        static void Bind(Leg leg, Transform joint, string side)
        {
            if (joint.name == "thigh" + side) leg.Thigh = joint;
            else if (joint.name == "shin" + side) leg.Shin = joint;
            else if (joint.name == "shin" + side + "_end") leg.Ankle = joint;
            else if (joint.name == "shin" + side + ".001") leg.Foot = joint;
        }

        void Update() => RestoreAnimatedPose();
        void OnDisable() => RestoreAnimatedPose();

        void RestoreAnimatedPose()
        {
            if (!applied) return;
            applied = false;
            if (ragdoll != null && ragdoll.IsRagdolled) return;
            pelvis.localPosition = pelvisPosition;
            foreach (Leg leg in legs)
            {
                leg.Thigh.localRotation = leg.ThighRotation;
                leg.Shin.localRotation = leg.ShinRotation;
                leg.Foot.localPosition = leg.FootPosition;
                leg.Foot.localRotation = leg.FootRotation;
            }
        }

        void LateUpdate()
        {
            if (!skater.animator.isActiveAndEnabled || skater.IsJumping ||
                (ragdoll != null && ragdoll.IsRagdolled) ||
                (combat != null && (!combat.Active || combat.Downed.Value || combat.HasFinishedRace)) ||
                !FindGround(transform.TransformPoint(capsule.center), capsule.height * .5f * transform.lossyScale.y + .22f, out _))
            {
                foreach (Leg leg in legs) leg.Weight = 0;
                return;
            }

            float blend = 1f - Mathf.Exp(-22f * Time.deltaTime);
            float pelvisDrop = 0;
            foreach (Leg leg in legs)
            {
                float weight = 0f;
                if (FindGround(leg.Foot.position + Vector3.up * .48f, 1f, out var ground))
                {
                    float offset = ground.point.y + skater.SkateGroundClearance + .025f - leg.Foot.position.y;
                    float liftTolerance = skater.IsSkateMode ? .4f : .24f;
                    weight = 1f - Mathf.InverseLerp(.08f, liftTolerance, -offset);
                    leg.Offset = Mathf.Lerp(leg.Offset, Mathf.Clamp(offset, -.27f, .32f), blend);
                    leg.Normal = Vector3.Slerp(leg.Normal, ground.normal, blend);
                }
                leg.Weight = Mathf.Lerp(leg.Weight, weight, blend);
                pelvisDrop = Mathf.Min(pelvisDrop, leg.Offset * leg.Weight);
                leg.ThighRotation = leg.Thigh.localRotation;
                leg.ShinRotation = leg.Shin.localRotation;
                leg.FootRotation = leg.Foot.localRotation;
                leg.FootPosition = leg.Foot.localPosition;
            }

            pelvisPosition = pelvis.localPosition;
            pelvisDrop = Mathf.Max(-.18f, pelvisDrop);
            pelvis.position += Vector3.up * pelvisDrop;
            applied = true;
            foreach (Leg leg in legs)
            {
                if (leg.Weight > .001f) PlaceFoot(leg, pelvisDrop);
                else leg.Foot.position += Vector3.up * pelvisDrop;
            }
        }

        void PlaceFoot(Leg leg, float pelvisDrop)
        {
            Vector3 hip = leg.Thigh.position;
            Vector3 knee = leg.Shin.position;
            Vector3 ankle = leg.Ankle.position;
            float upperLength = Vector3.Distance(hip, knee);
            float lowerLength = Vector3.Distance(knee, ankle);
            Vector3 desired = ankle + Vector3.up * (leg.Offset * leg.Weight - pelvisDrop);
            Vector3 reach = desired - hip;
            if (upperLength < .01f || lowerLength < .01f || reach.sqrMagnitude < .0001f) return;
            Vector3 direction = reach.normalized;
            float length = Mathf.Clamp(reach.magnitude,
                Mathf.Abs(upperLength - lowerLength) + .001f, upperLength + lowerLength - .001f);
            Vector3 bend = Vector3.ProjectOnPlane(knee - hip, direction);
            if (bend.sqrMagnitude < .0001f) bend = Vector3.ProjectOnPlane(transform.forward, direction);
            float along = (upperLength * upperLength - lowerLength * lowerLength + length * length) / (2f * length);
            float sideways = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
            Vector3 kneeTarget = hip + direction * along + bend.normalized * sideways;

            leg.Thigh.rotation = Quaternion.FromToRotation(knee - hip, kneeTarget - hip) * leg.Thigh.rotation;
            leg.Shin.rotation = Quaternion.FromToRotation(leg.Ankle.position - leg.Shin.position,
                hip + direction * length - leg.Shin.position) * leg.Shin.rotation;
            leg.Foot.position += leg.Ankle.position - ankle + Vector3.up * pelvisDrop;
            var tilt = Quaternion.FromToRotation(Vector3.up, leg.Normal);
            leg.Foot.rotation = Quaternion.Slerp(Quaternion.identity, tilt, leg.Weight) * leg.Foot.rotation;
        }

        bool FindGround(Vector3 origin, float distance, out RaycastHit ground)
        {
            ground = default;
            float nearest = distance;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.distance >= nearest || hit.normal.y < .55f ||
                    hit.collider.GetComponentInParent<PlayerController>() != null ||
                    hit.collider.GetComponentInParent<SkateNetworkItem>() != null) continue;
                nearest = hit.distance;
                ground = hit;
            }
            return ground.collider != null;
        }
    }
}
