using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HitBoss.Multiplayer.Skate
{
    public sealed class SkateMatchRules : NetworkModeRules
    {
        const int CountdownSeconds = 3;
        const double GoMessageSeconds = 1;

        [UnityEngine.Serialization.FormerlySerializedAs("bombPickup")]
        public SkateNetworkItem itemBox;
        public SkateNetworkItem bomb, mine;
        [Header("Race visuals (also preserve shaders in builds)")]
        public Material markerMaterial, effectsMaterial, trailMaterial;
        public RaceUITheme uiTheme;
        public AudioClip pickupSound;
        public float respawnDelay = .75f;
        [Min(1f)] public float baseSkateSpeed = 23f;
        [Min(60f)] public float raceTimeoutSeconds = 240f;
        [Range(0f, 2.5f)] public float maximumPackAssist = 1.2f;
        [Range(1f, 1.2f)] public float maximumBotCatchupPace = 1.08f;
        [Min(3f)] public float resultsDisplaySeconds = 10f;
        [Range(3, 30)] public int maximumItemBoxes = 12;
        public bool useScenePickupPoints = true;
        [Min(2f)] public float itemBoxRespawnSeconds = 8f;
        [Min(.2f)] public float itemBoxRefillSeconds = .5f;
        public bool Running { get; private set; }
        Transform[] spawnPoints, pickupPoints;
        sealed class BoxSlot { public SkateNetworkItem Item; public double ReadyAt; }
        BoxSlot[] boxSlots;
        double raceDeadline;
        SkateRaceFeedback feedback;
        readonly Dictionary<ulong, SkateRaceResult> departed = new Dictionary<ulong, SkateRaceResult>();
        public int RequiredLaps => LapsToFinish;
        public float ExpectedRaceSeconds => track == null ? 0f : track.Length * LapsToFinish / Mathf.Max(1f, baseSkateSpeed);
        MatchHUD hud;
        SkateRaceTrack track;
        MapArenaSetup raceArena;
        PauseMenu pauseMenu;
        SkateCombat watching;
        double startTime, nextStandings;
        int finishCount;
        bool resultsClosed;
        bool returningToMenu;
        float returnToMenuAt = -1f;
        int LapsToFinish => raceArena == null ? 3 : Mathf.Max(1, raceArena.lapsToFinish);
        float raceTimerFontSize;
        RectTransform boostFill;
        RectTransform boostMeter;
        TMPro.TMP_Text boostLabel;
        TMPro.TMP_Text killNotice;
        float killNoticeUntil;
        readonly List<SkateBoostPad> boostPads = new List<SkateBoostPad>();
        readonly Dictionary<SkateCombat, Racer> racers = new Dictionary<SkateCombat, Racer>();
        Collider raceSurface;
        readonly Collider[] recoveryBlockers = new Collider[32];
        readonly RaycastHit[] recoverySweepHits = new RaycastHit[32];
        static readonly float[] recoveryOffsets = { -4f, -9f, -16f, -26f, -40f };
        static readonly float[] recoveryLanes = { 0f, -4f, 4f, -8f, 8f };
        static readonly float[] recoveryPathSamples = { 0f, 2.5f, 5.5f, 9f };
        sealed class Racer
        {
            public float Distance;
            public float SafeDistance;
            public Pose Safe;
            public double CrossedAt = double.PositiveInfinity;
        }
        float nextPickup, nextHud;
        void Awake()
        {
            SkateCombatEffects.ConfigureMaterials(effectsMaterial, trailMaterial,pickupSound);
            var arena = FindObjectsByType<MapArenaSetup>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(setup => setup.gameObject.activeInHierarchy);
            raceArena = arena;
            if (arena != null && arena.raceRoute != null && arena.raceRoute.Count(p => p != null) >= 4)
                track = new SkateRaceTrack(arena);

            raceSurface = arena != null && arena.raceSurface != null ? arena.raceSurface : FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c.name == "Banked track | Flattened skating lane");
            pauseMenu = FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            spawnPoints = arena != null ? arena.playerSpawnPoints : null;
            pickupPoints = arena != null ? arena.pickupSpawnPoints : null;
            if (track != null) { AddItemBoxPoints(); PlaceBoostPads(); }
            boxSlots = pickupPoints == null ? System.Array.Empty<BoxSlot>() : pickupPoints.Select(_ => new BoxSlot()).ToArray();
            hud = FindFirstObjectByType<MatchHUD>();
            if (hud != null) { hud.enabled = false; LayoutHud(); if (hud.endPanel != null) hud.endPanel.SetActive(false); }
        }
        void AddItemBoxPoints()
        {
            if(useScenePickupPoints && pickupPoints!=null && pickupPoints.Any(p=>p!=null))
            {
                pickupPoints=pickupPoints.Where(p=>p!=null).Take(maximumItemBoxes).ToArray();
                return;
            }
            var points = new List<Transform>();
            int stations = Mathf.Max(1, Mathf.CeilToInt(maximumItemBoxes / 2f));
            for (int station = 0; station < stations; station++)
            {
                float distance = 65f + track.Length * station / stations;
                Vector3 center = track.PointAt(distance);
                Vector3 forward = track.PointAt(distance + 2f) - track.PointAt(distance - 2f);
                forward.y = 0; forward.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                foreach (float lane in new[] { -6f, 6f })
                {
                    if (points.Count >= maximumItemBoxes) break;
                    if (!RecoveryGround(center + right * lane, out var ground) || ground.normal.y < .7f) continue;
                    Vector3 position = ground.point + Vector3.up * .45f;
                    if (points.Any(point => Vector3.Distance(point.position, position) < 3.5f)) continue;
                    var blockers = Physics.OverlapBox(ground.point + Vector3.up * 1.2f, new Vector3(.85f, .8f, .85f),
                        Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (blockers.Any(c => c.GetComponentInParent<NetworkPlayer>() == null &&
                        c.GetComponentInParent<SkateNetworkItem>() == null)) continue;
                    var point = new GameObject("Item Box Point " + (points.Count + 1)).transform;
                    point.SetParent(transform, false);
                    point.SetPositionAndRotation(position, Quaternion.identity);
                    points.Add(point);
                }
            }
            pickupPoints = points.ToArray();
        }
        void PlaceBoostPads()
        {
            // Side lanes reward a deliberate line choice; the shortest line misses each pad.
            float[] locations = { .027f, .107f, .217f, .596f, .701f, .775f, .335f, .4f, .46f, .83f, .89f, .95f };
            var surface = raceSurface as MeshCollider ?? FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c.name == "Banked track | Flattened skating lane");
            Physics.SyncTransforms();
            for (int i = 0; i < locations.Length; i++)
            {
                float distance = track.Length * locations[i];
                Vector3 center = track.PointAt(distance);
                Vector3 tangent = track.PointAt(distance + 3f) - track.PointAt(distance - 3f);
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                float preferredLane = i % 2 == 0 ? 4.5f : -9f;
                foreach (float lane in new[] { preferredLane, -preferredLane, preferredLane * .5f, -preferredLane * .5f })
                {
                    Vector3 point = center + right * lane;
                    if (pickupPoints != null && pickupPoints.Any(p => p != null && Vector3.Distance(p.position, point) < 7f)) continue;
                    RaycastHit ground;
                    if (surface != null)
                    {
                        if (!surface.Raycast(new Ray(point + Vector3.up * 20f, Vector3.down), out ground, 50f)) continue;
                    }
                    else if (!FindSurface(point + Vector3.up * 2f, out ground)) continue;
                    Vector3 forward = Vector3.ProjectOnPlane(tangent, ground.normal);
                    if (forward.sqrMagnitude < .01f) continue;
                    var pad = new SkateBoostPad(transform, ground.point, Quaternion.LookRotation(forward, ground.normal), surface);
                    if (!pad.FitsSurface) { pad.Dispose(); continue; }
                    boostPads.Add(pad);
                    break;
                }
            }
        }
        void LayoutHud()
        {
            feedback=FindFirstObjectByType<SkateRaceFeedback>(FindObjectsInactive.Include);
            if(feedback==null || !feedback.SceneReady)
            {
                Debug.LogError("The race scene is missing its editable Race HUD references.");
                return;
            }
            feedback.Bind(hud,this);
            boostMeter=feedback.boostMeter;boostFill=feedback.boostFill;
            boostLabel=feedback.boostLabel;killNotice=feedback.killNotice;
            if(hud.timerText!=null)raceTimerFontSize=hud.timerText.fontSizeMax;
            if(feedback.leaveMatchButton.onClick.GetPersistentEventCount()==0)
                feedback.leaveMatchButton.onClick.AddListener(LeaveMatch);
        }
        void LeaveMatch()
        {
            if(pauseMenu!=null)pauseMenu.QuitMatch();
            else if(RoomManager.Instance!=null)_=RoomManager.Instance.LeaveAsync();
        }
#if UNITY_EDITOR
        // Run once in the editor. Playing a match only binds the saved scene objects.
        public void BuildEditableHud(MatchHUD sceneHud)
        {
            hud=sceneHud;hud.enabled=false;
            if(FindFirstObjectByType<SkateRaceFeedback>(FindObjectsInactive.Include)!=null)
                throw new System.InvalidOperationException("The scene already has an editable Race HUD.");
            pauseMenu=FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (hud.timerText != null) raceTimerFontSize = hud.timerText.fontSizeMax;
            var parentCanvas = hud.GetComponentInParent<Canvas>() ?? hud.healthText?.GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;
            var canvas=new GameObject("Race HUD",typeof(RectTransform)).transform;
            canvas.SetParent(parentCanvas.transform,false);
            var canvasRect=(RectTransform)canvas;canvasRect.anchorMin=Vector2.zero;canvasRect.anchorMax=Vector2.one;canvasRect.offsetMin=canvasRect.offsetMax=Vector2.zero;
            LayoutText(hud.healthText, canvas.transform, uiTheme != null ? new Vector2(.01f, .855f) : new Vector2(0, .93f), uiTheme != null ? new Vector2(.18f, .88f) : new Vector2(.4f, 1), 18);
            LayoutText(hud.timerText, canvas.transform, new Vector2(.33f, .83f), new Vector2(.70f, 1), 22);
            if (hud.timerText != null) raceTimerFontSize = hud.timerText.fontSizeMax;
            LayoutText(hud.throwableText, canvas.transform, uiTheme != null ? new Vector2(.78f,.79f) : new Vector2(.62f, .9f), uiTheme != null ? new Vector2(.99f,.89f) : Vector2.one, 18);
            LayoutText(hud.rankingText, canvas.transform, new Vector2(.015f, .36f), new Vector2(.30f, .85f), 14);
            if (hud.rankingText != null) hud.rankingText.alignment = TMPro.TextAlignmentOptions.TopLeft;
            BuildBoostMeter(canvas.transform);
            if (hud.healthText != null)
            {
                killNotice = Instantiate(hud.healthText, canvas.transform);
                killNotice.name = "KillCoinNotice";
                killNotice.raycastTarget = false;
                killNotice.alignment = TMPro.TextAlignmentOptions.Center;
                killNotice.color = new Color(1f, .85f, .22f);
                killNotice.richText = false;
                LayoutText(killNotice, canvas.transform, new Vector2(.28f, .64f), new Vector2(.72f, .72f), 23);
                killNotice.gameObject.SetActive(false);
            }
            feedback = canvas.gameObject.AddComponent<SkateRaceFeedback>();
            feedback.Initialize(hud, this, canvas.transform);
            feedback.boostMeter=boostMeter;feedback.boostFill=boostFill;
            feedback.boostLabel=boostLabel;feedback.killNotice=killNotice;
            if (hud.endPanel == null) return;
            var panel = hud.endPanel.GetComponent<RectTransform>();
            panel.SetParent(canvas.transform, false); panel.anchorMin = new Vector2(.06f, .025f); panel.anchorMax = new Vector2(.94f, .90f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            LayoutText(hud.resultText, panel, Vector2.zero, Vector2.one, 24);
            if (hud.resultText != null) hud.resultText.alignment = TMPro.TextAlignmentOptions.Center;
            if (hud.resultText != null)
            {
                hud.resultText.rectTransform.anchorMin = new Vector2(0f, .19f);
                hud.resultText.raycastTarget = false;
                feedback.leaveMatchButton=MakeResultButton(panel,"Leave match",.5f,LeaveMatch);
            }
            if(hud.endPanel!=null)hud.endPanel.SetActive(false);
        }
        UnityEngine.UI.Button MakeResultButton(Transform parent, string title, float x, UnityEngine.Events.UnityAction click)
        {
            var root = new GameObject(title, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x - .18f, .035f);
            rect.anchorMax = new Vector2(x + .18f, .145f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = root.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(.08f, .19f, .28f, 1f);
            if (uiTheme != null) RaceHUDArtwork.Skin(rect, uiTheme.redButton);
            var button = root.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(click);
            var label = Instantiate(hud.resultText, rect);
            label.name = "Label";
            label.text = title;
            label.raycastTarget = false;
            LayoutText(label, rect, Vector2.zero, Vector2.one, 18);
            if (uiTheme != null)
            {
                label.text = "";
                RaceHUDArtwork.Picture("Exit icon", rect, uiTheme.exitIcon, new Vector2(.08f,.20f), new Vector2(.23f,.80f));
                RaceHUDArtwork.Picture("Leave", rect, uiTheme.leave, new Vector2(.27f,.23f), new Vector2(.53f,.77f));
                RaceHUDArtwork.Picture("Match", rect, uiTheme.match, new Vector2(.56f,.23f), new Vector2(.86f,.77f));
            }
            return button;
        }
#endif
        public void ShowKillReward(int kills, int coins)
        {
            if (killNotice == null) return;
            killNotice.text = (kills == 1 ? "KILL!" : kills + " KILLS!") + "  +" + coins + " COINS";
            killNoticeUntil = Time.unscaledTime + 2.5f;
            killNotice.gameObject.SetActive(true);
        }
        void BuildBoostMeter(Transform canvas)
        {
            var background = MakeBarLayer("BoostMeter", canvas, new Vector2(.36f, .05f), new Vector2(.64f, .069f),
                new Color(.04f, .06f, .1f, .9f));
            boostMeter=background;
            if(uiTheme!=null)
            {
                background.anchorMin=new Vector2(.34f,.035f);background.anchorMax=new Vector2(.66f,.115f);
                var inset=MakeBarLayer("Boost reserve track",background,new Vector2(.15f,.18f),new Vector2(.96f,.82f),new Color(.025f,.035f,.12f,.8f));
                boostFill=MakeBarLayer("BoostFill",inset,Vector2.zero,Vector2.up,Color.white);
            }
            else boostFill = MakeBarLayer("BoostFill", background, Vector2.zero, Vector2.up, new Color(.67f, .96f, .19f));
            if (hud.healthText == null) return;
            boostLabel = Instantiate(hud.healthText, canvas);
            boostLabel.name = "BoostLabel";
            boostLabel.raycastTarget = false;
            boostLabel.alignment = TMPro.TextAlignmentOptions.Center;
            LayoutText(boostLabel, canvas, new Vector2(.36f, .071f), new Vector2(.64f, .116f), 15);
            if(uiTheme!=null)LayoutText(boostLabel,background,new Vector2(.18f,.12f),new Vector2(.96f,.88f),20);
            boostLabel.text = "BOOST  •  HOLD SHIFT";
        }
        static RectTransform MakeBarLayer(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var element = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var image = element.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = color;
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        static void LayoutText(TMPro.TMP_Text label, Transform parent, Vector2 min, Vector2 max, float size)
        {
            if (label == null) return;
            var rect = label.rectTransform; rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = new Vector2(12, 4); rect.offsetMax = new Vector2(-12, -4);
            var canvas = label.GetComponentInParent<Canvas>();
            float readableScale = canvas == null ? 1 : Mathf.Max(1, 1 / Mathf.Max(.1f, canvas.rootCanvas.scaleFactor));
            label.enableAutoSizing = true; label.fontSizeMin = 10 * readableScale; label.fontSizeMax = size * readableScale;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap; label.overflowMode = TMPro.TextOverflowModes.Truncate;
        }
        public override Pose SpawnPose(int index)
        {
            Transform[] valid = spawnPoints == null
                ? System.Array.Empty<Transform>()
                : spawnPoints.Where(point => point != null).ToArray();

            if (valid.Length > 0)
            {
                int baseIndex = index % valid.Length;
                int duplicate = index / valid.Length;
                Transform point = valid[baseIndex];
                Vector3 position = point.position;

                if (duplicate > 0)
                {
                    int sequence = duplicate - 1;
                    float angle = (sequence * 137.508f + baseIndex * 31f) * Mathf.Deg2Rad;
                    float radius = 1.8f * (1 + sequence / 6);
                    position += point.right * (Mathf.Cos(angle) * radius)
                        + point.forward * (Mathf.Sin(angle) * radius);
                }

                return new Pose(track != null ? position : SurfacePosition(position, .3f), point.rotation);
            }

            int column = index % 5;
            int row = index / 5;
            return new Pose(SurfacePosition(new Vector3((column - 2) * 2f, 2, row * 2f), .3f), Quaternion.identity);
        }
        public override void ConfigurePlayer(NetworkPlayer player, MultiplayerMode mode)
        {
            player.movement.startInSkateMode = true;
            player.movement.skateForwardSpeed = baseSkateSpeed;
            player.movement.skateAcceleration = Mathf.Max(13f, player.movement.skateAcceleration);
            player.movement.skateBrakeSpeed = Mathf.Max(18f, player.movement.skateBrakeSpeed);
            if (!player.movement.TryGetComponent<SkaterFootPlacement>(out _))
                player.movement.gameObject.AddComponent<SkaterFootPlacement>();
            if (!player.movement.TryGetComponent<SkateSlopeLean>(out _))
                player.movement.gameObject.AddComponent<SkateSlopeLean>();
            if (player.IsOwner && !player.IsBot.Value && player.playerCamera != null)
            {
                player.playerCamera.FollowRaceSkater(player.movement);
                player.movement.cameraTransform = player.playerCamera.transform;
                player.playerCamera.gameObject.AddComponent<SkateSpeedWind>();
                player.playerCamera.gameObject.AddComponent<SkateGrenadeAim>();
            }
        }
        public override void BeginServerMatch()
        {
            if (track == null || boxSlots.Length == 0 || raceArena.boxRewards == null)
                throw new System.InvalidOperationException("The race needs a route, item-box positions, and reward settings.");
            racers.Clear(); departed.Clear(); nextStandings = 0;
            Running = true;
            startTime = NetworkManager.Singleton.ServerTime.Time + CountdownSeconds;
            raceDeadline = startTime + raceTimeoutSeconds;
            finishCount = 0;
            foreach (var player in SkateCombat.Players)
            {
                player.StartsAt.Value = startTime;
                player.RaceClosesAt.Value = raceDeadline;
                player.FinishSeconds.Value = 0;
                float distance = 0;
                if (track != null && track.Project(player.Position, out float onTrack)) distance = track.Delta(0, onTrack);
                racers[player] = new Racer { Distance = distance, SafeDistance = distance, Safe = new Pose(player.Position, player.Player.movement.transform.rotation) };
            }
            SpawnPickup();
            nextPickup = Time.time + itemBoxRefillSeconds;
        }
        public override void EndServerMatch()
        {
            Running = false;
            foreach (var player in SkateCombat.Players)
            {
                if (!player.IsSpawned) continue;
                player.Drafting.Value = false;
                player.PackAssist.Value = 0;
                player.Finished.Value = true;
            }
        }
        void Update()
        {
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.IsServer && Running)
            {
                if (Time.time >= nextPickup) { SpawnPickup(); nextPickup = Time.time + itemBoxRefillSeconds; }
            }
            if (killNotice != null && Time.unscaledTime >= killNoticeUntil) killNotice.gameObject.SetActive(false);
            UpdateSpectator();
            if (Time.unscaledTime < nextHud) return;
            nextHud = Time.unscaledTime + .1f;
            DrawHud();
            if (!returningToMenu && returnToMenuAt > 0 && Time.unscaledTime >= returnToMenuAt)
            {
                var manager = RoomManager.Instance;
                if (manager != null && manager.Busy) return;
                returningToMenu = true;
                if (pauseMenu != null) pauseMenu.QuitMatch();
                else if (manager != null) _ = manager.LeaveAsync();
                else if (MatchConnection.Instance != null) _ = MatchConnection.Instance.ReturnToMenuAsync();
            }
        }
        void FixedUpdate()
        {
            var net = NetworkManager.Singleton;
            if (!Running || net == null || !net.IsServer || net.ServerTime.Time < startTime) return;
            if (finishCount == 0 && net.ServerTime.Time >= raceDeadline) { EndServerMatch(); return; }
            if (track == null) { EndServerMatch(); return; }
            foreach (var gone in racers.Keys.Where(p => p == null || !p.IsSpawned).ToArray()) racers.Remove(gone);
            if (net.ServerTime.Time < nextStandings) return;
            double previousSampleTime = nextStandings == 0 ? startTime : nextStandings - .1;
            nextStandings = net.ServerTime.Time + .1;
            var active = SkateCombat.Players.Where(p => p.IsSpawned && !p.HasFinishedRace && racers.ContainsKey(p)).ToArray();
            UpdatePackAssistance(active);
            foreach (var player in active)
            {
                foreach (var pad in boostPads) pad.CheckRider(player);
                UpdateDrafting(player, active);
                if (player.Downed.Value || !track.Project(player.Position, out float distance)) continue;
                var race = racers[player];
                float step = track.Delta(race.Distance, distance);
                // Permit network interpolation and small jumps, but do not credit cutting across the loop.
                if (Mathf.Abs(step) > 10) continue;
                float previousDistance = race.Distance;
                race.Distance += step;
                player.CompletedLaps.Value = Mathf.Max(0, Mathf.FloorToInt(race.Distance / track.Length));
                float finishDistance = track.Length * LapsToFinish;
                if (previousDistance < finishDistance && race.Distance >= finishDistance)
                {
                    double fraction = (finishDistance - previousDistance) / step;
                    race.CrossedAt = previousSampleTime + (net.ServerTime.Time - previousSampleTime) * fraction;
                }
                if (TrySurface(player.Position, .96f, out var ground) && Mathf.Abs(ground.y - player.Position.y) < 1.5f)
                {
                    race.Safe = new Pose(ground, player.Player.movement.transform.rotation);
                    race.SafeDistance = race.Distance;
                }
            }
            var finishers = active.Where(p => !double.IsPositiveInfinity(racers[p].CrossedAt))
                .OrderBy(p => racers[p].CrossedAt).ThenBy(p => p.NetworkObjectId);
            foreach (var player in finishers)
            {
                player.FinishSeconds.Value = System.Math.Max(0, racers[player].CrossedAt - startTime);
                player.RacePlace.Value = ++finishCount;
                player.CompletedLaps.Value = LapsToFinish;
                player.FinishPlace.Value = finishCount;
                if (finishCount == 1)
                {
                    // After the first finish, everyone else gets to complete the race.
                    raceDeadline = double.PositiveInfinity;
                    foreach (var racer in SkateCombat.Players) racer.RaceClosesAt.Value = 0;

                }
            }
            var order = active.Where(p => !p.HasFinishedRace).OrderByDescending(p => racers[p].Distance)
                .ThenBy(p => p.NetworkObjectId).ToArray();
            for (int i = 0; i < order.Length; i++) order[i].RacePlace.Value = finishCount + i + 1;
            if (order.Length == 0) EndServerMatch();
        }
        void UpdateDrafting(SkateCombat player, SkateCombat[] active)
        {
            bool drafting = false;
            if (player.CanAct)
            {
                foreach (var other in active)
                {
                    if (other == player || !other.CanAct) continue;
                    Vector3 gap = other.Position - player.Position;
                    if (Mathf.Abs(gap.y) > 2f) continue;
                    gap.y = 0;
                    float ahead = Vector3.Dot(gap, player.Player.movement.transform.forward);
                    float lateral = Vector3.ProjectOnPlane(gap, player.Player.movement.transform.forward).magnitude;
                    if (ahead > 2f && ahead < 18f && lateral < 2.3f &&
                        Vector3.Dot(player.Player.movement.transform.forward, other.Player.movement.transform.forward) > .7f &&
                        SkateCombat.ClearPath(player.Position, other.Position))
                    {
                        drafting = true;
                        break;
                    }
                }
            }
            player.Drafting.Value = drafting;
        }
        public float TrailingBoostBonus(SkateCombat player)
        {
            if (!racers.TryGetValue(player, out var racer) || finishCount > 0) return 0;
            float leader = racers.Where(pair => pair.Key != null && pair.Key.IsSpawned && !pair.Key.HasFinishedRace)
                .Select(pair => pair.Value.Distance).DefaultIfEmpty(racer.Distance).Max();
            return Mathf.Lerp(0f, 2f, Mathf.InverseLerp(25f, 90f, leader - racer.Distance));
        }
        void OnDestroy()
        {
            foreach (var pad in boostPads) pad.Dispose();

        }
        public bool FellOff(SkateCombat player)
        {
            if (!racers.TryGetValue(player, out var race)) return player.Position.y < -25;
            if (player.Position.y >= race.Safe.position.y - 8f) return false;
            // A bank can descend much farther than eight metres. The physical
            // track underneath, rather than the last safe height, decides a fall.
            if (raceSurface != null && raceSurface.Raycast(new Ray(player.Position + Vector3.up * .25f, Vector3.down), out var below, 50f)
                && below.normal.y > .2f) return false;
            return true;
        }

        public Pose RecoveryPose(SkateCombat player, bool fellOff)
        {
            var position = player.Position;
            var rotation = player.Player.movement.transform.rotation;
            if (!fellOff && TrySurface(position, .96f, out var ground) && Mathf.Abs(ground.y - position.y) <= 4f)
                return new Pose(ground, rotation);
            if (!racers.TryGetValue(player, out var race)) return new Pose(position, rotation);
            race.Distance = race.SafeDistance;
            if (track != null) player.CompletedLaps.Value = Mathf.Max(0, Mathf.FloorToInt(race.Distance / track.Length));
            return race.Safe;
        }
        public bool TryBotRaceTarget(SkateCombat bot, float lateralOffset, out Vector3 target, float lookAhead = 10f)
        {
            target = default;
            if (track == null || !racers.TryGetValue(bot, out var racer)) return false;
            float distance = racer.Distance + lookAhead;
            Vector3 ahead = track.PointAt(distance);
            Vector3 tangent = track.PointAt(distance + 3f) - track.PointAt(distance - 3f);
            tangent.y = 0;
            target = ahead + Vector3.Cross(Vector3.up, tangent.normalized) * lateralOffset;
            return true;
        }
        public bool TryGetRaceDistance(SkateCombat player, out float distance)
        {
            distance = 0;
            if (!racers.TryGetValue(player, out var racer)) return false;
            distance = racer.Distance;
            return true;
        }
        public static float PackAssistForGap(float gap, float maximum) => Mathf.Clamp(maximum, 0f, 2.5f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(45f, 150f, gap));
        void UpdatePackAssistance(SkateCombat[] active)
        {
            float packFront = active.Where(p => !p.Downed.Value)
                .Select(p => racers[p].Distance).DefaultIfEmpty(0f).Max();
            foreach (var player in active)
                player.PackAssist.Value = player.CanAct ? PackAssistForGap(packFront - racers[player].Distance, maximumPackAssist) : 0f;
        }
        public bool IsDrivableBank(RaycastHit hit) => hit.collider == raceSurface && hit.normal.y >= .35f;
        public bool TryTrackSupport(Vector3 point, out RaycastHit hit) => RecoveryGround(point, out hit);
        public float BotRacePace(SkateCombat bot)
        {
            if (!Running || bot == null || !racers.TryGetValue(bot, out var racer)) return 1f;
            float humanFront = racers.Where(p => p.Key != null && p.Key.IsSpawned && !p.Key.Player.IsBot.Value)
                .Select(p => p.Value.Distance).DefaultIfEmpty(racer.Distance).Max();
            float amount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(60f, 160f, humanFront - racer.Distance));
            return Mathf.Lerp(1f, maximumBotCatchupPace, amount);
        }
        public bool RecoverStuckBot(SkateCombat bot)
        {
            if (!bot.CanAct || !bot.IsServer || !bot.Player.IsBot.Value || !racers.TryGetValue(bot, out var racer)) return false;
            if (!TryFindClearBotRecovery(bot.Player, racer.Distance, out var pose, out float behind)) return false;
            behind = Mathf.Min(behind, racer.Distance);
            racer.Distance = behind;
            racer.SafeDistance = behind;
            racer.Safe = pose;
            bot.CompletedLaps.Value = Mathf.Max(0, Mathf.FloorToInt(behind / track.Length));
            bot.RepositionStuckBot(pose);
            return true;
        }
        public bool TryFindClearBotRecovery(NetworkPlayer bot, float distance, out Pose pose, out float restoredDistance)
        {
            pose = default;
            restoredDistance = distance;
            if (track == null || bot == null || bot.movement == null) return false;
            var capsule = bot.movement.GetComponent<CapsuleCollider>();
            if (capsule == null) return false;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(capsule.transform.lossyScale.x), Mathf.Abs(capsule.transform.lossyScale.z));
            float halfHeight = Mathf.Max(radius, capsule.height * Mathf.Abs(capsule.transform.lossyScale.y) * .5f);
            float centerHeight = capsule.transform.TransformVector(capsule.center).y;
            foreach (float offset in recoveryOffsets)
                foreach (float lane in recoveryLanes)
                {
                    float recoveryDistance = distance + offset;
                    Vector3 spawn = default, previous = default, facing = Vector3.forward;
                    bool clear = true;
                    foreach (float sample in recoveryPathSamples)
                    {
                        float at = recoveryDistance + sample;
                        Vector3 center = track.PointAt(at);
                        Vector3 tangent = track.PointAt(at + 2f) - track.PointAt(at - 2f);
                        tangent.y = 0;
                        tangent.Normalize();
                        Vector3 wanted = center + Vector3.Cross(Vector3.up, tangent) * lane;
                        if (!RecoveryGround(wanted, out var ground) || ground.normal.y < .65f)
                        { clear = false; break; }
                        float support = radius + (halfHeight - radius) * ground.normal.y;
                        Vector3 root = ground.point + Vector3.up * (support / ground.normal.y - centerHeight + .1f);
                        Vector3 bottom = root + Vector3.up * (centerHeight - halfHeight + radius);
                        Vector3 top = root + Vector3.up * (centerHeight + halfHeight - radius);
                        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, recoveryBlockers,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        if (count >= recoveryBlockers.Length) { clear = false; break; }
                        for (int i = 0; i < count; i++)
                            if (BlocksBotRecovery(recoveryBlockers[i], bot, sample == 0f)) { clear = false; break; }
                        if (!clear) break;
                        if (sample > 0f)
                        {
                            Vector3 travel = root - previous;
                            Vector3 startBottom = previous + Vector3.up * (centerHeight - halfHeight + radius);
                            Vector3 startTop = previous + Vector3.up * (centerHeight + halfHeight - radius);
                            int hits = Physics.CapsuleCastNonAlloc(startBottom, startTop, radius, travel.normalized,
                                recoverySweepHits, travel.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                            if (hits >= recoverySweepHits.Length) { clear = false; break; }
                            for (int i = 0; i < hits; i++)
                            {
                                var hit = recoverySweepHits[i];
                                if (hit.normal.y < .65f && BlocksBotRecovery(hit.collider, bot, false))
                                { clear = false; break; }
                            }
                            if (!clear) break;
                        }
                        else { spawn = root; facing = tangent; }
                        previous = root;
                    }
                    if (!clear) continue;
                    pose = new Pose(spawn, Quaternion.LookRotation(facing));
                    restoredDistance = recoveryDistance;
                    return true;
                }
            return false;
        }
        bool RecoveryGround(Vector3 wanted, out RaycastHit ground)
        {
            if (raceSurface != null)
                return raceSurface.Raycast(new Ray(wanted + Vector3.up * 20f, Vector3.down), out ground, 50f);
            return FindSurface(wanted + Vector3.up * 2f, out ground);
        }
        static bool BlocksBotRecovery(Collider collider, NetworkPlayer bot, bool checkRacers)
        {
            if (collider.transform.IsChildOf(bot.transform) ||
                collider.GetComponentInParent<SkateNetworkItem>() != null) return false;
            var other = collider.GetComponentInParent<NetworkPlayer>();
            if (other != null)
            {
                var combat = other.GetComponent<SkateCombat>();
                return checkRacers && (combat == null || !combat.HasFinishedRace);
            }
            return true;
        }
        void UpdateSpectator()
        {
            var local = SkateCombat.Players.FirstOrDefault(p => p.IsOwner && !p.Player.IsBot.Value);
            if (local == null || !local.HasFinishedRace || local.Finished.Value || local.Player.IsPaused) return;
            var camera = local.Player.playerCamera;
            if (camera == null) return;
            bool next = Keyboard.current?.rightArrowKey.wasPressedThisFrame == true;
            bool previous = Keyboard.current?.leftArrowKey.wasPressedThisFrame == true;
            if (!next && !previous && watching != null && watching.IsSpawned && !watching.HasFinishedRace) return;
            var choices = SkateCombat.Players.Where(p => p.IsSpawned && !p.HasFinishedRace).OrderBy(p => p.RacePlace.Value).ToArray();
            if (choices.Length == 0) return;
            int current = System.Array.IndexOf(choices, watching);
            int index = current < 0 ? 0 : (current + (previous ? -1 : 1) + choices.Length) % choices.Length;
            watching = choices[index];
            camera.FollowRaceSkater(watching.Player.movement);
        }
        void SpawnPickup()
        {
            if (boxSlots == null || itemBox == null || raceArena == null || raceArena.boxRewards == null) return;
            double now = NetworkManager.Singleton.ServerTime.Time;
            for (int i = 0; i < boxSlots.Length; i++)
            {
                var slot = boxSlots[i];
                if (slot.Item != null || now < slot.ReadyAt || pickupPoints[i] == null) continue;
                var pickup = Instantiate(itemBox, PickupPosition(pickupPoints[i]), pickupPoints[i].rotation);
                pickup.SetReward(raceArena.boxRewards.Roll());
                pickup.SetPickupSource(this, i);
                slot.Item = pickup;
                pickup.NetworkObject.Spawn(true);
            }
        }
        public void PickupCollected(int index, SkateNetworkItem item)
        {
            if (boxSlots == null || index < 0 || index >= boxSlots.Length || boxSlots[index].Item != item) return;
            boxSlots[index].Item = null;
            boxSlots[index].ReadyAt = NetworkManager.Singleton.ServerTime.Time + itemBoxRespawnSeconds;
        }
        public void ShowHitNotice(string name, bool knockout)
        {
            feedback?.Notice((knockout ? "KNOCKED OUT BY " : "HIT BY ") + name, new Color(1f, .55f, .3f), 2f);
        }
        public void PlayerLeft(SkateCombat player)
        {
            if (player == null || !player.Active) return;
            var row = Snapshot(player); row.Disconnected = true;
            departed[player.NetworkObjectId] = row;
            racers.Remove(player);
        }
        SkateRaceResult Snapshot(SkateCombat p) => new SkateRaceResult {
            Name = ScoreName(p), PlayerId = p.NetworkObjectId, Place = p.RacePlace.Value, Kills = p.Kills.Value,
            Coins = p.CoinsEarned.Value, Seconds = p.FinishSeconds.Value, Finished = p.HasFinishedRace,
            Progress = racers.TryGetValue(p, out var race) ? race.Distance : 0f
        };
        public SkateRaceResult[] ResultRows() => SkateCombat.Players.Where(p => p != null && p.IsSpawned)
            .Select(Snapshot).Concat(departed.Values).OrderBy(p => p.Finished ? 0 : 1)
            .ThenBy(p => p.Finished ? p.Place : int.MaxValue).ThenByDescending(p => p.Progress).ToArray();
        Vector3 PickupPosition(Transform point) => track != null ? point.position : SurfacePosition(point.position, .45f);
        static Vector3 SurfacePosition(Vector3 position, float clearance)
        {
            return TrySurface(position, clearance, out var grounded) ? grounded : position;
        }
        static bool TrySurface(Vector3 position, float clearance, out Vector3 grounded)
        {
            bool found = FindSurface(position, out var ground);
            grounded = found ? ground.point + Vector3.up * clearance : position;
            return found;
        }
        static bool FindSurface(Vector3 position, out RaycastHit ground)
        {
            var hits = Physics.RaycastAll(position + Vector3.up * .25f, Vector3.down, 10,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .OrderBy(hit => hit.distance);
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<SkateCombat>() != null ||
                    hit.collider.GetComponentInParent<SkateNetworkItem>() != null)
                    continue;
                if (hit.normal.y < .45f) continue;
                ground = hit;
                return true;
            }
            ground = default;
            return false;
        }
        public bool SpawnProjectile(SkateCombat owner, bool isBomb, float charge, Vector3 aim)
        {
            var prefab = isBomb ? bomb : mine;
            if (!NetworkManager.Singleton.IsServer || !Running || prefab == null) return false;
            var inventory = owner.Player.movement.GetComponent<SkatePlayerSettings>();
            if (inventory == null) return false;
            var forward = Vector3.ProjectOnPlane(owner.Player.movement.transform.forward, Vector3.up).normalized;
            Vector3 velocity = Vector3.zero;
            var position = owner.Position - forward * inventory.minePlaceDistance;
            if (isBomb)
                SkateGrenadeLaunch.Calculate(owner, inventory, charge, aim, out position, out velocity);
            else
            {
                bool placed = false;
                for (int step = 0; step < 4; step++)
                {
                    Vector3 candidate = position - forward * (step * 3.8f);
                    if (!RecoveryGround(candidate, out var ground) || ground.normal.y < .6f) continue;
                    candidate = ground.point + Vector3.up * .1f;
                    if (SkateNetworkItem.ActiveItems.Any(p => p != null && p.kind == SkateItemKind.Mine && (p.transform.position - candidate).sqrMagnitude < 3.8f * 3.8f)) continue;
                    int count = Physics.OverlapSphereNonAlloc(candidate + Vector3.up * .3f, .2f, recoveryBlockers,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (count >= recoveryBlockers.Length) continue;
                    bool blocked = false;
                    for (int c = 0; c < count; c++)
                        if (recoveryBlockers[c].GetComponentInParent<NetworkPlayer>() == null && recoveryBlockers[c].GetComponentInParent<SkateNetworkItem>() == null)
                            { blocked = true; break; }
                    if (blocked) continue;
                    position = candidate; placed = true; break;
                }
                if (!placed) return false;
            }
            var item = Instantiate(prefab, position, Quaternion.LookRotation(forward));
            item.SetAttacker(owner); item.NetworkObject.Spawn(true);
            if (isBomb && item.TryGetComponent<Rigidbody>(out var projectile))
                projectile.linearVelocity = velocity;
            return true;
        }
        static string ScoreName(SkateCombat skater) => skater.Player.Username.Value.ToString()
            .Replace('<', '‹').Replace('>', '›').Replace('\r', ' ').Replace('\n', ' ');

        void DrawHud()
        {
            var local = SkateCombat.Players.FirstOrDefault(p => p.IsOwner && !p.Player.IsBot.Value);
            if (local == null || hud == null) return;
            feedback?.UpdateRace(local);
            var sorted = SkateCombat.Players.OrderBy(p => p.RacePlace.Value <= 0 ? int.MaxValue : p.RacePlace.Value)
                .ThenBy(p => p.NetworkObjectId).ToArray();
            var text = new StringBuilder();
            for (int i = 0; i < sorted.Length; i++)
            {
                var skater = sorted[i];
                string name = ScoreName(skater);
                string progress = skater.HasFinishedRace ? "FINISHED" : skater.Finished.Value ? "DNF" :
                    "LAP " + Mathf.Min(skater.CompletedLaps.Value + 1, LapsToFinish) + "/" + LapsToFinish;
                text.AppendLine((skater.RacePlace.Value > 0 ? skater.RacePlace.Value : i + 1) + ". " + name + "   " +
                    skater.Kills.Value + " K  •  " + progress);
            }
            if (hud.rankingText != null) { hud.rankingText.richText = true; hud.rankingText.text = text.ToString(); }
            if (boostFill != null)
                boostFill.anchorMax = new Vector2(Mathf.Clamp01(local.BoostReserve.Value / SkateCombat.MaxBoostReserve), 1f);
            if (boostLabel != null)
                boostLabel.text = local.BoostReserve.Value > 0f
                    ? (local.ReserveBoosting.Value ? "BOOST  " : "HOLD SHIFT  •  BOOST  ") + local.BoostReserve.Value.ToString("F1") + "s"
                    : uiTheme != null ? "" : "BOOST EMPTY";
            if (hud.healthText != null) hud.healthText.text = (uiTheme != null ? "" : "HEALTH: " + local.Health.Value + "/" + local.MaxHealth) + (local.IsProtected ? " · PROTECTED" : "");
            if (hud.throwableText != null)
            {
                string item = local.ItemCount.Value == 0 ? "ITEM: EMPTY" : (local.Item.Value == 1 ? "BOMB x" : "MINE x") + local.ItemCount.Value;
                if (local.IsChargingThrow) item += "\nTHROW " + Mathf.RoundToInt(local.ThrowCharge * 100) + "%";
                if (local.BoostSpeed > 0) item += "\nSPEED BOOST";
                else if (local.Drafting.Value) item += "\nSLIPSTREAM";
                if (uiTheme != null) item = (local.IsChargingThrow ? "THROW " + Mathf.RoundToInt(local.ThrowCharge * 100) + "%" : "") + (local.BoostSpeed > 0 ? "\nSPEED BOOST" : local.Drafting.Value ? "\nSLIPSTREAM" : "");
                hud.throwableText.text = item;
            }
            double now = NetworkManager.Singleton.ServerTime.Time;
            if (hud.timerText != null)
            {
                double secondsToStart = local.StartsAt.Value - now;
                bool showStart = !local.Finished.Value && !local.HasFinishedRace && local.StartsAt.Value > 0
                    && secondsToStart > -GoMessageSeconds;
                hud.timerText.fontSizeMax = showStart ? raceTimerFontSize * 3 : raceTimerFontSize;
                hud.timerText.text = local.Finished.Value ? "RACE COMPLETE"
                    : showStart ? (secondsToStart > 0 ? Mathf.Clamp(Mathf.CeilToInt((float)secondsToStart), 1, CountdownSeconds).ToString() : "GO!")
                    : !local.Started ? "GET READY"
                    : local.HasFinishedRace ? "FINISHED #" + local.FinishPlace.Value + "  •  SPECTATING " + (watching == null ? "" : ScoreName(watching)) +
                        "\n← / → Switch racer  •  Esc Leave"
                    : "PLACE " + local.RacePlace.Value + "/" + sorted.Length + (uiTheme != null ? "" : "  •  LAP " +
                        Mathf.Min(local.CompletedLaps.Value + 1, LapsToFinish) + "/" + LapsToFinish);
                if (!local.Finished.Value && local.RaceClosesAt.Value > 0 && (sorted.Any(p => p.HasFinishedRace) || local.RaceClosesAt.Value - now <= 60))
                    hud.timerText.text += "\nRACE ENDS IN " + Mathf.Max(0, Mathf.CeilToInt((float)(local.RaceClosesAt.Value - now))) + "s";
            }
            if (local.Finished.Value && !resultsClosed)
            {
                resultsClosed = true;
                returnToMenuAt = Time.unscaledTime + resultsDisplaySeconds;
            }
            if (local.Finished.Value && hud.timerText != null && returnToMenuAt > 0)
                hud.timerText.text = "MAIN MENU IN " + Mathf.Max(0, Mathf.CeilToInt(returnToMenuAt - Time.unscaledTime)) + "s";
            bool showResults = local.Finished.Value &&
                (pauseMenu == null || !pauseMenu.IsPaused);
            bool overlay = showResults || pauseMenu != null && pauseMenu.IsPaused;
            foreach (var label in new[] { hud.healthText, hud.timerText, hud.throwableText, hud.rankingText })
                if (label != null) label.gameObject.SetActive(uiTheme != null ? !showResults : !overlay);
            if (boostMeter != null) boostMeter.gameObject.SetActive(!showResults && !local.HasFinishedRace && (uiTheme != null || !overlay));
            if (boostLabel != null) boostLabel.gameObject.SetActive(!showResults && !local.HasFinishedRace && (uiTheme != null || !overlay));
            if (overlay && killNotice != null) killNotice.gameObject.SetActive(false);
            feedback?.SetControlsVisible(!overlay && !local.HasFinishedRace, !showResults && !local.HasFinishedRace && (uiTheme != null || !overlay));
            feedback?.DrawArtwork(local, showResults, pauseMenu != null && pauseMenu.IsPaused);
            if (hud.endPanel != null) hud.endPanel.SetActive(showResults);
            if (showResults)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                feedback?.DrawResults(local, ResultRows(), returnToMenuAt > 0 ? Mathf.Max(0, Mathf.CeilToInt(returnToMenuAt - Time.unscaledTime)) : -1);

            }
            else if (local.HasFinishedRace && !local.Finished.Value && !local.Player.IsPaused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
