using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitBoss.Authentication
{
    [DisallowMultipleComponent]
    public sealed class AuthenticationUI : MonoBehaviour
    {
        [SerializeField] CanvasGroup root;
        [SerializeField] GameObject loadingPanel;
        [SerializeField] GameObject loginPanel;
        [SerializeField] Slider progressBar;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Button playGamesButton;
        [SerializeField] Button googleButton;
        [SerializeField] Button guestButton;
        [SerializeField] Button retryButton;
        [SerializeField] Button logoutButton;
        [SerializeField] float fadeSeconds = .2f;
        float logoutConfirmUntil;
        bool hidden;

        void Start()
        {
            Bind(playGamesButton, () => Run(() => AuthenticationManager.Instance.SignInGooglePlayGamesAsync()));
            Bind(googleButton, () => Run(() => AuthenticationManager.Instance.SignInGoogleAsync()));
            Bind(guestButton, () => Run(() => AuthenticationManager.Instance.SignInGuestAsync()));
            Bind(retryButton, () => Run(() => AuthenticationManager.Instance.RetryAsync()));
            Bind(logoutButton, Logout);
            if (AuthenticationManager.Instance != null)
            {
                AuthenticationManager.Instance.Changed += Refresh;
                AuthenticationManager.Instance.Authenticated += Hide;
                AuthenticationManager.Instance.SignedOut += Show;
            }
            Refresh();
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        async void Run(Func<System.Threading.Tasks.Task> action)
        {
            try { await action(); }
            catch { /* Manager exposes a safe message in the UI. */ }
        }

        void Logout()
        {
            bool confirmed = Time.unscaledTime <= logoutConfirmUntil;
            if (!confirmed) logoutConfirmUntil = Time.unscaledTime + 8f;
            Run(async () =>
            {
                try { await AuthenticationManager.Instance.LogoutAsync(confirmed); }
                catch (InvalidOperationException)
                {
                    if (statusText != null) statusText.text = "Guest progress is not linked. Tap LOGOUT again to confirm.";
                }
            });
        }

        void Refresh()
        {
            var manager = AuthenticationManager.Instance;
            if (manager == null) return;
            if (statusText != null) statusText.text = manager.Status;
            if (progressBar != null) progressBar.value = manager.Progress;
            bool busy = manager.State == AuthenticationState.Initializing || manager.State == AuthenticationState.SigningIn;
            if (loadingPanel != null) loadingPanel.SetActive(busy);
            if (loginPanel != null) loginPanel.SetActive(!busy && manager.State != AuthenticationState.Authenticated);
            if (retryButton != null) retryButton.gameObject.SetActive(manager.State == AuthenticationState.Error);
            if (playGamesButton != null) playGamesButton.interactable = !busy;
            if (googleButton != null) googleButton.interactable = !busy;
            if (guestButton != null) guestButton.interactable = !busy;
            if (manager.Ready) Hide(); else Show();
        }

        public void Show()
        {
            hidden = false;
            if (root == null) return;
            root.gameObject.SetActive(true);
            root.alpha = 1;
            root.interactable = root.blocksRaycasts = true;
        }

        public void Hide()
        {
            if (hidden || root == null) return;
            hidden = true;
            StartCoroutine(FadeOut());
        }

        IEnumerator FadeOut()
        {
            float start = root.alpha;
            for (float t = 0; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                root.alpha = Mathf.Lerp(start, 0, t / Mathf.Max(.01f, fadeSeconds));
                yield return null;
            }
            root.alpha = 0;
            root.interactable = root.blocksRaycasts = false;
            root.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (AuthenticationManager.Instance == null) return;
            AuthenticationManager.Instance.Changed -= Refresh;
            AuthenticationManager.Instance.Authenticated -= Hide;
            AuthenticationManager.Instance.SignedOut -= Show;
        }
    }
}
