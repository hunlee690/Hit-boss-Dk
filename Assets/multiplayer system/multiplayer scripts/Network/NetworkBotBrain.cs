using System.Linq;
using HitBoss.Multiplayer.Skate;
using UnityEngine;

namespace HitBoss.Multiplayer
{
    // Host-owned roaming combat AI. Network transform/animator replicate it to guests.
    public sealed class NetworkBotBrain : MonoBehaviour
    {
        const float RoamRefreshMin = 2.5f;
        const float RoamRefreshMax = 5.5f;
        const float EnemyRefreshMin = 3f;
        const float EnemyRefreshMax = 6f;
        const float SeparationRadius = 4f;

        NetworkPlayer player;
        SkateCombat combat;
        SkateCombat enemy;
        Rigidbody body;
        Transform[] roamAnchors;
        Vector3 home;
        Vector3 roamTarget;
        Vector3 lastPosition;
        float nextRoamTarget;
        float nextEnemyChoice;
        float stuckAt;
        float orbitDirection;
        float avoidUntil;
        Vector3 lockedAvoidDirection;

        public void Initialize(NetworkPlayer value)
        {
            player = value;
            combat = value.GetComponent<SkateCombat>();
            body = value.movement.GetComponent<Rigidbody>();
            home = body.position;
            lastPosition = home;
            stuckAt = Time.time + 1.25f;
            orbitDirection = Random.value < .5f ? -1f : 1f;

            MapArenaSetup arena = FindObjectsByType<MapArenaSetup>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(setup => setup.gameObject.activeInHierarchy);
            roamAnchors = arena == null || arena.playerSpawnPoints == null
                ? System.Array.Empty<Transform>()
                : arena.playerSpawnPoints.Where(point => point != null).ToArray();

            ChooseRoamTarget();
            ChooseEnemy();
        }

        void ChooseRoamTarget()
        {
            Vector3 center = roamAnchors != null && roamAnchors.Length > 0
                ? roamAnchors[Random.Range(0, roamAnchors.Length)].position
                : home;
            Vector2 offset = Random.insideUnitCircle * Random.Range(2.5f, 7f);
            roamTarget = center + new Vector3(offset.x, 0f, offset.y);
            nextRoamTarget = Time.time + Random.Range(RoamRefreshMin, RoamRefreshMax);
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
                    other.CanAct && other.Health.Value > 0)
                .OrderBy(other => (other.Position - body.position).sqrMagnitude)
                .Take(5)
                .OrderBy(_ => Random.value)
                .ToArray();

            enemy = candidates.Length == 0 ? null : candidates[0];
            nextEnemyChoice = Time.time + Random.Range(EnemyRefreshMin, EnemyRefreshMax);
            orbitDirection = Random.value < .5f ? -1f : 1f;
        }

