using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;

namespace HitBoss.Multiplayer
{
    // Only this adapter knows how Unity stores room and ready data.
    public sealed class UnityRoomService
    {
        public const string Version = "1";
        readonly IMultiplayerService service;
        public UnityRoomService(IMultiplayerService service = null) { this.service = service ?? MultiplayerService.Instance; }
        public Task<IHostSession> CreateAsync(MultiplayerMode mode, string username) => service.CreateSessionAsync(new SessionOptions
        {
            Name = "Private room", Type = "hitboss-private", IsPrivate = true, MaxPlayers = mode.maxPlayers,
            PlayerProperties = PlayerData(username),
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                { "version", new SessionProperty(Version, VisibilityPropertyOptions.Member) },
                { "mode", new SessionProperty(mode.modeId, VisibilityPropertyOptions.Member) },
                { "revision", new SessionProperty(Guid.NewGuid().ToString("N"), VisibilityPropertyOptions.Member) },
                { "phase", new SessionProperty("room", VisibilityPropertyOptions.Member) }
            }
        });
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
