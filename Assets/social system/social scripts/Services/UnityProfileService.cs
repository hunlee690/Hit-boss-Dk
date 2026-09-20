using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.CloudSave.Models.Data.Player;
using SaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;

namespace HitBoss.Social
{
    public sealed class UnityProfileService : IProfileService
    {
        readonly ICloudSaveService cloud;
        ICloudSaveService Cloud => cloud ?? CloudSaveService.Instance;
        public UnityProfileService(ICloudSaveService cloud = null) { this.cloud = cloud; }
        public const string ProfileKey = "social_profile_v1";
        public const string SearchKey = "social_username";
        public static string NormalizeName(string name) => (name ?? "").Trim().ToLowerInvariant();
        public static string BaseName(string name) => (name ?? "").Split('#')[0];

        public async Task<ProfileRecord> LoadAsync(string playerId)
        {
            var data = await Cloud.Data.Player.LoadAsync(new HashSet<string> { ProfileKey },
                new LoadOptions(new PublicReadAccessClassOptions(playerId)));
            if (!data.TryGetValue(ProfileKey, out var item)) return null;
            var profile = item.Value.GetAs<PlayerProfile>();
            if (profile == null || profile.schemaVersion != 1) throw new InvalidOperationException("This profile needs a newer game version.");
            return new ProfileRecord { playerId = playerId, profile = profile, writeLock = item.WriteLock };
        }

        public async Task SaveAsync(ProfileRecord record)
        {
            var data = new Dictionary<string, SaveItem>
            {
                { ProfileKey, new SaveItem(record.profile, record.writeLock) },
                { SearchKey, new SaveItem(NormalizeName(BaseName(record.profile.username)), null) }
            };
            var locks = await Cloud.Data.Player.SaveAsync(data, new SaveOptions(new PublicWriteAccessClassOptions()));
            record.writeLock = locks[ProfileKey];
        }

        public async Task<IReadOnlyList<SocialPlayer>> SearchAsync(string username)
        {
            string text = NormalizeName(username);
            if (text.Length < 3) throw new ArgumentException("Enter at least 3 characters of an exact username.");
            var query = new Query(new List<FieldFilter> { new FieldFilter(SearchKey, NormalizeName(BaseName(text)), FieldFilter.OpOptions.EQ, true) }, new HashSet<string> { ProfileKey });
            var results = await Cloud.Data.Player.QueryAsync(query, new QueryOptions());
            var players = new List<SocialPlayer>();
            foreach (var result in results)
            {
                var item = result.Data.FirstOrDefault(x => x.Key == ProfileKey);
                if (item == null) continue;
                var profile = item.Value.GetAs<PlayerProfile>();
                if (text.Contains("#") && NormalizeName(profile.username) != text) continue;
                players.Add(new SocialPlayer { playerId = result.Id, username = profile.username, presence = "View profile" });
            }
            return players;
        }
    }
}
