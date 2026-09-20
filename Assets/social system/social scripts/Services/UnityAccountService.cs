using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;

namespace HitBoss.Social
{
    public sealed class UnityAccountService : IAccountService
    {
        readonly IUnityServices services;
        IAuthenticationService Auth => services == null ? AuthenticationService.Instance : services.GetAuthenticationService();
        public UnityAccountService(IUnityServices services = null) { this.services = services; }
        public string PlayerId => Auth.PlayerId;
        public async Task SignInAsync(string environment)
        {
            if ((services?.State ?? UnityServices.State) != ServicesInitializationState.Initialized)
                await (services ?? UnityServices.Instance).InitializeAsync(new InitializationOptions().SetEnvironmentName(environment));
            // Reuses Unity's cached session. Never clears tokens or silently creates a new account on an error.
            if (!Auth.IsSignedIn)
                await Auth.SignInAnonymouslyAsync();
        }
        public Task<string> GetNameAsync() => Auth.GetPlayerNameAsync();
        public Task<string> RenameAsync(string name) => Auth.UpdatePlayerNameAsync(name);
    }
}
