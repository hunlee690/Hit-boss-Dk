using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HitBoss.Social
{
    public interface IAccountService
    {
        string PlayerId { get; }
        Task SignInAsync(string environment);
        Task<string> GetNameAsync();
        Task<string> RenameAsync(string name);
    }
    public interface IProfileService
    {
        Task<ProfileRecord> LoadAsync(string playerId);
        Task SaveAsync(ProfileRecord record);
        Task<IReadOnlyList<SocialPlayer>> SearchAsync(string username);
    }
    public interface IFriendsProvider : IDisposable
    {
        event Action Changed;
        Task InitializeAsync();
        IReadOnlyList<SocialPlayer> GetPlayers(SocialList list);
        Task AddAsync(string playerId);
        Task DeclineAsync(string playerId);
        Task CancelAsync(string playerId);
        Task RemoveAsync(string playerId);
        Task RefreshAsync();
        Task SetOnlineAsync(bool online);
    }
}
