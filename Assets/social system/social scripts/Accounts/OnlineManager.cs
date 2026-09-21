using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HitBoss.Social
{
    [DefaultExecutionOrder(-100)]
    public sealed class OnlineManager : MonoBehaviour
    {
        public static OnlineManager Instance { get; private set; }
        [Tooltip("Must match the Unity Cloud environment containing the social_username index.")]
        public string environment = "production";
        [Min(0)] public int xpPerKill = 25;
        [Min(5)] public float saveInterval = 10;
        public ProfileManager Profiles { get; private set; }
        public FriendsManager Friends { get; private set; }
        public IAccountService Account { get; private set; }
        public bool Busy { get; private set; }
        public bool Ready => Profiles?.Current != null;
        public string Status { get; private set; } = "Connecting…";
        public event Action Changed;
        float nextSave;
        bool saving;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += SceneLoaded;
        }
        async void Start() { await ConnectAsync(); }
        public async Task ConnectAsync()
        {
            if (Busy) return;
            Busy = true; SetStatus("Signing in…");
            try
            {
                if (string.IsNullOrWhiteSpace(Application.cloudProjectId))
                    throw new InvalidOperationException("Online setup is not linked yet. You can still play offline.");
                Account = Account ?? new UnityAccountService();
                await Account.SignInAsync(environment);
                if (Profiles == null)
                {
                    Profiles = new ProfileManager(Account, new UnityProfileService(), Application.cloudProjectId + "." + environment);
                    Profiles.Changed += ProfileChanged;
                }
                SetStatus("Loading profile…");
                await Profiles.InitializeAsync();
                if (Friends == null)
                {
                    Friends = new FriendsManager(new UnityFriendsProvider(), Account.PlayerId);
                    Friends.Changed += Notify;
                }
                SetStatus("Connecting friends…");
                await Friends.InitializeAsync();
                SetStatus("Online");
            }
            catch (Exception e) { Report(e); }
            finally { Busy = false; Notify(); }
        }
        void Notify() => Changed?.Invoke();
        void ProfileChanged() { UpdatePlayerNames(); Notify(); }
        void SceneLoaded(Scene scene, LoadSceneMode mode) { UpdatePlayerNames(); Flush(); }
        void UpdatePlayerNames()
        {
            if (!Ready) return;
            foreach (var p in FindObjectsByType<MatchParticipant>(FindObjectsSortMode.None))
                if (!p.isAI && p.GetComponentInParent<HitBoss.Multiplayer.NetworkPlayer>() == null) { p.playerName = Profiles.Current.username; p.RefreshName(); }
        }
        public void RecordKill() { if (Ready) Profiles.RecordProgress(1, 0, xpPerKill); }
        public void RecordDeath() { if (Ready) Profiles.RecordProgress(0, 1, 0); }
        void Update() { if (Time.unscaledTime >= nextSave) { nextSave = Time.unscaledTime + saveInterval; Flush(); } }
        public async void Flush()
        {
            if (!Ready || saving || Busy) return;
            saving = true;
            try { await Profiles.FlushAsync(); }
            catch (Exception e) { Report(e); }
            finally { saving = false; }
        }
        async void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
            if (Friends == null) return;
            try { await Friends.SetOnlineAsync(!paused); if (!paused && Friends.Ready) await Friends.RefreshAsync(); }
            catch (Exception e) { Report(e); }
        }
        async void OnApplicationQuit()
        {
            // Pending progression is persisted synchronously at each score event, even if this request is interrupted.
            if (Friends != null) try { await Friends.SetOnlineAsync(false); } catch { }
        }
        public void SetStatus(string message) { Status = message; Notify(); }
        public void Report(Exception e)
        {
            Debug.LogWarning("[Social] " + e);
            SetStatus(e is ArgumentException || e is InvalidOperationException ? e.Message : "Could not reach online services. Please try again.");
        }
        void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= SceneLoaded;
            if (Profiles != null) Profiles.Changed -= ProfileChanged;
            if (Friends != null) { Friends.Changed -= Notify; Friends.Dispose(); }
            Instance = null;
        }
    }
}
