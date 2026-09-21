using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    public sealed class SkateMatchRules : NetworkModeRules
    {
        public SkateNetworkItem staminaPickup, bombPickup, minePickup, bomb, mine;
        public float matchDuration = 180, respawnDelay = 2;
        public bool Running { get; private set; }
        Transform[] spawnPoints, pickupPoints;
        MatchHUD hud;
        double endTime;
        float nextPickup, nextHud;
        void Awake()
        {
            var legacy = FindFirstObjectByType<SkateArena>();
            spawnPoints = legacy != null ? legacy.spawnPoints : null;
            if (legacy != null) { matchDuration = legacy.matchDuration; respawnDelay = legacy.respawnDelay; }
            pickupPoints = legacy != null ? legacy.pickupPoints : null;
            hud = FindFirstObjectByType<MatchHUD>();
            if (hud != null) { hud.enabled = false; LayoutHud(); if (hud.endPanel != null) hud.endPanel.SetActive(false); }
        }
        void LayoutHud()
        {
            var canvas = hud.GetComponentInParent<Canvas>() ?? hud.healthText?.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            LayoutText(hud.healthText, canvas.transform, new Vector2(0, .93f), new Vector2(.4f, 1), 18);
            LayoutText(hud.staminaText, canvas.transform, new Vector2(0, .86f), new Vector2(.4f, .93f), 18);
            LayoutText(hud.timerText, canvas.transform, new Vector2(.4f, .93f), new Vector2(.6f, 1), 22);
            LayoutText(hud.throwableText, canvas.transform, new Vector2(.62f, .9f), Vector2.one, 18);
            LayoutText(hud.rankingText, canvas.transform, new Vector2(0, .55f), new Vector2(.55f, .85f), 18);
            if (hud.endPanel == null) return;
            var group = hud.endPanel.GetComponent<CanvasGroup>();
            if (group == null) group = hud.endPanel.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            foreach (var graphic in hud.endPanel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
            var panel = hud.endPanel.GetComponent<RectTransform>();
            panel.SetParent(canvas.transform, false); panel.anchorMin = new Vector2(.08f, .18f); panel.anchorMax = new Vector2(.92f, .82f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            LayoutText(hud.resultText, panel, Vector2.zero, Vector2.one, 24);
            if (hud.resultText != null) hud.resultText.alignment = TMPro.TextAlignmentOptions.Center;
        }
        static void LayoutText(TMPro.TMP_Text label, Transform parent, Vector2 min, Vector2 max, float size)
        {
            if (label == null) return;
            var rect = label.rectTransform; rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = new Vector2(12, 4); rect.offsetMax = new Vector2(-12, -4);
            var canvas = label.GetComponentInParent<Canvas>();
            float readableScale = canvas == null ? 1 : Mathf.Max(1, 1 / Mathf.Max(.1f, canvas.rootCanvas.scaleFactor));
            label.enableAutoSizing = true; label.fontSizeMin = 10 * readableScale; label.fontSizeMax = size * readableScale;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap; label.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        }
        public override Pose SpawnPose(int index)
        {
            if (spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[index % spawnPoints.Length] != null)
            { var point = spawnPoints[index % spawnPoints.Length]; return new Pose(point.position, point.rotation); }
            return new Pose(new Vector3(index % 4 * 2, 2, index / 4 * 2), Quaternion.identity);
        }
        public override void ConfigurePlayer(NetworkPlayer player, MultiplayerMode mode) { player.movement.startInSkateMode = true; }
        public override void BeginServerMatch()
        {
            Running = true; endTime = NetworkManager.Singleton.ServerTime.Time + matchDuration;
            foreach (var player in SkateCombat.Players) player.EndsAt.Value = endTime;
            for (int i = 0; i < 4; i++) SpawnPickup();
            nextPickup = Time.time + 5;
        }
        public override void EndServerMatch()
        {
            Running = false;
            foreach (var player in SkateCombat.Players) if (player.IsSpawned) player.Finished.Value = true;
        }
        void Update()
        {
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.IsServer && Running)
            {
                if (NetworkManager.Singleton.ServerTime.Time >= endTime) EndServerMatch();
                else if (Time.time >= nextPickup) { SpawnPickup(); nextPickup = Time.time + 5; }
            }
            if (Time.unscaledTime < nextHud) return;
            nextHud = Time.unscaledTime + .1f; DrawHud();
        }
        void SpawnPickup()
        {
            if (pickupPoints == null || pickupPoints.Length == 0) return;
            var existing = FindObjectsByType<SkateNetworkItem>(FindObjectsSortMode.None).Where(p => p.kind <= SkateItemKind.MinePickup).ToArray();
            if (existing.Length >= 5) return;
            var free = pickupPoints.Where(p => p != null && existing.All(x => Vector3.Distance(x.transform.position, p.position) > 1)).ToArray();
            if (free.Length == 0) return;
            var options = new[] { staminaPickup, staminaPickup, bombPickup, minePickup };
            var prefab = options[Random.Range(0, options.Length)]; if (prefab == null) return;
            var spot = free[Random.Range(0, free.Length)];
            Instantiate(prefab, spot.position, spot.rotation).NetworkObject.Spawn(true);
        }
        public void SpawnProjectile(SkateCombat owner, bool isBomb)
        {
            if (!NetworkManager.Singleton.IsServer || !Running) return;
            var inventory = owner.Player.movement.GetComponent<SkatePlayerSettings>();
            var forward = owner.Player.movement.transform.forward; forward.y = 0; forward.Normalize();
            var position = isBomb ? (inventory.throwPoint != null ? inventory.throwPoint.position : owner.Position + Vector3.up) : owner.Position - forward * inventory.minePlaceDistance;
            if (!isBomb && Physics.Raycast(position + Vector3.up * 2, Vector3.down, out var hit, 6, inventory.groundLayers, QueryTriggerInteraction.Ignore)) position = hit.point + Vector3.up * .05f;
            var item = Instantiate(isBomb ? bomb : mine, position, Quaternion.LookRotation(forward));
            item.SetAttacker(owner); item.NetworkObject.Spawn(true);
            if (isBomb) item.GetComponent<Rigidbody>().linearVelocity = forward * inventory.bombForwardSpeed + Vector3.up * inventory.bombUpwardSpeed;
        }
        void DrawHud()
        {
            var local = SkateCombat.Players.FirstOrDefault(p => p.IsOwner && !p.Player.IsBot.Value);
            if (local == null || hud == null) return;
            var sorted = SkateCombat.Players.OrderByDescending(p => p.Kills.Value).ThenBy(p => p.Deaths.Value).ThenBy(p => p.NetworkObjectId).ToArray();
            var text = new StringBuilder();
            for (int i = 0; i < sorted.Length; i++) text.AppendLine((i + 1) + ". " + sorted[i].Player.Username.Value + "   " + sorted[i].Kills.Value + " K / " + sorted[i].Deaths.Value + " D");
            if (hud.rankingText != null) { hud.rankingText.richText = false; hud.rankingText.text = text.ToString(); }
            if (hud.healthText != null) hud.healthText.text = "HEALTH: " + local.Health.Value + "/" + local.MaxHealth;
            if (hud.staminaText != null) hud.staminaText.text = "STAMINA: " + local.Stamina.Value + "/" + local.MaxStamina;
            if (hud.throwableText != null) hud.throwableText.text = local.ItemCount.Value == 0 ? "ITEM: EMPTY" : (local.Item.Value == 1 ? "BOMB x" : "MINE x") + local.ItemCount.Value;
            int remaining = Mathf.Max(0, Mathf.CeilToInt((float)(local.EndsAt.Value - NetworkManager.Singleton.ServerTime.Time)));
            if (hud.timerText != null) hud.timerText.text = (remaining / 60).ToString("00") + ":" + (remaining % 60).ToString("00");
            if (local.Finished.Value)
            {
                if (hud.endPanel != null) hud.endPanel.SetActive(!local.Player.IsPaused);
                bool tie = sorted.Length > 1 && sorted[0].Kills.Value == sorted[1].Kills.Value && sorted[0].Deaths.Value == sorted[1].Deaths.Value;
                if (hud.resultText != null) { hud.resultText.richText = false; hud.resultText.text = (tie ? "DRAW" : sorted[0].Player.Username.Value + " WINS!") + "\n" + text + "\nEsc: leave match"; }
            }
        }
    }
}