        void FixedUpdate()
        {
            if (player == null || body == null || !player.IsSpawned || !player.IsServer || !player.IsBot.Value)
                return;

            if (combat != null && combat.Active && !combat.CanAct)
            {
                if (!body.isKinematic) body.linearVelocity = Vector3.zero;
                return;
            }

            if (body.position.y < home.y - 15f)
            {
                body.position = home;
                body.linearVelocity = Vector3.zero;
                ChooseRoamTarget();
            }

            if (Time.time >= nextRoamTarget || HorizontalDistance(body.position, roamTarget) < 1.2f)
                ChooseRoamTarget();

            if (Time.time >= nextEnemyChoice || enemy == null || !enemy.Active || !enemy.CanAct)
                ChooseEnemy();

            Vector3 separation = SeparationDirection();
            Vector3 roamDirection = FlatDirection(roamTarget - body.position);
            Vector3 direction = roamDirection;

            if (combat != null && enemy != null)
            {
                Vector3 enemyDelta = enemy.Position - body.position;
                enemyDelta.y = 0f;
                float enemyDistance = enemyDelta.magnitude;
                Vector3 towardEnemy = enemyDistance > .01f ? enemyDelta / enemyDistance : transform.forward;

                if (!combat.BotCanMelee)
                {
                    // Recharging bots keep moving and create space instead of
                    // freezing beside their opponent.
                    Vector3 retreat = enemyDistance < 10f ? -towardEnemy : roamDirection;
                    direction = retreat * 1.25f + roamDirection * .65f + separation * 2.2f;
                }
                else if (enemyDistance <= 2.6f)
                {
                    // Circle the opponent while attacking so bots never form a
                    // stationary pile in one spot.
                    Vector3 orbit = Vector3.Cross(Vector3.up, towardEnemy) * orbitDirection;
                    direction = orbit * 1.2f + towardEnemy * .2f + separation * 2f;
                    combat.TryBotAttack(enemy);
                }
                else if (enemyDistance < 18f)
                {
                    direction = towardEnemy + separation * 1.7f;
                    combat.TryBotAttack(enemy);
                }
                else
                {
                    direction = roamDirection + separation * 1.8f;
                }
            }
            else
            {
                direction = roamDirection + separation * 1.8f;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude < .04f)
                direction = Quaternion.Euler(0f, orbitDirection * 70f, 0f) * transform.forward;
            direction.Normalize();
            direction = AvoidEdgesAndWalls(direction);

            if ((body.position - lastPosition).sqrMagnitude > .16f)
            {
                lastPosition = body.position;
                stuckAt = Time.time + 1.25f;
            }
            else if (Time.time >= stuckAt)
            {
                ChooseRoamTarget();
                orbitDirection *= -1f;
                lockedAvoidDirection = Quaternion.Euler(0f, orbitDirection * 115f, 0f) * transform.forward;
                lockedAvoidDirection.y = 0f;
                lockedAvoidDirection.Normalize();
                avoidUntil = Time.time + 1.1f;
                direction = lockedAvoidDirection;
                stuckAt = Time.time + 1.25f;
            }

            float speed = player.Skating.Value ? 10.5f : 3.5f;
            body.linearVelocity = new Vector3(direction.x * speed, body.linearVelocity.y, direction.z * speed);
            if (direction.sqrMagnitude > .01f)
                body.MoveRotation(Quaternion.Slerp(
                    body.rotation,
                    Quaternion.LookRotation(direction),
                    Time.fixedDeltaTime * 6f));

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
            return separation.sqrMagnitude > .01f ? separation.normalized : Vector3.zero;
        }

        Vector3 AvoidEdgesAndWalls(Vector3 direction)
        {
            // Keep an avoidance turn for a short time. Choosing a new left/right
            // direction every physics frame caused bots to shake in corners.
            if (Time.time < avoidUntil && lockedAvoidDirection.sqrMagnitude > .01f)
                return lockedAvoidDirection;

            if (DirectionIsClear(direction)) return direction;

            Vector3 left = Quaternion.Euler(0f, -80f, 0f) * direction;
            Vector3 right = Quaternion.Euler(0f, 80f, 0f) * direction;
            bool leftClear = DirectionIsClear(left);
            bool rightClear = DirectionIsClear(right);

            if (leftClear && rightClear)
                lockedAvoidDirection = orbitDirection < 0f ? left : right;
            else if (leftClear)
                lockedAvoidDirection = left;
            else if (rightClear)
                lockedAvoidDirection = right;
            else
                lockedAvoidDirection = -direction;

            lockedAvoidDirection.y = 0f;
            lockedAvoidDirection.Normalize();
            orbitDirection = Vector3.Dot(lockedAvoidDirection, left) > Vector3.Dot(lockedAvoidDirection, right) ? -1f : 1f;
            avoidUntil = Time.time + Random.Range(.85f, 1.35f);
            ChooseRoamTarget();
            return lockedAvoidDirection;
        }

        bool DirectionIsClear(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < .01f) return false;
            direction.Normalize();

            Vector3 ahead = body.position + direction * 1.7f;
            bool hasGround = Physics.Raycast(
                ahead + Vector3.up * 3f,
                Vector3.down,
                7f,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (!hasGround) return false;

            if (!Physics.SphereCast(
                    body.position + Vector3.up,
                    .28f,
                    direction,
                    out RaycastHit hit,
                    1.2f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
                return true;

            // Nearby players are handled by separation steering. Treating them
            // as walls made two bots rapidly reverse against each other.
            return hit.rigidbody == body || hit.collider.GetComponentInParent<NetworkPlayer>() != null;
        }

        static Vector3 FlatDirection(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude > .01f ? value.normalized : Vector3.zero;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
