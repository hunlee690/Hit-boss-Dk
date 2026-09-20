using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HitBoss.Social
{
    public sealed class FriendsManager : IDisposable
    {
        readonly IFriendsProvider provider;
        readonly string self;
        public bool Ready { get; private set; }
        public event Action Changed;
        public FriendsManager(IFriendsProvider provider, string playerId)
        { this.provider = provider; self = playerId; provider.Changed += OnChanged; }
        void OnChanged() => Changed?.Invoke();
        public async Task InitializeAsync() { await provider.InitializeAsync(); Ready = true; Changed?.Invoke(); }
        public IReadOnlyList<SocialPlayer> GetPlayers(SocialList list) => Ready ? provider.GetPlayers(list) : Array.Empty<SocialPlayer>();
        public bool Contains(SocialList list, string id) => GetPlayers(list).Any(p => p.playerId == id);
        public async Task AddAsync(string id)
        {
            if (id == self) throw new ArgumentException("That is your own profile.");
            if (Contains(SocialList.Friends, id)) return;
            if (Contains(SocialList.Sent, id)) return;
            await provider.AddAsync(id);
        }
        public Task DeclineAsync(string id) => provider.DeclineAsync(id);
        public Task CancelAsync(string id) => provider.CancelAsync(id);
        public Task RemoveAsync(string id) => provider.RemoveAsync(id);
        public Task RefreshAsync() => provider.RefreshAsync();
        public Task SetOnlineAsync(bool online) => Ready ? provider.SetOnlineAsync(online) : Task.CompletedTask;
        public void Dispose() { provider.Changed -= OnChanged; provider.Dispose(); }
    }
}
