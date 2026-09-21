using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;

namespace HitBoss.Multiplayer
{
    // Only this adapter knows how Unity stores room and ready data.
    public sealed class UnityRoomService
    {
        public const string Version = "3";
        readonly IMultiplayerService service;
        public UnityRoomService(IMultiplayerService service = null) { this.service = service ?? MultiplayerService.Instance; }
        public static async Task RetryAsync(Func<Task> action)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { await action(); return; }
                catch (SessionException e) when (attempt < 3 && e.Error == SessionError.RateLimitExceeded)
                { await Task.Delay(1500 * (attempt + 1)); }
            }
        }
        public Task<IHostSession> CreateAsync(MultiplayerMode mode, string username, bool publicMatch = false) => service.CreateSessionAsync(new SessionOptions
        {
            Name = publicMatch ? "Online match" : "Private room", Type = "hitboss-private", IsPrivate = !publicMatch, MaxPlayers = mode.maxPlayers,
            PlayerProperties = PlayerData(username),
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                { "version", new SessionProperty(Version, VisibilityPropertyOptions.Member) },
                { "mode", new SessionProperty(mode.modeId, VisibilityPropertyOptions.Member) },
                { "revision", new SessionProperty(Guid.NewGuid().ToString("N"), VisibilityPropertyOptions.Member) },
                { "phase", new SessionProperty("room", VisibilityPropertyOptions.Member) },
                { "public", new SessionProperty(publicMatch ? "1" : "0", VisibilityPropertyOptions.Member) },
                { "bots", new SessionProperty(publicMatch ? "1" : "0", VisibilityPropertyOptions.Member) },
                { "queue", new SessionProperty(publicMatch ? "hitboss-" + Version + "-" + mode.modeId : "private", VisibilityPropertyOptions.Public, PropertyIndex.String1) }
            }
        });
        public async Task<ISession> FindPublicAsync(MultiplayerMode mode, string username)
        {
            var results = await service.QuerySessionsAsync(new QuerySessionsOptions
            {
                Count = 20,
                FilterOptions = new List<FilterOption>
                {
                    new FilterOption(FilterField.StringIndex1, "hitboss-" + Version + "-" + mode.modeId, FilterOperation.Equal),
                    new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater),
                    new FilterOption(FilterField.IsLocked, "false", FilterOperation.Equal)
                }
            });
            foreach (var candidate in results.Sessions)
            {
                ISession joined;
                try { joined = await service.JoinSessionByIdAsync(candidate.Id, new JoinSessionOptions { Type = "hitboss-private", PlayerProperties = PlayerData(username) }); }
                catch (SessionException) { continue; }
                if (Property(joined, "version") == Version && Property(joined, "mode") == mode.modeId && Property(joined, "public") == "1" && !joined.IsLocked && Property(joined, "phase") == "room") return joined;
                await joined.LeaveAsync();
            }
            return null;
        }
        public Task<ISession> JoinAsync(string code, string username)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length < 4 || code.Length > 12) throw new ArgumentException("Enter a valid room code.");
            foreach (var c in code) if (!char.IsLetterOrDigit(c)) throw new ArgumentException("Room codes use letters and numbers.");
            return service.JoinSessionByCodeAsync(code, new JoinSessionOptions { Type = "hitboss-private", PlayerProperties = PlayerData(username) });
        }
        static Dictionary<string, PlayerProperty> PlayerData(string name) => new Dictionary<string, PlayerProperty>
        {
            { "name", new PlayerProperty(name, VisibilityPropertyOptions.Member) },
            { "ready", new PlayerProperty("", VisibilityPropertyOptions.Member) }
        };
        public static string Property(ISession room, string key) => room != null && room.Properties.TryGetValue(key, out var p) ? p.Value : "";
        public static string PlayerValue(IReadOnlyPlayer player, string key) => player.Properties.TryGetValue(key, out var p) ? p.Value : "";
        public static bool IsReady(ISession room, IReadOnlyPlayer player) => player.Id == room.Host || !string.IsNullOrEmpty(Property(room, "revision")) && PlayerValue(player, "ready") == Property(room, "revision");
        public static async Task SetReadyAsync(ISession room, bool ready)
        {
            room.CurrentPlayer.SetProperty("ready", new PlayerProperty(ready ? Property(room, "revision") : "", VisibilityPropertyOptions.Member));
            await room.SaveCurrentPlayerDataAsync();
        }
        public static async Task SetModeAsync(IHostSession room, MultiplayerMode mode)
        {
            room.SetProperty("mode", new SessionProperty(mode.modeId, VisibilityPropertyOptions.Member));
            room.SetProperty("revision", new SessionProperty(Guid.NewGuid().ToString("N"), VisibilityPropertyOptions.Member));
            await room.SavePropertiesAsync();
        }
    }
}
