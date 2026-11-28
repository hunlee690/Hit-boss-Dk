using System;
using System.Threading.Tasks;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;

namespace HitBoss.Authentication
{
    public static class GooglePlayGamesProvider
    {
        public static async Task<string> GetServerAuthCodeAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayGamesPlatform.Activate();
            var signIn = new TaskCompletionSource<SignInStatus>();
            PlayGamesPlatform.Instance.ManuallyAuthenticate(status => signIn.TrySetResult(status));
            if (await Task.WhenAny(signIn.Task, Task.Delay(120000)) != signIn.Task)
                throw new TimeoutException("Google Play Games sign-in timed out.");
            if (await signIn.Task != SignInStatus.Success)
                throw new OperationCanceledException("Google Play Games sign-in was cancelled.");

            var codeResult = new TaskCompletionSource<string>();
            PlayGamesPlatform.Instance.RequestServerSideAccess(false, code => codeResult.TrySetResult(code));
            if (await Task.WhenAny(codeResult.Task, Task.Delay(30000)) != codeResult.Task)
                throw new TimeoutException("Google Play Games did not return an authorization code.");
            string code = await codeResult.Task;
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Google Play Games is not configured for this app yet.");
            return code;
#else
            await Task.CompletedTask;
            throw new NotSupportedException("Google Play Games sign-in is available in the Android build.");
#endif
        }
    }
}
