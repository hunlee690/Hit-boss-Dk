using System;
using System.Threading.Tasks;
using UnityEngine;

namespace HitBoss.Authentication
{
    public sealed class GoogleSignInProvider : MonoBehaviour
    {
        [Serializable] sealed class Settings { public string webClientId; }
        static GoogleSignInProvider instance;
        TaskCompletionSource<string> pending;

        public static async Task<string> GetIdTokenAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var asset = Resources.Load<TextAsset>("HitBoss/GoogleSignIn");
            var settings = asset != null ? JsonUtility.FromJson<Settings>(asset.text) : null;
            if (settings == null || string.IsNullOrWhiteSpace(settings.webClientId))
                throw new InvalidOperationException("Google Sign-In needs its Web Client ID in HitBoss/GoogleSignIn.json.");
            if (instance == null)
            {
                var owner = new GameObject("GoogleSignInManager");
                instance = owner.AddComponent<GoogleSignInProvider>();
                DontDestroyOnLoad(owner);
            }
            if (instance.pending != null) throw new InvalidOperationException("Google Sign-In is already open.");
            instance.pending = new TaskCompletionSource<string>();
            try
            {
                using (var helper = new AndroidJavaClass("com.google.mobilesiwgunity.GoogleSignInHelper"))
                {
                    helper.CallStatic("initialize", settings.webClientId);
                    helper.CallStatic("attemptSignIn");
                }
                if (await Task.WhenAny(instance.pending.Task, Task.Delay(120000)) != instance.pending.Task)
                    throw new TimeoutException("Google Sign-In timed out.");
                return await instance.pending.Task;
            }
            finally { instance.pending = null; }
#else
            await Task.CompletedTask;
            throw new NotSupportedException("Google Sign-In is available in the Android build.");
#endif
        }

        [UnityEngine.Scripting.Preserve]
        public void HandleSignInSuccess(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) pending?.TrySetException(new InvalidOperationException("Google returned an empty sign-in token."));
            else pending?.TrySetResult(token);
        }

        [UnityEngine.Scripting.Preserve]
        public void HandleSignInError(string error)
        {
            pending?.TrySetException(new OperationCanceledException("Google Sign-In was cancelled."));
        }
    }
}
