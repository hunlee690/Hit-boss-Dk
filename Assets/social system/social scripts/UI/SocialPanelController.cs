using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitBoss.Social
{
    public sealed class SocialPanelController : MonoBehaviour
    {
        [Header("Existing profile panel")]
        public Button ownProfileButton;
        public TMP_Text ownName;
        public TMP_Text ownLevel;
        public TMP_Text ownXp;
        public Image ownProgress;
        public TMP_InputField existingNameInput;
        public Button saveExistingNameButton;
        [Header("Social panel")]
        public TMP_Text statusText;
        public TMP_Text emptyText;
        public TMP_InputField searchInput;
        public Button searchButton;
        public Button friendsButton;
        public Button requestsButton;
        public Button sentButton;
        public Button refreshButton;
        public TMP_Text requestsLabel;
        public Transform content;
        public SocialPlayerRow rowPrefab;
        [Header("Profile viewer")]
        public GameObject profileWindow;
        public TMP_Text profileTitle;
        public TMP_Text profileStats;
        public TMP_Text profileNote;
        public TMP_InputField renameInput;
        public Button renameButton;
        public Button closeProfileButton;
        public Button profileFriendButton;
        public TMP_Text profileFriendLabel;
        OnlineManager online;
        SocialList selected;
        IReadOnlyList<SocialPlayer> searchResults = Array.Empty<SocialPlayer>();
        string viewedId;
        bool busy;
        int searchVersion;
        int visitVersion;

        void Start()
        {
            online = OnlineManager.Instance;
            if (online == null) { statusText.text = "Online setup is missing."; return; }
            online.Changed += Refresh;
            ownProfileButton.onClick.AddListener(OpenSelf);
            friendsButton.onClick.AddListener(() => Select(SocialList.Friends));
            requestsButton.onClick.AddListener(() => Select(SocialList.Requests));
            sentButton.onClick.AddListener(() => Select(SocialList.Sent));
            searchButton.onClick.AddListener(Search);
            searchInput.onSubmit.AddListener(_ => Search());
            refreshButton.onClick.AddListener(() => Run(async () => { if (online.Friends?.Ready == true) await online.Friends.RefreshAsync(); else await online.ConnectAsync(); }));
            closeProfileButton.onClick.AddListener(() => { visitVersion++; profileWindow.SetActive(false); });
            renameButton.onClick.AddListener(() => Rename(renameInput.text));
            if (saveExistingNameButton != null) saveExistingNameButton.onClick.AddListener(() => Rename(existingNameInput.text));
            Refresh();
        }
        void OnEnable() { if (online != null) Refresh(); }
        void OnDisable() { searchVersion++; visitVersion++; if (profileWindow != null) profileWindow.SetActive(false); }
        void OnDestroy() { if (online != null) online.Changed -= Refresh; }
        void Select(SocialList list) { searchVersion++; selected = list; Refresh(); }

        void Refresh()
        {
            if (online == null || this == null) return;
            statusText.text = online.Status;
            var profile = online.Profiles?.Current;
            ownName.text = profile?.username ?? "Your profile";
            ownLevel.text = profile != null ? "LEVEL " + profile.Level : "CONNECT TO PLAY ONLINE";
            ownXp.text = profile != null ? $"{profile.XpIntoLevel} / {profile.XpToNextLevel} XP" : "Guest account";
            ownProgress.fillAmount = profile?.Progress ?? 0;
            ownProfileButton.interactable = profile != null;
            if (existingNameInput != null && profile != null && !existingNameInput.isFocused)
                existingNameInput.SetTextWithoutNotify(UnityProfileService.BaseName(profile.username));
            bool connected = online.Friends?.Ready == true;
            // The refresh control may be an icon-only button in the existing menu.
            var refreshLabel = refreshButton.GetComponentInChildren<TMP_Text>(true);
            if (refreshLabel != null) refreshLabel.text = connected ? "Refresh" : "Retry";
            searchButton.interactable = connected && !busy;
            requestsButton.interactable = connected && !busy;
            sentButton.interactable = connected && !busy;
            friendsButton.interactable = connected && !busy;
            refreshButton.interactable = !busy && !online.Busy;
            renameButton.interactable = profile != null && !busy;
            if (saveExistingNameButton != null) saveExistingNameButton.interactable = profile != null && !busy;
            requestsLabel.text = "Requests" + (connected ? " (" + online.Friends.GetPlayers(SocialList.Requests).Count + ")" : "");
            Color active = new Color(.2f, .45f, .58f); Color inactive = new Color(.14f, .19f, .25f);
            friendsButton.image.color = selected == SocialList.Friends ? active : inactive;
            requestsButton.image.color = selected == SocialList.Requests ? active : inactive;
            sentButton.image.color = selected == SocialList.Sent ? active : inactive;
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var players = selected == SocialList.Search ? searchResults : connected ? online.Friends.GetPlayers(selected) : Array.Empty<SocialPlayer>();
            var visible = players.Where(p => p.playerId != online.Account?.PlayerId).ToList();
            emptyText.gameObject.SetActive(visible.Count == 0);
            emptyText.text = !connected ? "Connect to see your friends.\nUse Retry to reconnect." : selected == SocialList.Requests ? "No incoming requests." :
                selected == SocialList.Sent ? "No requests sent yet." : selected == SocialList.Search ? "No players found.\nSearch their exact username." : "Your friends will appear here.\nSearch a username to get started.";
            foreach (var player in visible)
            {
                var row = Instantiate(rowPrefab, content); row.gameObject.SetActive(true);
                string primary = "", secondary = ""; Action action = null, other = null;
                if (selected == SocialList.Requests) { primary = "Accept"; secondary = "Decline"; action = () => Run(() => online.Friends.AddAsync(player.playerId)); other = () => Run(() => online.Friends.DeclineAsync(player.playerId)); }
                else if (selected == SocialList.Sent) { primary = "Cancel"; action = () => Run(() => online.Friends.CancelAsync(player.playerId)); }
                else if (selected == SocialList.Search)
                {
                    primary = online.Friends.Contains(SocialList.Friends, player.playerId) ? "Friends" : online.Friends.Contains(SocialList.Sent, player.playerId) ? "Sent" : online.Friends.Contains(SocialList.Requests, player.playerId) ? "Accept" : "Add";
                    if (primary == "Add" || primary == "Accept") action = () => Run(() => online.Friends.AddAsync(player.playerId));
                }
                row.Bind(player, primary, secondary, () => Visit(player.playerId), action, other);
                row.primaryButton.interactable &= !busy;
                row.secondaryButton.interactable = !busy;
            }
            if (profileWindow.activeSelf && viewedId == online.Account?.PlayerId && profile != null) ShowProfile(profile, true);
        }

        async void Search()
        {
            if (busy || !online.Ready || online.Friends?.Ready != true) return;
            int request = ++searchVersion;
            await RunAsync(async () =>
            {
                var results = await online.Profiles.SearchAsync(searchInput.text);
                if (this == null || request != searchVersion) return;
                searchResults = results; selected = SocialList.Search;
            });
        }
        public void OpenSelf()
        {
            if (!online.Ready) return;
            visitVersion++; viewedId = online.Account.PlayerId;
            ShowProfile(online.Profiles.Current, true); profileWindow.SetActive(true);
        }
        async void Visit(string id)
        {
            int request = ++visitVersion; viewedId = id;
            profileWindow.SetActive(true); profileTitle.text = "Loading profile…"; profileStats.text = ""; profileNote.text = "";
            renameInput.gameObject.SetActive(false); renameButton.gameObject.SetActive(false); profileFriendButton.gameObject.SetActive(false);
            try
            {
                var record = await online.Profiles.VisitAsync(id);
                if (this == null || request != visitVersion) return;
                if (record == null) { profileTitle.text = "Profile unavailable"; profileNote.text = "This player has not created a profile yet."; return; }
                ShowProfile(record.profile, id == online.Account.PlayerId);
            }
            catch (Exception e) { if (this != null && request == visitVersion) { profileTitle.text = "Couldn't load profile"; profileNote.text = "Close and try again."; online.Report(e); } }
        }
        void ShowProfile(PlayerProfile p, bool own)
        {
            profileTitle.text = p.username;
            profileStats.text = $"LEVEL {p.Level}\n{p.XpIntoLevel} / {p.XpToNextLevel} XP to next level\n\nTOTAL KILLS     {p.totalKills}\nTOTAL DEATHS     {p.totalDeaths}\nTOTAL XP     {p.experience}";
            profileNote.text = own ? "Guest account • saved on this device and in the cloud\nUse your full name with #tag to help friends find you." : "Public player profile";
            renameInput.gameObject.SetActive(own); renameButton.gameObject.SetActive(own);
            if (own && !renameInput.isFocused) renameInput.SetTextWithoutNotify(UnityProfileService.BaseName(p.username));
            bool socialReady = online.Friends?.Ready == true;
            profileFriendButton.gameObject.SetActive(!own && socialReady);
            profileFriendButton.onClick.RemoveAllListeners();
            if (!own && socialReady)
            {
                string id = viewedId;
                bool friend = online.Friends.Contains(SocialList.Friends, id);
                bool sent = online.Friends.Contains(SocialList.Sent, id);
                profileFriendLabel.text = friend ? "Remove friend" : sent ? "Cancel request" : online.Friends.Contains(SocialList.Requests, id) ? "Accept request" : "Send friend request";
                profileFriendButton.onClick.AddListener(() => Run(async () => { if (friend) await online.Friends.RemoveAsync(id); else if (sent) await online.Friends.CancelAsync(id); else await online.Friends.AddAsync(id); if (this != null && viewedId == id) ShowProfile(p, false); }));
            }
        }
        void Rename(string name) => Run(() => online.Profiles.RenameAsync(name));
        async void Run(Func<Task> operation) { await RunAsync(operation); }
        async Task RunAsync(Func<Task> operation)
        {
            if (busy) return;
            busy = true; Refresh();
            try { await operation(); if (this != null) online.SetStatus(online.Friends?.Ready == true ? "Online" : online.Status); }
            catch (Exception e) { if (this != null) online.Report(e); }
            finally { busy = false; if (this != null) Refresh(); }
        }
    }
}
