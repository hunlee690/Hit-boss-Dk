using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Friends;

namespace HitBoss.Social.Editor
{
    // Explicit developer checks; never run automatically in the game or create fake UI data.
    public static class SocialValidation
    {
        public static string Result { get; private set; } = "Not run";
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("FAIL: " + message); Debug.Log("[Social test] PASS: " + message); }

        [MenuItem("Tools/Hit Boss/Validate Profile Logic")]
        public static async void ValidateProfileLogic()
        {
            Result = "Running profile logic";
            try
            {
                var p = new PlayerProfile { experience = 99 }; Check(p.Level == 1 && p.XpIntoLevel == 99, "Below XP threshold");
                p.experience = 100; Check(p.Level == 2 && p.XpIntoLevel == 0, "Exact level boundary");
                p.experience = 250; Check(p.Level == 3 && p.XpIntoLevel == 0, "Multiple level boundaries");
                var account = new FakeAccount(); var store = new FakeProfiles();
                var manager = new ProfileManager(account, store, "validation-" + Guid.NewGuid().ToString("N"));
                await manager.InitializeAsync();
                manager.RecordProgress(2,1,250); Check(manager.Current.totalKills == 2 && manager.Current.Level == 3, "Unsaved progression is visible");
                store.failAfterCommit = true;
                try { await manager.FlushAsync(); } catch (TimeoutException) { }
                await manager.FlushAsync();
                Check(store.record.profile.totalKills == 2 && store.record.profile.totalDeaths == 1 && store.record.profile.experience == 250, "Uncertain-response retry does not duplicate stats");
                await Task.WhenAll(manager.FlushAsync(), manager.RenameAsync("TestName"));
                Check(manager.Current.totalKills == 2 && manager.Current.username == "TestName#0001", "Rename preserves progression");
                bool rejected = false; try { await manager.RenameAsync("<bad>"); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Invalid name rejected");
                for (int i = 0; i < 300; i++) manager.RecordProgress(1, 0, 0);
                store.failAfterCommit = true;
                try { await manager.FlushAsync(); } catch (TimeoutException) { }
                for (int i = 0; i < 6; i++) await manager.FlushAsync();
                Check(store.record.profile.totalKills == 302, "Large offline queue retries without duplicate kills");
                Result = "PASS: profile logic, level thresholds, retry idempotency, rename, validation";
            }
            catch (Exception e) { Result = e.ToString(); Debug.LogError(Result); }
        }

        [MenuItem("Tools/Hit Boss/Validate Live Social Services (Play Mode)")]
        public static async void ValidateLive()
        {
            if (!Application.isPlaying) { Result = "Enter Play mode first"; return; }
            Result = "Running live service checks";
            FriendsManager aFriends = null, bFriends = null;
            IAuthenticationService aAuth = null, bAuth = null;
            try
            {
                var a = UnityServices.Services.TryGetValue("social-validation-a", out var existingA) ? existingA : UnityServices.CreateServices("social-validation-a");
                var b = UnityServices.Services.TryGetValue("social-validation-b", out var existingB) ? existingB : UnityServices.CreateServices("social-validation-b");
                await a.InitializeAsync(new InitializationOptions().SetEnvironmentName("production").SetProfile("social-validation-a"));
                await b.InitializeAsync(new InitializationOptions().SetEnvironmentName("production").SetProfile("social-validation-b"));
                var aAccount = new UnityAccountService(a); var bAccount = new UnityAccountService(b);
                await aAccount.SignInAsync("production"); await bAccount.SignInAsync("production");
                aAuth = a.GetAuthenticationService(); bAuth = b.GetAuthenticationService();
                Check(aAccount.PlayerId != bAccount.PlayerId, "Two independent test accounts");
                var aProfile = new ProfileManager(aAccount, new UnityProfileService(a.GetCloudSaveService()), "live-tests");
                var bProfile = new ProfileManager(bAccount, new UnityProfileService(b.GetCloudSaveService()), "live-tests");
                await aProfile.InitializeAsync(); await bProfile.InitializeAsync();
                await aProfile.RenameAsync("SocialTestA"); await bProfile.RenameAsync("SocialTestB");
                int oldKills = aProfile.Current.totalKills, oldDeaths = aProfile.Current.totalDeaths, oldXp = aProfile.Current.experience;
                aProfile.RecordProgress(1, 1, 25); await aProfile.FlushAsync();
                var reloaded = await aProfile.VisitAsync(aAccount.PlayerId);
                Check(reloaded.profile.totalKills == oldKills + 1 && reloaded.profile.totalDeaths == oldDeaths + 1 && reloaded.profile.experience == oldXp + 25, "Cloud stats survive reload");
                var search = await aProfile.SearchAsync("socialtestb");
                Check(search.Any(p => p.playerId == bAccount.PlayerId), "Case-insensitive username search");
                var publicProfile = await aProfile.VisitAsync(bAccount.PlayerId);
                Check(publicProfile.profile.username == bProfile.Current.username, "Visit another player's public profile");
                aFriends = new FriendsManager(new UnityFriendsProvider(a.GetFriendsService()), aAccount.PlayerId);
                bFriends = new FriendsManager(new UnityFriendsProvider(b.GetFriendsService()), bAccount.PlayerId);
                await aFriends.InitializeAsync(); await bFriends.InitializeAsync();
                if (aFriends.Contains(SocialList.Friends,bAccount.PlayerId)) await aFriends.RemoveAsync(bAccount.PlayerId);
                if (aFriends.Contains(SocialList.Sent,bAccount.PlayerId)) await aFriends.CancelAsync(bAccount.PlayerId);
                if (bFriends.Contains(SocialList.Sent,aAccount.PlayerId)) await bFriends.CancelAsync(aAccount.PlayerId);
                await aFriends.RefreshAsync(); await bFriends.RefreshAsync();
                await aFriends.AddAsync(bAccount.PlayerId); await bFriends.RefreshAsync();
                Check(aFriends.Contains(SocialList.Sent,bAccount.PlayerId) && bFriends.Contains(SocialList.Requests,aAccount.PlayerId), "Outgoing and incoming friend request");
                await bFriends.DeclineAsync(aAccount.PlayerId); await aFriends.RefreshAsync();
                Check(!aFriends.Contains(SocialList.Sent,bAccount.PlayerId), "Decline request");
                await aFriends.AddAsync(bAccount.PlayerId); await aFriends.CancelAsync(bAccount.PlayerId); await bFriends.RefreshAsync();
                Check(!bFriends.Contains(SocialList.Requests,aAccount.PlayerId), "Cancel sent request");
                await aFriends.AddAsync(bAccount.PlayerId); await bFriends.AddAsync(aAccount.PlayerId);
                await aFriends.RefreshAsync(); await bFriends.RefreshAsync();
                Check(aFriends.Contains(SocialList.Friends,bAccount.PlayerId) && bFriends.Contains(SocialList.Friends,aAccount.PlayerId), "Accept creates mutual friendship");
                await bFriends.SetOnlineAsync(true); await aFriends.RefreshAsync();
                Check(aFriends.GetPlayers(SocialList.Friends).First(p => p.playerId == bAccount.PlayerId).online, "Online presence");
                await bFriends.SetOnlineAsync(false); await aFriends.RefreshAsync();
                Check(!aFriends.GetPlayers(SocialList.Friends).First(p => p.playerId == bAccount.PlayerId).online, "Offline presence");
                await aFriends.RemoveAsync(bAccount.PlayerId); await bFriends.RefreshAsync();
                Check(!bFriends.Contains(SocialList.Friends,aAccount.PlayerId), "Remove friend");
                Result = "PASS: two accounts, cloud profiles/stats, search, profile visit, send/decline/cancel/accept requests, online/offline presence, remove friend";
                Debug.Log("[Social test] " + Result);
            }
            catch (Exception e) { Result = e.ToString(); Debug.LogError("[Social test] " + Result); }
            finally
            {
                if (aFriends != null) { try { await aFriends.SetOnlineAsync(false); } catch { } aFriends.Dispose(); }
                if (bFriends != null) { try { await bFriends.SetOnlineAsync(false); } catch { } bFriends.Dispose(); }
                aAuth?.SignOut(); bAuth?.SignOut();
            }
        }

        class FakeAccount : IAccountService
        {
            public string PlayerId => "test-" + id;
            readonly string id = Guid.NewGuid().ToString("N");
            public Task SignInAsync(string environment) => Task.CompletedTask;
            public Task<string> GetNameAsync() => Task.FromResult("Player#0001");
            public Task<string> RenameAsync(string name) => Task.FromResult(name + "#0001");
        }
        class FakeProfiles : IProfileService
        {
            public ProfileRecord record;
            public bool failAfterCommit;
            public Task<ProfileRecord> LoadAsync(string id) => Task.FromResult(record == null ? null : new ProfileRecord { playerId=id,profile=record.profile.Copy(),writeLock=record.writeLock });
            public Task SaveAsync(ProfileRecord value)
            {
                record = new ProfileRecord { playerId=value.playerId, profile=value.profile.Copy(),writeLock="test" };
                if(failAfterCommit) { failAfterCommit=false; throw new TimeoutException("Simulated lost response after commit"); }
                return Task.CompletedTask;
            }
            public Task<IReadOnlyList<SocialPlayer>> SearchAsync(string name) => Task.FromResult<IReadOnlyList<SocialPlayer>>(Array.Empty<SocialPlayer>());
        }
    }
}
