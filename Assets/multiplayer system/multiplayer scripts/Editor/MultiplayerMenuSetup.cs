using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HitBoss.Multiplayer.Editor
{
    public static class MultiplayerMenuSetup
    {
        const string Root = "Assets/multiplayer system/";
        static TMP_FontAsset font;
        static readonly Color Panel = new Color(.065f, .085f, .12f, .99f);
        static readonly Color Surface = new Color(.11f, .15f, .20f);
        static readonly Color Accent = new Color(.38f, .85f, .72f);
        static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go, "Build multiplayer menu");
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); return rt;
        }
        static Image Background(RectTransform rt, Color color) { var image = rt.gameObject.AddComponent<Image>(); image.color = color; return image; }
        static TMP_Text Label(Transform parent, string name, string value, float x, float y, float w, float h, float size = 16)
        {
            var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size; text.color = Color.white; text.raycastTarget = false; text.richText = false; text.overflowMode = TextOverflowModes.Ellipsis; return text;
        }
        static Button Button(Transform parent, string name, string text, float x, float y, float w, float h, bool accent = false)
        {
            var rect = Rect(name, parent, x, y, w, h); var image = Background(rect, accent ? new Color(.16f, .43f, .36f) : Surface);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = Label(rect, "Label", text, 4, 0, w - 8, h, 14); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        static TMP_InputField Input(Transform parent, float x, float y, float w, float h)
        {
            var rect = Rect("Room code input", parent, x, y, w, h); Background(rect, Surface);
            var area = Rect("Text area", rect, 10, 4, w - 20, h - 8); area.gameObject.AddComponent<RectMask2D>();
            var value = Label(area, "Text", "", 0, 0, w - 20, h - 8); value.alignment = TextAlignmentOptions.MidlineLeft;
            var hint = Label(area, "Placeholder", "Room code", 0, 0, w - 20, h - 8); hint.alignment = TextAlignmentOptions.MidlineLeft; hint.color = Color.gray;
            var input = rect.gameObject.AddComponent<TMP_InputField>(); input.textViewport = area; input.textComponent = value; input.placeholder = hint; input.characterLimit = 12; input.contentType = TMP_InputField.ContentType.Alphanumeric; return input;
        }
        static RectTransform List(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = Rect(name, parent, x, y, w, h); Background(rect, new Color(.045f, .06f, .09f));
            var scroll = rect.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", rect, 0, 0, w, h); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 6, 6, w - 12, 0);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 6; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content; return content;
        }
        [MenuItem("Tools/Hit Boss/Build Multiplayer Menu")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenu>();
            if (menu == null) throw new InvalidOperationException("Open the main menu scene first.");
            if (UnityEngine.Object.FindFirstObjectByType<RoomMenuController>(FindObjectsInactive.Include) != null) throw new InvalidOperationException("Multiplayer UI is already installed.");
            Directory.CreateDirectory("Library/CodexMultiplayerBackup");
            EditorSceneManager.SaveScene(menu.gameObject.scene, "Library/CodexMultiplayerBackup/main menu.before-multiplayer.unity", true);
            Directory.CreateDirectory(Root + "multiplayer prefabs"); Directory.CreateDirectory(Root + "multiplayer modes"); AssetDatabase.Refresh();
            font = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault()?.font ?? TMP_Settings.defaultFontAsset;
            var canvas = menu.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var driver = Rect("Multiplayer menu", canvas.transform, 0, 0, 0, 0);
            var view = driver.gameObject.AddComponent<RoomMenuController>(); view.mainMenu = menu;
            view.matterButton = menu.GetComponentsInChildren<Button>(true).First(b => b.name == "MatterButton");
            var buttonRect = (RectTransform)view.matterButton.transform;
            view.badge = Label(buttonRect, "Room badge", "", 0, -22, buttonRect.rect.width, 22, 12); view.badge.color = Accent; view.badge.alignment = TextAlignmentOptions.Center;
            var shade = Rect("Matter room window", canvas.transform, 0, 0, 0, 0); shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero; Background(shade, new Color(0, 0, 0, .7f));
            view.window = shade.gameObject;
            var panel = Rect("Private rooms", shade, 0, 0, 880, 560); panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f); panel.anchoredPosition = Vector2.zero; Background(panel, Panel);
            Label(panel, "Title", "PLAY TOGETHER", 24, 18, 600, 34, 25).color = Accent;
            Label(panel, "Subtitle", "Private rooms • friends • your choice of mode", 24, 56, 740, 24, 14);
            view.closeButton = Button(panel, "Close", "Close", 770, 22, 86, 34);
            Label(panel, "Mode heading", "GAME MODE", 24, 96, 140, 22, 12).color = Accent;
            view.previousMode = Button(panel, "Previous mode", "<", 24, 124, 36, 36);
            view.modeText = Label(panel, "Selected mode", "Select mode", 74, 125, 684, 34, 18); view.modeText.alignment = TextAlignmentOptions.MidlineLeft;
            view.nextMode = Button(panel, "Next mode", ">", 820, 124, 36, 36);
            view.createArea = Rect("Create or join", panel, 24, 174, 832, 44).gameObject;
            view.createButton = Button(view.createArea.transform, "Create room", "Create private room", 0, 0, 240, 40, true);
            view.codeInput = Input(view.createArea.transform, 420, 0, 246, 40);
            view.joinButton = Button(view.createArea.transform, "Join by code", "Join room", 676, 0, 156, 40);
            view.roomArea = Rect("Current room", panel, 24, 174, 832, 44).gameObject;
            Label(view.roomArea.transform, "Code heading", "ROOM CODE", 0, 0, 100, 16, 11).color = Accent;
            view.codeText = Label(view.roomArea.transform, "Room code", "------", 0, 17, 170, 25, 22);
            view.copyButton = Button(view.roomArea.transform, "Copy code", "Copy", 180, 0, 76, 40);
            view.refreshButton = Button(view.roomArea.transform, "Refresh room", "Refresh", 266, 0, 90, 40);
            view.leaveButton = Button(view.roomArea.transform, "Leave room", "Leave room", 420, 0, 128, 40);
            view.readyButton = Button(view.roomArea.transform, "Ready", "Ready", 566, 0, 266, 40, true);
            view.startButton = Button(view.roomArea.transform, "Start match", "Start together", 566, 0, 266, 40, true);
            view.rosterTitle = Label(panel, "Roster heading", "PLAYERS", 24, 236, 390, 24, 14);
            view.rightTitle = Label(panel, "Friends heading", "INVITE FRIENDS", 454, 236, 400, 24, 14);
            view.playerContent = List(panel, "Players", 24, 268, 402, 194);
            view.friendContent = List(panel, "Friends and invitations", 454, 306, 402, 156);
            view.friendsTab = Button(panel, "Friends tab", "Friends", 454, 268, 195, 30);
            view.invitesTab = Button(panel, "Invites tab", "Invites (0)", 657, 268, 199, 30);
            view.emptyPlayers = Label(panel, "Players empty", "Choose a mode, then create a room.\nShare its code or invite your friends.\n\nThe host starts once everyone is ready.", 44, 292, 362, 142, 16);
            view.emptyFriends = Label(panel, "Friends empty", "Friends appear here", 474, 326, 362, 112, 15);
            view.statusText = Label(panel, "Room status", "Create a room to get started.", 24, 474, 832, 36, 14); view.statusText.color = Accent;
            Label(panel, "Phase note", "Online free play: movement is shared. Combat and scoring will follow in the mode rules.", 24, 524, 832, 22, 12).color = new Color(.65f, .73f, .80f);
            var rowRect = Rect("Room player row", null, 0, 0, 390, 80); Background(rowRect, Surface);
            rowRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
            var row = rowRect.gameObject.AddComponent<RoomListRow>();
            row.title = Label(rowRect, "Name", "Player", 10, 5, 365, 24, 15);
            row.detail = Label(rowRect, "Details", "Online", 10, 33, 232, 36, 12); row.detail.color = new Color(.66f, .76f, .84f);
            row.action = Button(rowRect, "Action", "Invite", 254, 38, 80, 30, true);
            row.secondary = Button(rowRect, "Dismiss", "X", 342, 38, 32, 30);
            view.rowPrefab = PrefabUtility.SaveAsPrefabAsset(rowRect.gameObject, Root + "multiplayer prefabs/Room player row.prefab").GetComponent<RoomListRow>();
            Undo.DestroyObjectImmediate(rowRect.gameObject);

            var ruleObject = new GameObject("Shared movement rules"); ruleObject.AddComponent<SharedMovementRules>();
            var rules = PrefabUtility.SaveAsPrefabAsset(ruleObject, Root + "multiplayer prefabs/Shared movement rules.prefab").GetComponent<SharedMovementRules>();
            UnityEngine.Object.DestroyImmediate(ruleObject);
            var modes = new MultiplayerMode[menu.gameModes.Length];
            for (int i = 0; i < modes.Length; i++)
            {
                var mode = ScriptableObject.CreateInstance<MultiplayerMode>(); mode.modeId = "mode-" + i; mode.displayName = menu.gameModes[i].modeName; mode.sceneName = menu.gameModes[i].sceneName; mode.rulesPrefab = rules; mode.skating = i == 0; mode.maxPlayers = 5; mode.minPlayers = 2;
                AssetDatabase.CreateAsset(mode, Root + "multiplayer modes/" + mode.modeId + ".asset"); modes[i] = mode;
            }
            var avatar = UnityEngine.Object.Instantiate(menu.playerSetup); avatar.name = "Network player"; avatar.transform.SetParent(null); avatar.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var movement = avatar.GetComponentInChildren<PlayerController>(true); movement.transform.localPosition = Vector3.zero;
            avatar.AddComponent<NetworkObject>();
            movement.gameObject.AddComponent<OwnerNetworkTransform>();
            var animator = movement.animator != null ? movement.animator : movement.GetComponentInChildren<Animator>();
            var networkAnimator = animator.gameObject.AddComponent<OwnerNetworkAnimator>(); networkAnimator.Animator = animator;
            var player = avatar.AddComponent<NetworkPlayer>(); player.movement = movement; player.coopMovement = movement.GetComponent<CoopPlayerController>(); player.playerCamera = avatar.GetComponentInChildren<PlayerCamera>(true); player.participant = movement.GetComponent<MatchParticipant>();
            // Every reference is inside the prefab, including the owner's camera.
            if (player.playerCamera != null) { movement.cameraTransform = player.playerCamera.transform; player.playerCamera.target = movement.transform; player.playerCamera.playerController = movement; }
            var avatarPrefab = PrefabUtility.SaveAsPrefabAsset(avatar, Root + "multiplayer prefabs/Network player.prefab").GetComponent<NetworkPlayer>();
            UnityEngine.Object.DestroyImmediate(avatar);
            var runtime = new GameObject("Multiplayer Manager");
            var transport = runtime.AddComponent<UnityTransport>(); var net = runtime.AddComponent<NetworkManager>();
            net.NetworkConfig.NetworkTransport = transport; net.NetworkConfig.EnableSceneManagement = true; net.NetworkConfig.PlayerPrefab = null;
            net.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = avatarPrefab.gameObject });
            var roomManager = runtime.AddComponent<RoomManager>();
            var connection = runtime.AddComponent<MatchConnection>(); connection.network = net; connection.playerPrefab = avatarPrefab;
            roomManager.modes = modes; roomManager.connection = connection;
            var runtimePrefab = PrefabUtility.SaveAsPrefabAsset(runtime, Root + "multiplayer prefabs/Multiplayer Manager.prefab").GetComponent<RoomManager>();
            UnityEngine.Object.DestroyImmediate(runtime);
            driver.gameObject.AddComponent<MultiplayerBootstrap>().managerPrefab = runtimePrefab;
            shade.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene); EditorSceneManager.SaveScene(menu.gameObject.scene); AssetDatabase.SaveAssets();
            Debug.Log("[Rooms] Matter menu, mode assets and network prefabs installed. No play testing was run.");
        }
    }
}
