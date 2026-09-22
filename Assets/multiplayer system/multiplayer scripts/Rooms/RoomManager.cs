using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        public MultiplayerMode Mode => modes.FirstOrDefault(m => m != null && m.modeId == UnityRoomService.Property(Room, "mode"));
        public bool InMatch => Room != null && UnityRoomService.Property(Room, "phase") == "playing";
        public bool PublicMatch => UnityRoomService.Property(Room, "public") == "1";
        public bool FillWithBots => UnityRoomService.Property(Room, "bots") == "1";
        public int BotCount => Room == null || Mode == null || !FillWithBots ? 0 : Math.Max(0, Math.Min(Room.MaxPlayers, Mode.maxPlayers) - Room.PlayerCount);
        public bool CanStart => !Busy && IsHost && !Room.IsLocked && Mode != null && Room.PlayerCount + BotCount >= Mode.minPlayers && Room.PlayerCount <= Mode.maxPlayers && Room.Players.All(p => UnityRoomService.IsReady(Room, p));
        float publicStartTime;
        bool autoStartIssued;
        public int SearchSeconds => Math.Max(0, (int)Math.Ceiling(publicStartTime - Time.unscaledTime));
        UnityRoomService service;
        bool listening, leaving, refreshing, quitting;
        CancellationTokenSource roomLifetime;
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
            if (PublicMatch && IsHost && !InMatch && !autoStartIssued && CanStart && SearchSeconds == 0)
            { autoStartIssued = true; _ = StartMatchAsync(); }
            if (!listening && OnlineManager.Instance?.Friends?.Ready == true)
            {
                FriendsService.Instance.MessageReceived += ReceiveInvite; listening = true;
            }
            if (Invitations.RemoveAll(i => i.Expired) > 0) Notify();
            if (Room != null && !Busy && !refreshing && Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 12; RefreshQuietly(); }
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
            if (Busy || leaving || quitting) return;
            Busy = true; Notify();
            try { await action(); }
            catch (OperationCanceledException) { }
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
            modes[index].ValidateConfiguration();
            SetStatus("Creating private room...");
            Attach(await service.CreateAsync(modes[index], OnlineManager.Instance.Profiles.Current.username));
            SetStatus("Room created. Invite friends or share the code.");
        });
        public Task FindOnlineAsync(int index) => RunAsync(async () =>
        {
            RequireAccount();
            if (Room != null) throw new InvalidOperationException("Leave your current room first.");
            if (index < 0 || index >= modes.Length) throw new ArgumentException("Select a mode.");
            var mode = modes[index]; mode.ValidateConfiguration();
            SetStatus("Looking for players in this mode...");
            var name = OnlineManager.Instance.Profiles.Current.username;
            var found = await service.FindPublicAsync(mode, name);
            if (found == null)
            {
                await Task.Delay(UnityEngine.Random.Range(1250, 2000));
                found = await service.FindPublicAsync(mode, name);
            }
            Attach(found ?? await service.CreateAsync(mode, name, true));
            if (!IsHost) await UnityRoomService.SetReadyAsync(Room, true);
            SetStatus(IsHost ? "Looking for players for 15 seconds. Empty places will use bots." : "Joined online match. Waiting for the host...");
        });
        public Task ToggleBotsAsync() => RunAsync(async () =>
        {
            if (!IsHost || Room.IsLocked || PublicMatch) return;
            Room.AsHost().SetProperty("bots", new SessionProperty(FillWithBots ? "0" : "1", VisibilityPropertyOptions.Member));
            await UnityRoomService.RetryAsync(() => Room.AsHost().SavePropertiesAsync());
            SetStatus(FillWithBots ? "Bots fill empty places. You can start by yourself." : "Bots removed.");
        });
        public Task JoinAsync(string code) => RunAsync(async () =>
        {
            RequireAccount(); if (Room != null) throw new InvalidOperationException("Leave your current room before joining another.");
            SetStatus("Joining room...");
            var joined = await service.JoinAsync(code, OnlineManager.Instance.Profiles.Current.username);
            if (UnityRoomService.Property(joined, "version") != UnityRoomService.Version || !modes.Any(m => m != null && m.modeId == UnityRoomService.Property(joined, "mode")) || joined.IsLocked || UnityRoomService.Property(joined, "phase") != "room")
            { await joined.LeaveAsync(); throw new InvalidOperationException("This room has started or uses a different game version."); }
            Attach(joined);
            if (PublicMatch) await UnityRoomService.SetReadyAsync(Room, true);
            SetStatus(PublicMatch ? "Joined online match. Waiting for the host..." : "Joined. Press Ready when you are ready to play.");
        });
        void Attach(ISession room)
        {
            Room = room; originalHost = room.Host; nextRefresh = Time.unscaledTime + 12;
            publicStartTime = Time.unscaledTime + 15; autoStartIssued = false;
            roomLifetime = new CancellationTokenSource();
            Room.Changed += RoomChanged; Room.Deleted += RoomEnded; Room.RemovedFromSession += RoomEnded;
            Room.SessionHostChanged += HostChanged; Notify();
        }
        void Detach()
        {
            if (Room == null) return;
            roomLifetime?.Cancel(); roomLifetime?.Dispose(); roomLifetime = null;
            Room.Changed -= RoomChanged; Room.Deleted -= RoomEnded; Room.RemovedFromSession -= RoomEnded; Room.SessionHostChanged -= HostChanged; Room = null;
        }
        void RoomChanged() { Notify(); }
        void HostChanged(string id) { if (id != originalHost) RoomEnded(); }
        async void RoomEnded()
        {
            if (leaving || quitting) return;
            await LeaveCoreAsync(); SetStatus("The host closed the room or disconnected. Create or join another room.");
        }
        async void RefreshQuietly()
        {
            var room = Room;
            refreshing = true;
            try { if (room != null) await room.RefreshAsync(); }
            catch { if (Room == room) SetStatus("Room connection interrupted. Retry or leave the room."); }
            finally { refreshing = false; }
        }
        public Task RefreshAsync() => RunAsync(async () => { if (Room != null) await Room.RefreshAsync(); SetStatus("Room refreshed."); });
        public Task ReadyAsync() => RunAsync(async () =>
        {
            if (Room == null || IsHost || Room.IsLocked) return;
            await UnityRoomService.SetReadyAsync(Room, !UnityRoomService.IsReady(Room, Room.CurrentPlayer));
        });
        public Task SelectModeAsync(int index) => RunAsync(async () =>
        {
            if (!IsHost || PublicMatch || Room.IsLocked || index < 0 || index >= modes.Length) return;
            modes[index].ValidateConfiguration();
            if (Room.PlayerCount > modes[index].maxPlayers) throw new InvalidOperationException("There are too many players for this mode.");
            if (Room.MaxPlayers < modes[index].minPlayers) throw new InvalidOperationException("Create a new room for this mode's larger player requirement.");
            await UnityRoomService.SetModeAsync(Room.AsHost(), modes[index]); SetStatus("Mode changed. Everyone must ready up again.");
        });
        public Task StartMatchAsync() => RunAsync(async () =>
        {
            if (!IsHost || Room.IsLocked) return;
            var host = Room.AsHost();
            var cancellation = roomLifetime.Token;
            await UnityRoomService.RetryAsync(() => host.RefreshAsync()); cancellation.ThrowIfCancellationRequested();
            var selected = Mode;
            if (selected == null || Room.PlayerCount + BotCount < selected.minPlayers || Room.PlayerCount > selected.maxPlayers || !Room.Players.All(p => UnityRoomService.IsReady(Room, p)))
                throw new InvalidOperationException("Wait for the required players and everyone to be ready.");
            selected.ValidateConfiguration();
            try
            {
                host.IsLocked = true; await UnityRoomService.RetryAsync(() => host.SavePropertiesAsync()); cancellation.ThrowIfCancellationRequested();
                await UnityRoomService.RetryAsync(() => host.RefreshAsync()); cancellation.ThrowIfCancellationRequested();
                if (host.PlayerCount + BotCount < selected.minPlayers || host.PlayerCount > selected.maxPlayers || !host.Players.All(p => UnityRoomService.IsReady(host, p)))
                    throw new InvalidOperationException("The player list or ready state changed. Ready up and try again.");
                SetStatus("Connecting players...");
                await host.Network.StartRelayNetworkAsync(new RelayNetworkOptions()); cancellation.ThrowIfCancellationRequested();
                await connection.WaitForPlayersAsync(host.PlayerCount, cancellation);
                host.SetProperty("phase", new SessionProperty("playing", VisibilityPropertyOptions.Member));
                await UnityRoomService.RetryAsync(() => host.SavePropertiesAsync()); cancellation.ThrowIfCancellationRequested();
                connection.LoadMode(selected);
                SetStatus("Match started.");
            }
            catch
            {
                if (!cancellation.IsCancellationRequested)
                {
                    try { await host.Network.StopNetworkAsync(); } catch { }
                    try { host.IsLocked = false; host.SetProperty("phase", new SessionProperty("room", VisibilityPropertyOptions.Member)); await host.SavePropertiesAsync(); } catch { }
                }
                throw;
            }
        });
        public Task LeaveAsync() => RunAsync(async () => { await LeaveCoreAsync(); SetStatus("You left the room."); });
        public async Task HandleDisconnectAsync()
        {
            if (leaving || quitting || Room == null) return;
            await LeaveCoreAsync(); SetStatus("Disconnected from the host. Create or join another room.");
        }
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
            finally { await connection.ReturnToMenuAsync(); leaving = false; Notify(); }
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
        void OnApplicationQuit() { quitting = true; roomLifetime?.Cancel(); }
        void OnDestroy()
        {
            if (Instance != this) return;
            if (listening) FriendsService.Instance.MessageReceived -= ReceiveInvite;
            Detach(); Instance = null;
        }
    }
}
