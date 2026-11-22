using System.Linq;
using HitBoss.Multiplayer.Skate;
using UnityEngine;

namespace HitBoss.Multiplayer
{
    // Host-owned race AI. Network transform/animator replicate it to guests.
    public sealed class NetworkBotBrain : MonoBehaviour
    {
        const float EnemyRefreshMin = .4f;
        const float EnemyRefreshMax = .8f;
        const float SeparationRadius = 5f;

        NetworkPlayer player;
        SkateCombat combat;
        SkateCombat enemy;
        Rigidbody body;
        SkateMatchRules raceRules;
        float catchupPace = 1f, nextCombatAttempt;
        float nextEnemyChoice;
        float raceLane, racePace;
        float raceSpeedBonus;
        float plannedLane, nextLanePlan, raceStuckTime, reverseUntil, recoveryCooldown;
        float bestRaceDistance = float.NegativeInfinity;
        readonly RaycastHit[] obstacleHits = new RaycastHit[24];
        readonly float[] laneChoices = { -8f, -4f, 0f, 4f, 8f };
        static readonly float[] groundProbeDistances = { 2f, 5f, 9f };

        public void Initialize(NetworkPlayer value)
        {
            player = value;
            combat = value.GetComponent<SkateCombat>();
            body = value.movement.GetComponent<Rigidbody>();
            raceRules = MatchConnection.Instance?.ActiveRules as SkateMatchRules;
            raceLane = laneChoices[(int)((value.NetworkObjectId + 4) % (ulong)laneChoices.Length)] + Random.Range(-.65f, .65f);
            racePace = Random.Range(.98f, 1.04f);
            plannedLane = raceLane;

            ChooseEnemy();
        }



        void ChooseEnemy()
        {
            if (combat == null)
            {
                enemy = null;
                return;
            }

            SkateCombat[] candidates = SkateCombat.Players
                .Where(other => other != null && other != combat && other.Active &&
                    other.CanAct && !other.IsProtected && other.Health.Value > 0 &&
                    (other.Position-body.position).sqrMagnitude < 144f &&
                    (combat.Item.Value == 2 || Vector3.Dot(transform.forward,(other.Position-body.position).normalized) > .2f))
                .OrderBy(other => other.Player.IsBot.Value ? 1 : 0)
                .ThenBy(other => (other.Position - body.position).sqrMagnitude)
                .ToArray();

            enemy = candidates.Length == 0 ? null : candidates[0];
            nextEnemyChoice = Time.time + Random.Range(EnemyRefreshMin, EnemyRefreshMax);
        }

        void FixedUpdate()
        {
            if (player == null || body == null || !player.IsSpawned || !player.IsServer || !player.IsBot.Value)
                return;

            if (combat != null && combat.Active && !combat.CanAct)
            {
                if (!body.isKinematic) body.linearVelocity = Vector3.zero;
                combat.SetBotBoost(false);
                raceStuckTime = 0;
                bestRaceDistance = float.NegativeInfinity;
                return;
            }

            if (!player.movement.RefreshSkateContact())
            {
                // Keep the ramp's launch velocity and gravity throughout flight.
                combat.SetBotBoost(false);
                raceStuckTime = 0f;
                Vector3 flying = body.linearVelocity; flying.y = 0f;
                if (!body.isKinematic && flying.sqrMagnitude > 1f)
                    body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(flying), 75f * Time.fixedDeltaTime));
                return;
            }

