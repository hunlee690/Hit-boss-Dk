using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using HitBoss.Social;

namespace HitBoss.Authentication
{
    public enum AuthenticationState
    {
        Initializing,
        AwaitingLogin,
        SigningIn,
        Authenticated,
        Error
    }

    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class AuthenticationManager : MonoBehaviour
    {
        public static AuthenticationManager Instance { get; private set; }

        [SerializeField] string environment = "production";
        public AuthenticationState State { get; private set; } = AuthenticationState.Initializing;
        public string Status { get; private set; } = "Starting online services...";
        public float Progress { get; private set; }
        public bool Ready => State == AuthenticationState.Authenticated &&
            UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsAuthorized;
        public bool HasLinkedAccount
        {
            get
            {
                if (UnityServices.State != ServicesInitializationState.Initialized ||
                    !AuthenticationService.Instance.IsAuthorized || AuthenticationService.Instance.PlayerInfo == null) return false;
                var info = AuthenticationService.Instance.PlayerInfo;
                return !string.IsNullOrEmpty(info.GetGoogleId()) || !string.IsNullOrEmpty(info.GetGooglePlayGamesId());
            }
        }
        public string PlayerId => Ready ? AuthenticationService.Instance.PlayerId : "";

        public event Action Changed;
        public event Action Authenticated;
        public event Action SignedOut;

        Task initialization;
        bool authEventsBound;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
            try { await InitializeAndRestoreAsync(); }
            catch (Exception e) { Fail(e); }
        }

        public Task InitializeAsync()
        {
            if (initialization == null || initialization.IsFaulted || initialization.IsCanceled)
                initialization = InitializeCoreAsync();
            return initialization;
        }

        public async Task RetryAsync()
        {
            initialization = null;
            await InitializeAndRestoreAsync();
        }

        async Task InitializeCoreAsync()
        {
            Set(AuthenticationState.Initializing, "Connecting to game services...", .15f);
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions().SetEnvironmentName(environment);
#if UNITY_EDITOR
                options.SetProfile("hitboss_" + Hash128.Compute(Application.dataPath).ToString().Substring(0, 12));
#endif
                await UnityServices.InitializeAsync(options);
            }
            BindAuthEvents();
            Set(AuthenticationState.Initializing, "Checking your saved session...", .55f);
        }

        async Task InitializeAndRestoreAsync()
        {
            await InitializeAsync();
            var auth = AuthenticationService.Instance;
            if (auth.IsSignedIn && !auth.IsExpired)
            {
                FinishAuthentication("Welcome back.");
                return;
            }
            if (auth.IsExpired) auth.SignOut(false);
            if (auth.SessionTokenExists)
            {
                Set(AuthenticationState.SigningIn, "Restoring your account...", .75f);
                try
                {
                    // Unity restores the cached provider identity; it does not create a new guest here.
                    await auth.SignInAnonymouslyAsync();
                    FinishAuthentication("Account restored.");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Authentication] Cached session restore failed: " + e.Message);
                }
            }
            Set(AuthenticationState.AwaitingLogin, "Choose how you want to continue.", 1f);
        }

        public async Task SignInGuestAsync()
        {
            await RunSignIn(async () =>
            {
                var auth = AuthenticationService.Instance;
                if (!auth.IsSignedIn) await auth.SignInAnonymouslyAsync();
            }, "Guest account ready. Link Google later to protect your progress.");
        }

        public async Task SignInGooglePlayGamesAsync()
        {
            await RunSignIn(async () =>
            {
                string code = await GooglePlayGamesProvider.GetServerAuthCodeAsync();
                await LinkOrSwitchAsync(
                    () => AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(code),
                    () => AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(code));
            }, "Google Play Games connected.");
        }

        public async Task SignInGoogleAsync()
        {
            await RunSignIn(async () =>
            {
                string token = await GoogleSignInProvider.GetIdTokenAsync();
                try
                {
                    await LinkOrSwitchAsync(
                        () => AuthenticationService.Instance.LinkWithGoogleAsync(token),
                        () => AuthenticationService.Instance.SignInWithGoogleAsync(token));
                }
                finally { token = null; }
            }, "Google account connected.");
        }

        async Task RunSignIn(Func<Task> action, string success)
        {
            if (State == AuthenticationState.SigningIn) return;
            try
            {
                await InitializeAsync();
                Set(AuthenticationState.SigningIn, "Signing in...", .8f);
                await action();
                FinishAuthentication(success);
            }
            catch (Exception e) { Fail(e, true); }
        }

        async Task LinkOrSwitchAsync(Func<Task> link, Func<Task> signIn)
        {
            var auth = AuthenticationService.Instance;
            if (auth.IsSignedIn && !HasLinkedAccount)
            {
                try
                {
                    await link();
                    return;
                }
                catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                {
                    // The selected provider belongs to another cloud account. Switch to it safely.
                }
            }
            if (auth.IsSignedIn) auth.SignOut(false);
            await signIn();
        }

        public async Task LogoutAsync(bool confirmGuestLoss = false)
        {
            await InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) return;
            if (!HasLinkedAccount && !confirmGuestLoss)
                throw new InvalidOperationException("This guest account is not linked. Tap logout again to confirm losing access on this device.");

            Set(AuthenticationState.SigningIn, "Saving before logout...", .8f);
            if (OnlineManager.Instance?.Ready == true)
                await OnlineManager.Instance.Profiles.FlushAsync();
            AuthenticationService.Instance.SignOut(true);
            Set(AuthenticationState.AwaitingLogin, "Signed out. Choose an account to continue.", 1f);
            SignedOut?.Invoke();
        }

        void FinishAuthentication(string message)
        {
            Set(AuthenticationState.Authenticated, message, 1f);
            Authenticated?.Invoke();
        }

        void BindAuthEvents()
        {
            if (authEventsBound) return;
            AuthenticationService.Instance.Expired += HandleSessionEnded;
            AuthenticationService.Instance.SignedOut += HandleSessionEnded;
            authEventsBound = true;
        }

        void HandleSessionEnded()
        {
            Set(AuthenticationState.AwaitingLogin, "Your session ended. Sign in again to continue.", 1f);
            SignedOut?.Invoke();
        }

        void Set(AuthenticationState state, string message, float progress)
        {
            State = state;
            Status = message;
            Progress = Mathf.Clamp01(progress);
            Changed?.Invoke();
        }

        void Fail(Exception e, bool returnToChoice = false)
        {
            Debug.LogWarning("[Authentication] " + e);
            string message = FriendlyError(e);
            Set(returnToChoice ? AuthenticationState.AwaitingLogin : AuthenticationState.Error, message, 1f);
        }

        static string FriendlyError(Exception e)
        {
            if (e is OperationCanceledException) return "Sign-in was cancelled. Your progress is unchanged.";
            if (e is TimeoutException) return "Sign-in timed out. Check your connection and try again.";
            if (e is InvalidOperationException) return e.Message;
            if (Application.internetReachability == NetworkReachability.NotReachable)
                return "No internet connection. Reconnect and try again.";
            return "Could not sign in right now. Please try again.";
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            if (authEventsBound && UnityServices.State == ServicesInitializationState.Initialized)
            {
                AuthenticationService.Instance.Expired -= HandleSessionEnded;
                AuthenticationService.Instance.SignedOut -= HandleSessionEnded;
            }
            Instance = null;
        }
    }
}
