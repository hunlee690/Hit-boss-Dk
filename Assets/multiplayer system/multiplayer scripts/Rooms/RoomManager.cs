using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HitBoss.Social;
using Unity.Services.Friends;
using Unity.Services.Friends.Notifications;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace HitBoss.Multiplayer
{
    [DefaultExecutionOrder(-90)]
    public sealed class RoomManager : MonoBehaviour
    {
        public static RoomManager Instance { get; private set; }
        public MultiplayerMode[] modes;
        public MatchConnection connection;
        public ISession Room { get; private set; }
        public bool Busy { get; private set; }
        public string Status { get; private set; } = "Create a private room or join with a code.";
        public readonly List<RoomInvitation> Invitations = new List<RoomInvitation>();
        public event Action Changed;
        public bool IsHost => Room != null && Room.IsHost;
        public MultiplayerMode Mode => modes.FirstOrDefault(m => m.modeId == UnityRoomService.Property(Room, "mode"));
        public bool InMatch => Room != null && UnityRoomService.Property(Room, "phase") == "playing";
        public bool CanStart => !Busy && IsHost && Mode != null && Room.PlayerCount >= Mode.minPlayers && Room.PlayerCount <= Mode.maxPlayers && Room.Players.All(p => UnityRoomService.IsReady(Room, p));
        UnityRoomService service;
        bool listening, leaving;
        string originalHost;
        float nextRefresh;
        readonly Dictionary<string, float> inviteTimes = new Dictionary<string, float>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }
        void Update()
        {
            if (!listening && OnlineManager.Instance?.Friends?.Ready == true)
            {
                FriendsService.Instance.MessageReceived += ReceiveInvite; listening = true;
            }
            if (Invitations.RemoveAll(i => i.Expired) > 0) Notify();
            if (Room != null && !Busy && Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 12; RefreshQuietly(); }
        }
        public void Notify() => Changed?.Invoke();
        public void SetStatus(string text) { Status = text; Notify(); }
        void RequireAccount()
        {
            if (OnlineManager.Instance?.Ready != true) throw new InvalidOperationException("Wait for your profile to connect, then try again.");
            service = service ?? new UnityRoomService();
        }
        public async Task RunAsync(Func<Task> action)
        {
            if (Busy) return;
            Busy = true; Notify();
            try { await action(); }
            catch (Exception e)
            {
                Debug.LogWarning("[Rooms] " + e.GetType().Name + ": " + e.Message);
                SetStatus(e is ArgumentException || e is InvalidOperationException ? e.Message : "Room action failed. Check the code, connection, or room capacity and try again.");
            }
            finally { Busy = false; Notify(); }
        }
        public Task CreateAsync(int index) => RunAsync(async () =>
        {
            RequireAccount(); if (Room != null) throw new InvalidOperationException("Leave your current room first.");
            if (index < 0 || index >= modes.Length) throw new ArgumentException("Select a mode.");
            SetStatus("Creating private room…");
            Attach(await service.CreateAsync(modes[index], OnlineManager.Instance.Profiles.Current.username));
            SetStatus("Room created. Invite friends or share the code.");
        });
        public Task JoinAsync(string code) => RunAsync(async () =>
        {
            RequireAccount(); if (Room != null) throw new InvalidOperationException("Leave your current room before joining another.");
            SetStatus("Joining room…");
            var joined = await service.JoinAsync(code, OnlineManager.Instance.Profiles.Current.username);
            if (UnityRoomService.Property(joined, "version") != UnityRoomService.Version || !modes.Any(m => m.modeId == UnityRoomService.Property(joined, "mode")) || joined.IsLocked)
            { await joined.LeaveAsync(); throw new InvalidOperationException("This room has started or uses a different game version."); }
            Attach(joined); SetStatus("Joined. Press Ready when you are ready to play.");
        });
        void Attach(ISession room)
        {
            Room = room; originalHost = room.Host; nextRefresh = Time.unscaledTime + 12;
            Room.Changed += RoomChanged; Room.Deleted += RoomEnded; Room.RemovedFromSession += RoomEnded;
            Room.SessionHostChanged += HostChanged; Notify();
        }
        void Detach()
        {
            if (Room == null) return;
            Room.Changed -= RoomChanged; Room.Deleted -= RoomEnded; Room.RemovedFromSession -= RoomEnded; Room.SessionHostChanged -= HostChanged; Room = null;
        }
        void RoomChanged() { Notify(); }
        void HostChanged(string id) { if (id != originalHost) RoomEnded(); }
        async void RoomEnded()
        {
            if (leaving) return;
            await LeaveCoreAsync(); SetStatus("The host closed the room or disconnected. Create or join another room.");
        }
        async void RefreshQuietly()
        {
            var room = Room;
            try { if (room != null) await room.RefreshAsync(); }
            catch { if (Room == room) SetStatus("Room connection interrupted. Retry or leave the room."); }
        }
        public Task RefreshAsync() => RunAsync(async () => { if (Room != null) await Room.RefreshAsync(); SetStatus("Room refreshed."); });
        public Task ReadyAsync() => RunAsync(async () =>
        {
            if (Room == null || IsHost || Room.IsLocked) return;
            await UnityRoomService.SetReadyAsync(Room, !UnityRoomService.IsReady(Room, Room.CurrentPlayer));
        });
        public Task SelectModeAsync(int index) => RunAsync(async () =>
        {
            if (!IsHost || Room.IsLocked || index < 0 || index >= modes.Length) return;
            if (Room.PlayerCount > modes[index].maxPlayers) throw new InvalidOperationException("There are too many players for this mode.");
            await UnityRoomService.SetModeAsync(Room.AsHost(), modes[index]); SetStatus("Mode changed. Everyone must ready up again.");
        });
        public Task StartMatchAsync() => RunAsync(async () =>
        {
            if (!IsHost) return;
            await Room.RefreshAsync();
            var selected = Mode;
            if (selected == null || Room.PlayerCount < selected.minPlayers || Room.PlayerCount > selected.maxPlayers || !Room.Players.All(p => UnityRoomService.IsReady(Room, p)))
                throw new InvalidOperationException("Wait for the required players and everyone to be ready.");
            var host = Room.AsHost();
            host.IsLocked = true; await host.SavePropertiesAsync();
            try
            {
                SetStatus("Connecting players…");
                await host.Network.StartRelayNetworkAsync(new RelayNetworkOptions());
                await connection.WaitForPlayersAsync(host.PlayerCount);
                host.SetProperty("phase", new SessionProperty("playing", VisibilityPropertyOptions.Member));
                await host.SavePropertiesAsync();
                connection.LoadMode(selected);
                SetStatus("Match started.");
            }
            catch
            {
                try { await host.Network.StopNetworkAsync(); host.IsLocked = false; host.SetProperty("phase", new SessionProperty("room", VisibilityPropertyOptions.Member)); await host.SavePropertiesAsync(); } catch { }
                throw;
            }
        });
        public Task LeaveAsync() => RunAsync(async () => { await LeaveCoreAsync(); SetStatus("You left the room."); });
        async Task LeaveCoreAsync()
        {
            if (leaving) return;
            leaving = true;
            var old = Room; Detach();
            try
            {
                if (old != null) { if (old.IsHost) await old.AsHost().DeleteAsync(); else await old.LeaveAsync(); }
            }
            catch (Exception e) { Debug.LogWarning("[Rooms] Leave: " + e.Message); }
            finally { connection.ReturnToMenu(); leaving = false; Notify(); }
        }
        public Task InviteAsync(string playerId) => RunAsync(async () =>
        {
            if (Room == null || Room.IsLocked) throw new InvalidOperationException("Create or join a room before inviting friends.");
            if (OnlineManager.Instance?.Friends?.GetPlayers(SocialList.Friends).Any(p => p.playerId == playerId && p.online) != true)
                throw new InvalidOperationException("Invitations are available for online friends.");
            if (inviteTimes.TryGetValue(playerId, out var time) && Time.unscaledTime - time < 15) throw new InvalidOperationException("Wait a few seconds before sending another invite.");
            await FriendsService.Instance.MessageAsync(playerId, new RoomInvitation { code = Room.Code, senderName = OnlineManager.Instance.Profiles.Current.username, expires = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds() });
            inviteTimes[playerId] = Time.unscaledTime; SetStatus("Room invitation sent.");
        });
        void ReceiveInvite(IMessageReceivedEvent message)
        {
            try
            {
                var invite = message.GetAs<RoomInvitation>();
                if (invite == null || invite.kind != "hitboss.room.v1" || invite.Expired || invite.expires > DateTimeOffset.UtcNow.AddMinutes(11).ToUnixTimeSeconds() || string.IsNullOrWhiteSpace(invite.code) || invite.code.Length > 12) return;
                var friend = OnlineManager.Instance?.Friends?.GetPlayers(SocialList.Friends).FirstOrDefault(p => p.playerId == message.UserId);
                if (friend == null) return;
                invite.senderId = message.UserId; invite.senderName = friend.username;
                Invitations.RemoveAll(i => i.senderId == invite.senderId);
                if (Invitations.Count >= 10) Invitations.RemoveAt(0);
                Invitations.Add(invite); Notify();
            }
            catch (Exception e) { Debug.LogWarning("[Rooms] Ignored invalid invitation: " + e.GetType().Name); }
        }
        public void Dismiss(RoomInvitation invitation) { Invitations.Remove(invitation); Notify(); }
        void OnDestroy()
        {
            if (Instance != this) return;
            if (listening) FriendsService.Instance.MessageReceived -= ReceiveInvite;
            Detach(); Instance = null;
        }
    }
}