            if (MatchConnection.Instance?.ActiveRules is SkateMatchRules rules &&
                rules.TryBotRaceTarget(combat, raceLane, out var raceTarget))
            {
                DriveRace(rules, raceTarget);
                return;
            }

        }

        void DriveRace(SkateMatchRules rules, Vector3 raceTarget)
        {
            if (rules.TryGetRaceDistance(combat, out float distance) && distance > bestRaceDistance + 1.2f)
            {
                bestRaceDistance = distance;
                raceStuckTime = 0f;
            }
            else raceStuckTime += Time.fixedDeltaTime;

            if (raceStuckTime >= 2.5f && Time.time >= recoveryCooldown)
            {
                recoveryCooldown = Time.time + .8f;
                if (rules.RecoverStuckBot(combat))
                {
                    bestRaceDistance = float.NegativeInfinity;
                    raceStuckTime = 0;
                    reverseUntil = 0;
                    nextLanePlan = 0;
                    raceSpeedBonus = 0;
                    recoveryCooldown = Time.time + 2.5f;
                    return;
                }
            }
            if (raceStuckTime >= 1.25f && Time.time >= reverseUntil + .9f)
            {
                reverseUntil = Time.time + .55f;
                nextLanePlan = 0;
            }
            if (Time.time < reverseUntil)
            {
                combat.SetBotBoost(false);
                Vector3 backwards = -transform.forward;
                float clearance = RaceClearance(backwards, 3f);
                if (!body.isKinematic)
                    body.linearVelocity = player.movement.GroundFollowingVelocity(backwards, clearance > 1.5f ? 4f : 0f);
                player.movement.SetBotSteering(0f);
                return;
            }

            float lookAhead = Mathf.Clamp(body.linearVelocity.magnitude * .55f, 10f, 18f);
            if (Time.time >= nextLanePlan)
            {
                float best = float.NegativeInfinity;
                foreach (float lane in laneChoices)
                {
                    if (!rules.TryBotRaceTarget(combat, lane, out var target, lookAhead)) continue;
                    Vector3 direction = FlatDirection(target - body.position);
                    float clearance = RaceClearance(direction, lookAhead);
                    float score = Mathf.Min(clearance, lookAhead) * 2f
                        - Mathf.Abs(lane - plannedLane) * .22f - Mathf.Abs(lane - raceLane) * .25f;
                    // Check beyond the lane change as well so the bot keeps space to pass the obstacle.
                    if (rules.TryBotRaceTarget(combat, lane, out var next, lookAhead + 5f))
                        score -= Mathf.Max(0f, 4f - ClearanceBetween(target, next)) * 3f;
                    score -= MineRisk(body.position, target) + TrafficRisk(body.position, target);
                    if (score <= best) continue;
                    best = score;
                    plannedLane = lane;
                }
                nextLanePlan = Time.time + .3f;
            }

            rules.TryBotRaceTarget(combat, plannedLane, out raceTarget, lookAhead);
            Vector3 desired = FlatDirection(raceTarget - body.position);
            Vector3 steering = (desired + SeparationDirection() * .45f).normalized;
            float freeSpace = RaceClearance(steering, lookAhead);
            combat.SetBotBoost(freeSpace > 12f && Vector3.Angle(transform.forward, steering) < 25f &&
                combat.RacePlace.Value > 1 && MineRisk(body.position, raceTarget) < 1f);
            float slide = combat.SlideSpeed;
            float bonus = combat.BoostSpeed + slide + combat.DraftSpeed + combat.PackSpeed;
            raceSpeedBonus = Mathf.MoveTowards(raceSpeedBonus, bonus, (slide > 0 ? 45f : bonus < raceSpeedBonus ? 24f : 17f) * Time.fixedDeltaTime);
            catchupPace = Mathf.MoveTowards(catchupPace, rules.BotRacePace(combat), .1f * Time.fixedDeltaTime);
            float speed = (player.movement.skateForwardSpeed * racePace * catchupPace + raceSpeedBonus) *
                Mathf.Clamp01((freeSpace - .7f) / 7f);
            speed = FollowingSpeed(steering, speed);
            if (freeSpace < 3f) nextLanePlan = 0;
            if (freeSpace > 5f)
            {
                if (Time.time >= nextEnemyChoice || enemy == null || !enemy.CanAct) ChooseEnemy();
                if (enemy != null && Time.time >= nextCombatAttempt)
                {
                    // Keep racing instead of spending every cooldown brawling with other bots.
                    bool attack = !enemy.Player.IsBot.Value || Random.value < .2f;
                    if (attack) combat.TryBotAttack(enemy);
                    nextCombatAttempt = Time.time + (enemy.Player.IsBot.Value ? Random.Range(2.2f, 3.2f) : Random.Range(.9f, 1.3f));
                }
            }
            Drive(steering, speed);
        }

        float FollowingSpeed(Vector3 direction, float desiredSpeed)
        {
            foreach (var other in SkateCombat.Players)
            {
                if (other == null || other == combat || !other.CanAct) continue;
                Vector3 delta = other.Position - body.position; delta.y = 0f;
                float ahead = Vector3.Dot(delta, direction);
                if (ahead <= 0f || ahead > 7f || (delta - direction * ahead).sqrMagnitude > 2.25f) continue;
                float otherSpeed = Mathf.Max(0f, Vector3.Dot(other.ThrowMomentum, direction));
                float gap = 2.5f + desiredSpeed * .1f;
                desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Max(0f, otherSpeed + (ahead - gap) * 4f));
                nextLanePlan = 0f;
            }
            return desiredSpeed;
        }
        float TrafficRisk(Vector3 start, Vector3 end)
        {
            Vector3 segment=end-start;segment.y=0;float length=segment.magnitude;
            if(length<.1f)return 0;
            Vector3 direction=segment/length;float risk=0;
            foreach(var other in SkateCombat.Players)
            {
                if(other==null||other==combat||!other.CanAct)continue;
                Vector3 delta=other.Position-start;delta.y=0;
                float ahead=Vector3.Dot(delta,direction);
                if(ahead<0||ahead>length)continue;
                float lateral=(delta-direction*ahead).magnitude;
                if(lateral<3f)risk+=(1f-lateral/3f)*(1f-ahead/length)*14f;
            }
            return risk;
        }
        static float MineRisk(Vector3 start, Vector3 end)
        {
            float risk=0; Vector3 segment=end-start; segment.y=0;
            foreach(var item in SkateNetworkItem.ActiveItems)
            {
                if(item==null || !item.ArmedMine)continue;
                Vector3 offset=item.transform.position-start; offset.y=0;
                float t=Mathf.Clamp01(Vector3.Dot(offset,segment)/Mathf.Max(.01f,segment.sqrMagnitude));
                float distance=(offset-segment*t).magnitude;
                if(distance<3.2f)risk+=(3.2f-distance)*8f;
            }
            return risk;
        }
        float RaceClearance(Vector3 direction, float distance)
        {
            if (direction.sqrMagnitude < .01f) return 0;
            float nearest = ObstacleDistance(body.position, direction, distance);
            foreach (float step in groundProbeDistances)
            {
                if (step > distance || step > nearest) break;
                Vector3 probe = body.position + direction * step;
                bool ground;
                if (raceRules != null)
                    ground = raceRules.TryTrackSupport(probe, out var support) && support.normal.y >= .35f;
                else
                {
                    int count = Physics.RaycastNonAlloc(probe + Vector3.up * 3f, Vector3.down, obstacleHits, 8f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    ground = false;
                    for (int i = 0; i < count; i++)
                    {
                        var hit = obstacleHits[i];
                        if (hit.normal.y >= .35f && hit.collider.GetComponentInParent<NetworkPlayer>() == null &&
                            hit.collider.GetComponentInParent<SkateNetworkItem>() == null) ground = true;
                    }
                }
                bool launchingFromRamp = player.movement.HasGroundContact && body.linearVelocity.y > 1.5f &&
                    player.movement.SkateGroundNormal.y < .99f;
                if (!ground && !launchingFromRamp) return Mathf.Min(nearest, step - .5f);
            }
            return nearest;
        }

        float ClearanceBetween(Vector3 start, Vector3 end)
        {
            Vector3 direction = end - start;
            direction.y = 0;
            return ObstacleDistance(start + Vector3.up * .96f, direction.normalized, direction.magnitude);
        }

        float ObstacleDistance(Vector3 position, Vector3 direction, float distance)
        {
            float nearest = distance;
            for (int height = 0; height < 2; height++)
            {
                int count = Physics.SphereCastNonAlloc(position + Vector3.up * (height == 0 ? -.3f : .35f),
                    .4f, direction, obstacleHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = obstacleHits[i];
                    if (hit.normal.y > .5f || raceRules != null && raceRules.IsDrivableBank(hit) || hit.collider.GetComponentInParent<NetworkPlayer>() != null ||
                        hit.collider.GetComponentInParent<SkateNetworkItem>() != null) continue;
                    nearest = Mathf.Min(nearest, hit.distance);
                }
            }
            return nearest;
        }

        void Drive(Vector3 direction, float speed)
        {
            if (body.isKinematic || direction.sqrMagnitude < .01f) return;
            float angle=Vector3.SignedAngle(transform.forward,direction,Vector3.up);
            // Restore the earlier direct racing response while retaining obstacle and spacing checks.
            body.linearVelocity=player.movement.GroundFollowingVelocity(direction,speed);
            body.MoveRotation(Quaternion.Slerp(body.rotation,Quaternion.LookRotation(direction),Time.fixedDeltaTime*8f));
            player.movement.SetBotSteering(Mathf.Clamp(angle/45f,-1f,1f));

            Animator animator = player.movement.animator;
            if (animator != null)
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == "Blend")
                        animator.SetFloat(parameter.nameHash, 1f);
        }

        Vector3 SeparationDirection()
        {
            Vector3 separation = Vector3.zero;
            foreach (SkateCombat other in SkateCombat.Players)
            {
                if (other == null || other == combat || !other.Active || other.Downed.Value) continue;
                Vector3 away = body.position - other.Position;
                away.y = 0f;
                float distance = away.magnitude;
                if (distance < .01f || distance >= SeparationRadius) continue;
                separation += away.normalized * (1f - distance / SeparationRadius);
            }
            return Vector3.ClampMagnitude(separation, 1f);
        }



        static Vector3 FlatDirection(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude > .01f ? value.normalized : Vector3.zero;
        }


    }
}
