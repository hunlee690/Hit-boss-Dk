using UnityEngine;

namespace HitBoss.Multiplayer
{
    // Runs only on the host. Existing network transform/animator replicate the bot to guests.
    public sealed class NetworkBotBrain : MonoBehaviour
    {
        NetworkPlayer player;
        Rigidbody body;
        Vector3 home, target;
        float nextTarget;
        public void Initialize(NetworkPlayer value)
        {
            player = value; body = value.movement.GetComponent<Rigidbody>();
            home = body.position; ChooseTarget();
        }
        void ChooseTarget()
        {
            var offset = Random.insideUnitCircle * 10f;
            target = home + new Vector3(offset.x, 0, offset.y);
            nextTarget = Time.time + Random.Range(2f, 5f);
        }
        void FixedUpdate()
        {
            if (player == null || !player.IsSpawned || !player.IsServer || !player.IsBot.Value) return;
            var combat = player.GetComponent<HitBoss.Multiplayer.Skate.SkateCombat>();
            if (combat != null && combat.Active && !combat.CanAct)
            {
                if (!body.isKinematic) body.linearVelocity = Vector3.zero;
                return;
            }
            if (body.position.y < home.y - 15) { body.position = home; body.linearVelocity = Vector3.zero; }
            var direction = target - body.position; direction.y = 0;
            if (combat != null && combat.Active && combat.BotTarget(out var enemyPosition)) { target = enemyPosition; nextTarget = Time.time + 1; direction = target - body.position; direction.y = 0; }
            if (direction.sqrMagnitude < 1 || Time.time >= nextTarget) { ChooseTarget(); direction = target - body.position; direction.y = 0; }
            direction.Normalize();
            var ahead = body.position + direction * 1.3f;
            bool ground = Physics.Raycast(ahead + Vector3.up * 3, Vector3.down, 7, ~0, QueryTriggerInteraction.Ignore);
            bool wall = Physics.SphereCast(body.position + Vector3.up, .25f, direction, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != body;
            if (!ground || wall) { ChooseTarget(); direction = Vector3.zero; }
            var speed = player.Skating.Value ? 4f : 2.5f;
            body.linearVelocity = new Vector3(direction.x * speed, body.linearVelocity.y, direction.z * speed);
            if (direction.sqrMagnitude > .01f) body.MoveRotation(Quaternion.Slerp(body.rotation, Quaternion.LookRotation(direction), Time.fixedDeltaTime * 5));
            var animator = player.movement.animator;
            if (animator != null)
                foreach (var parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == "Blend") animator.SetFloat(parameter.nameHash, direction.magnitude);
        }
    }
}
