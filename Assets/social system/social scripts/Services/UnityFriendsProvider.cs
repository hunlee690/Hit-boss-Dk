using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using Unity.Services.Friends.Options;

namespace HitBoss.Social
{
    public sealed class UnityFriendsProvider : IFriendsProvider
    {
        readonly IFriendsService friends;
        IFriendsService Service => friends ?? FriendsService.Instance;
        public UnityFriendsProvider(IFriendsService friends = null) { this.friends = friends; }
        public event Action Changed;
        bool subscribed;
        public async Task InitializeAsync()
        {
            await Service.InitializeAsync(new InitializeOptions().WithMemberPresence(true).WithMemberProfile(true).WithEvents(true));
            if (!subscribed)
            {
                Service.RelationshipAdded += Added;
                Service.RelationshipDeleted += Deleted;
                Service.PresenceUpdated += Presence;
                subscribed = true;
            }
            await SetOnlineAsync(true);
        }
        void Added(IRelationshipAddedEvent e) => Changed?.Invoke();
        void Deleted(IRelationshipDeletedEvent e) => Changed?.Invoke();
        void Presence(IPresenceUpdatedEvent e) => Changed?.Invoke();
        public IReadOnlyList<SocialPlayer> GetPlayers(SocialList list)
        {
            var relationships = list == SocialList.Requests ? Service.IncomingFriendRequests :
                list == SocialList.Sent ? Service.OutgoingFriendRequests : Service.Friends;
            return relationships.Select(r => new SocialPlayer
            {
                playerId = r.Member.Id,
                username = r.Member.Profile?.Name ?? "Player",
                online = r.Member.Presence != null && (r.Member.Presence.Availability == Availability.Online || r.Member.Presence.Availability == Availability.Away || r.Member.Presence.Availability == Availability.Busy),
                presence = r.Member.Presence == null || r.Member.Presence.Availability == Availability.Unknown ? "Status unavailable" :
                    r.Member.Presence.Availability == Availability.Invisible ? "Offline" : r.Member.Presence.Availability.ToString()
            }).OrderByDescending(p => p.online).ThenBy(p => p.username).ToList();
        }
        public async Task AddAsync(string id) { await Service.AddFriendAsync(id); Changed?.Invoke(); }
        public async Task DeclineAsync(string id) { await Service.DeleteIncomingFriendRequestAsync(id); Changed?.Invoke(); }
        public async Task CancelAsync(string id) { await Service.DeleteOutgoingFriendRequestAsync(id); Changed?.Invoke(); }
        public async Task RemoveAsync(string id) { await Service.DeleteFriendAsync(id); Changed?.Invoke(); }
        public async Task RefreshAsync() { await Service.ForceRelationshipsRefreshAsync(); Changed?.Invoke(); }
        public Task SetOnlineAsync(bool online) => Service.SetPresenceAvailabilityAsync(online ? Availability.Online : Availability.Offline);
        public void Dispose()
        {
            if (!subscribed) return;
            Service.RelationshipAdded -= Added;
            Service.RelationshipDeleted -= Deleted;
            Service.PresenceUpdated -= Presence;
            subscribed = false;
        }
    }
}
