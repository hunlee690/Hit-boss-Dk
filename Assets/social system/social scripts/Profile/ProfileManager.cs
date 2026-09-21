using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace HitBoss.Social
{
    public sealed class ProfileManager
    {
        [Serializable] class PendingProgress { public List<ProgressBatch> batches = new List<ProgressBatch>(); }
        readonly IAccountService account;
        readonly IProfileService service;
        readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        readonly string cacheScope;
        PendingProgress pending;
        PlayerProfile saved;
        public PlayerProfile Current { get; private set; }
        public event Action Changed;
        string PendingKey => "HitBoss.Progress." + cacheScope + "." + account.PlayerId;

        public ProfileManager(IAccountService account, IProfileService service, string scope)
        { this.account = account; this.service = service; cacheScope = scope; }

        public async Task InitializeAsync()
        {
            await gate.WaitAsync();
            try
            {
                var record = await service.LoadAsync(account.PlayerId);
                var name = await account.GetNameAsync();
                if (record == null)
                    record = new ProfileRecord { playerId = account.PlayerId, profile = new PlayerProfile { gamePlayerId = Guid.NewGuid().ToString("N"), username = name } };
                else record.profile.username = name;
                if (!record.profile.currencyInitialized)
                {
                    record.profile.coins = 300;
                    record.profile.gems = 100;
                    record.profile.currencyInitialized = true;
                }
                // Also republishes the indexed name after a new index is configured.
                await service.SaveAsync(record);
                saved = record.profile;
                pending = JsonUtility.FromJson<PendingProgress>(PlayerPrefs.GetString(PendingKey, "{}")) ?? new PendingProgress();
                if (pending.batches == null) pending.batches = new List<ProgressBatch>();
                UpdateView();
            }
            finally { gate.Release(); }
            await FlushAsync();
        }

        public async Task RenameAsync(string input)
        {
            string name = (input ?? "").Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_]{3,20}$"))
                throw new ArgumentException("Use 3–20 letters, numbers, or underscores.");
            await gate.WaitAsync();
            try
            {
                string actualName = await account.RenameAsync(name);
                var record = await service.LoadAsync(account.PlayerId);
                if (record == null) throw new InvalidOperationException("Your profile could not be loaded. Please reconnect.");
                record.profile.username = actualName;
                await service.SaveAsync(record);
                saved = record.profile;
                UpdateView();
            }
            finally { gate.Release(); }
        }

        // Development progression. Replace the source of these results with host/server-validated
        // match results when gameplay networking is introduced; do not use for a ranked economy.
        public void RecordProgress(int kills, int deaths, int xp)
        {
            if (saved == null) return;
            if (kills < 0 || deaths < 0 || xp < 0) throw new ArgumentOutOfRangeException("Progress cannot be negative.");
            pending.batches.Add(new ProgressBatch { kills = kills, deaths = deaths, xp = xp });
            PersistPending();
            UpdateView();
        }
        public void RecordCurrency(int coins, int gems)
        {
            if (saved == null) return;
            if (coins < 0 || gems < 0) throw new ArgumentOutOfRangeException("Currency rewards cannot be negative.");
            pending.batches.Add(new ProgressBatch { coins = coins, gems = gems });
            PersistPending();
            UpdateView();
        }
        public Task<ProfileRecord> VisitAsync(string id) => service.LoadAsync(id);
        public Task<IReadOnlyList<SocialPlayer>> SearchAsync(string name) => service.SearchAsync(name);

        public async Task FlushAsync()
        {
            if (saved == null || pending.batches.Count == 0) return;
            await gate.WaitAsync();
            try
            {
                // Read latest before merging deltas; write locks reject concurrent overwrites.
                var record = await service.LoadAsync(account.PlayerId);
                if (record == null) throw new InvalidOperationException("Profile unavailable. Progress remains saved on this device.");
                // Keep each uncertain-response retry entirely inside the retained ID window.
                var sent = pending.batches.Take(64).ToArray();
                foreach (var batch in sent) Apply(record.profile, batch);
                await service.SaveAsync(record);
                saved = record.profile;
                var ids = new HashSet<string>(sent.Select(b => b.id));
                pending.batches.RemoveAll(b => ids.Contains(b.id));
                PersistPending();
                UpdateView();
            }
            finally { gate.Release(); }
        }
        static void Apply(PlayerProfile profile, ProgressBatch batch)
        {
            if (profile.appliedBatches == null) profile.appliedBatches = new List<string>();
            if (profile.appliedBatches.Contains(batch.id)) return;
            profile.totalKills = (int)Math.Min(int.MaxValue, (long)profile.totalKills + batch.kills);
            profile.totalDeaths = (int)Math.Min(int.MaxValue, (long)profile.totalDeaths + batch.deaths);
            profile.experience = (int)Math.Min(int.MaxValue, (long)profile.experience + batch.xp);
            profile.coins = (int)Math.Min(int.MaxValue, (long)profile.coins + batch.coins);
            profile.gems = (int)Math.Min(int.MaxValue, (long)profile.gems + batch.gems);
            profile.appliedBatches.Add(batch.id);
            // Retain IDs to make a retry after an uncertain response idempotent.
            if (profile.appliedBatches.Count > 256) profile.appliedBatches.RemoveAt(0);
        }
        void PersistPending() { PlayerPrefs.SetString(PendingKey, JsonUtility.ToJson(pending)); PlayerPrefs.Save(); }
        void UpdateView()
        {
            Current = saved.Copy();
            foreach (var batch in pending.batches) Apply(Current, batch);
            Changed?.Invoke();
        }
    }
}
