using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HitBoss.Multiplayer
{
    public sealed class MatchConnection : MonoBehaviour
    {
        public static MatchConnection Instance { get; private set; }
        public NetworkManager network;
        public NetworkPlayer playerPrefab;
        public string menuScene = "main menu";
        public NetworkModeRules ActiveRules { get; private set; }
        GameObject menuRig;
        PlayerCustomizationManager menuCustomization;
        public NetworkLoadout CurrentLoadout { get; private set; }
        PauseMenu scenePause;
        bool callbacksBound, returning, quitting;
        public void BindMenu(GameObject rig)
        {
            // Scene synchronization may recreate the menu; keep only its current preview rig.
            if (menuRig != null && menuRig != rig) { menuRig.SetActive(false); Destroy(menuRig); }
            menuRig = rig; returning = false;
            menuCustomization = FindFirstObjectByType<PlayerCustomizationManager>(FindObjectsInactive.Include);
        }

        void Awake()
        {
            Instance = this;
            SceneManager.sceneLoaded += SceneLoaded;
            network.OnClientDisconnectCallback += ClientDisconnected;
            network.OnServerStarted += BindSceneCallbacks;
        }
        void BindSceneCallbacks()
        {
            if (callbacksBound || network.SceneManager == null) return;
            network.SceneManager.OnLoadEventCompleted += LoadCompleted;
            callbacksBound = true;
        }
        void Update()
        {
            if (menuCustomization != null) CurrentLoadout = NetworkLoadout.Capture(menuCustomization);
            if (network.IsListening && !callbacksBound)
            {
                BindSceneCallbacks();
            }
            if (!network.IsListening) callbacksBound = false;
            if (scenePause == null && RoomManager.Instance != null && RoomManager.Instance.InMatch && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                var unlock = Cursor.lockState != CursorLockMode.None;
                Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = unlock;
                network.LocalClient?.PlayerObject?.GetComponent<NetworkPlayer>()?.SetPaused(unlock);
            }
        }
        public async Task WaitForPlayersAsync(int count, CancellationToken cancellation)
        {
            var deadline = Time.realtimeSinceStartup + 25;
            while (!network.IsHost || network.ConnectedClients.Count < count)
            {
                if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("A player could not connect. Please ready up and try again.");
                await Task.Delay(100, cancellation);
            }
            cancellation.ThrowIfCancellationRequested();
        }
        public void LoadMode(MultiplayerMode mode)
        {
            if (!network.IsHost) throw new InvalidOperationException("Only the host can start a match.");
            BindSceneCallbacks();
            if (!Application.CanStreamedLevelBeLoaded(mode.sceneName)) throw new InvalidOperationException("This mode's scene is missing from the build.");
            var status = network.SceneManager.LoadScene(mode.sceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started) throw new InvalidOperationException("Scene loading could not start: " + status);
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            scenePause = FindFirstObjectByType<PauseMenu>();
            if (RoomManager.Instance?.Room == null || !network.IsListening || scene.name == menuScene) return;
            if (menuRig != null) { menuRig.SetActive(false); Destroy(menuRig); menuRig = null; }
            // These managers run local-only AI/match loops. Online mode rules take their place.
            foreach (var manager in FindObjectsByType<ModeManagerBase>(FindObjectsSortMode.None)) manager.enabled = false;
            var definition = RoomManager.Instance.Mode;
            if (definition != null && definition.rulesPrefab != null) ActiveRules = Instantiate(definition.rulesPrefab);
        }
        void LoadCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (!network.IsHost || sceneName == menuScene || ActiveRules == null) return;
            if (timedOut.Count > 0) { RoomManager.Instance.SetStatus("Some players failed to load the scene."); }
            for (var i = 0; i < completed.Count; i++)
            {
                if (!network.ConnectedClients.TryGetValue(completed[i], out var client) || client.PlayerObject != null) continue;
                var pose = ActiveRules.SpawnPose(i);
                var player = Instantiate(playerPrefab, pose.position, pose.rotation);
                player.NetworkObject.SpawnAsPlayerObject(completed[i], true);
            }
            var botCount = RoomManager.Instance.BotCount;
            // Bots never become the host's PlayerObject and never own a camera or input.
            for (int i = 0; i < botCount; i++)
            {
                var pose = ActiveRules.SpawnPose(completed.Count + i);
                var bot = Instantiate(playerPrefab, pose.position, pose.rotation);
                bot.PrepareBot(i + 1);
                bot.NetworkObject.Spawn(true);
            }
            ActiveRules.BeginServerMatch();
        }
        async void ClientDisconnected(ulong id)
        {
            if (quitting || returning || RoomManager.Instance?.Room == null) return;
            if (!network.IsServer && id == network.LocalClientId)
            { await RoomManager.Instance.HandleDisconnectAsync(); }
        }
        public async Task ReturnToMenuAsync()
        {
            returning = true;
            if (ActiveRules != null && network.IsServer) ActiveRules.EndServerMatch();
            if (callbacksBound && network.SceneManager != null) network.SceneManager.OnLoadEventCompleted -= LoadCompleted;
            callbacksBound = false;
            if (network.IsListening) network.Shutdown();
            // NGO shuts down at the end of a frame. Do not let it destroy a newly loaded menu.
            while (!quitting && network != null && network.ShutdownInProgress) await Task.Delay(30);
            if (quitting || this == null) return;
            ActiveRules = null;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (SceneManager.GetActiveScene().name != menuScene) SceneManager.LoadScene(menuScene);
            else returning = false;
        }
        void OnGUI()
        {
            if (RoomManager.Instance?.InMatch != true) return;
            if (ActiveRules is HitBoss.Multiplayer.Skate.SkateMatchRules) return;
            GUI.Box(new Rect(16, 16, 330, 60), "Online free play • " + (RoomManager.Instance.Mode?.displayName ?? "Room") + "\nEsc: show cursor • combat rules coming next");
            if (scenePause == null && Cursor.lockState == CursorLockMode.None && GUI.Button(new Rect(24, 82, 140, 36), "Leave room")) _ = RoomManager.Instance.LeaveAsync();
        }
        void OnApplicationQuit() { quitting = true; }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (network != null) network.OnClientDisconnectCallback -= ClientDisconnected;
            if (network != null) network.OnServerStarted -= BindSceneCallbacks;
            if (Instance == this) Instance = null;
        }
    }
}
