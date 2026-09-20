using System;
using System.Linq;
using HitBoss.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitBoss.Multiplayer
{
    public sealed class RoomMenuController : MonoBehaviour
    {
        public MainMenu mainMenu;
        public GameObject window, createArea, roomArea;
        public Button matterButton, closeButton, createButton, joinButton, leaveButton, readyButton, startButton, copyButton, refreshButton;
        public Button friendsTab, invitesTab, previousMode, nextMode;
        public TMP_InputField codeInput;
        public TMP_Text codeText, modeText, statusText, rosterTitle, rightTitle, emptyFriends, emptyPlayers, badge;
        public RectTransform playerContent, friendContent;
        public RoomListRow rowPrefab;
        RoomManager manager;
        OnlineManager online;
        int selectedMode;
        bool showInvites;
        void Start()
        {
            manager = RoomManager.Instance; online = OnlineManager.Instance;
            manager.connection.BindMenu(mainMenu.playerSetup);
            manager.Changed += Render;
            if (online != null) online.Changed += Render;
            matterButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(() => window.SetActive(false));
            createButton.onClick.AddListener(() => _ = manager.CreateAsync(selectedMode));
            joinButton.onClick.AddListener(() => _ = manager.JoinAsync(codeInput.text));
            leaveButton.onClick.AddListener(() => _ = manager.LeaveAsync());
            readyButton.onClick.AddListener(() => _ = manager.ReadyAsync());
            startButton.onClick.AddListener(() => _ = manager.StartMatchAsync());
            copyButton.onClick.AddListener(() => { if (manager.Room != null) { GUIUtility.systemCopyBuffer = manager.Room.Code; manager.SetStatus("Room code copied."); } });
            refreshButton.onClick.AddListener(() => _ = manager.RefreshAsync());
            friendsTab.onClick.AddListener(() => { showInvites = false; Render(); });
            invitesTab.onClick.AddListener(() => { showInvites = true; Render(); });
            previousMode.onClick.AddListener(() => ChangeMode(-1));
            nextMode.onClick.AddListener(() => ChangeMode(1));
            window.SetActive(false); Render();
        }
        public void Open() { window.SetActive(true); window.transform.SetAsLastSibling(); Render(); }
        void ChangeMode(int direction)
        {
            if (manager.modes.Length == 0) return;
            selectedMode = (selectedMode + direction + manager.modes.Length) % manager.modes.Length;
            if (manager.Room != null) _ = manager.SelectModeAsync(selectedMode); else Render();
        }
        static void Clear(Transform content)
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        RoomListRow Row(RectTransform content, string name, string description, string action = null, Action click = null, Action dismiss = null)
        {
            var row = Instantiate(rowPrefab, content); row.gameObject.SetActive(true); row.Bind(name, description, action, click, dismiss);
            row.action.interactable = !manager.Busy; row.secondary.interactable = !manager.Busy; return row;
        }
        void Render()
        {
            if (manager == null) return;
            badge.text = manager.Invitations.Count > 0 ? manager.Invitations.Count + " invite(s)" : manager.Room != null ? "In room" : "";
            // Closing the menu preserves the room; no list rebuilding is needed while hidden.
            if (!window.activeSelf) return;
            var room = manager.Room;
            bool inRoom = room != null;
            if (inRoom && manager.Mode != null) selectedMode = Array.IndexOf(manager.modes, manager.Mode);
            var selected = manager.modes.Length > 0 ? manager.modes[Mathf.Clamp(selectedMode, 0, manager.modes.Length - 1)] : null;
            modeText.text = selected != null ? selected.displayName + "  •  " + selected.minPlayers + "–" + selected.maxPlayers + " players" : "No modes configured";
            previousMode.interactable = nextMode.interactable = !manager.Busy && (!inRoom || manager.IsHost && !room.IsLocked);
            statusText.text = manager.Status;
            createArea.SetActive(!inRoom); roomArea.SetActive(inRoom);
            createButton.interactable = joinButton.interactable = !manager.Busy && online?.Ready == true && selected != null;
            codeInput.interactable = !manager.Busy;
            if (inRoom)
            {
                codeText.text = room.Code;
                readyButton.gameObject.SetActive(!manager.IsHost);
                readyButton.GetComponentInChildren<TMP_Text>().text = UnityRoomService.IsReady(room, room.CurrentPlayer) ? "Not ready" : "Ready";
                readyButton.interactable = !manager.Busy && !room.IsLocked;
                startButton.gameObject.SetActive(manager.IsHost); startButton.interactable = manager.CanStart;
            }
            leaveButton.interactable = copyButton.interactable = refreshButton.interactable = !manager.Busy;
            rosterTitle.text = inRoom ? "PLAYERS  " + room.PlayerCount + "/" + room.MaxPlayers : "YOUR PRIVATE ROOM";
            Clear(playerContent); Clear(friendContent);
            emptyPlayers.gameObject.SetActive(!inRoom);
            if (inRoom)
                foreach (var player in room.Players)
                {
                    var description = player.Id == room.Host ? "Host" : UnityRoomService.IsReady(room, player) ? "Ready" : "Not ready";
                    if (player.Id == room.CurrentPlayer.Id) description += " • You";
                    Row(playerContent, UnityRoomService.PlayerValue(player, "name"), description);
                }
            invitesTab.GetComponentInChildren<TMP_Text>().text = "Invites (" + manager.Invitations.Count + ")";
            rightTitle.text = showInvites ? "RECEIVED INVITATIONS" : "INVITE FRIENDS";
            var count = 0;
            if (showInvites)
            {
                foreach (var invitation in manager.Invitations.ToArray())
                {
                    var captured = invitation; count++;
                    var row = Row(friendContent, captured.senderName, inRoom ? "Leave this room to accept" : "Invited you to a private room", "Join", async () =>
                    {
                        if (captured.Expired) { manager.Dismiss(captured); return; }
                        await manager.JoinAsync(captured.code);
                        if (manager.Room != null && manager.Room.Code == captured.code) manager.Dismiss(captured);
                    }, () => manager.Dismiss(captured));
                    row.action.interactable &= !inRoom;
                }
            }
            else if (online?.Friends?.Ready == true)
            {
                foreach (var friend in online.Friends.GetPlayers(SocialList.Friends).OrderByDescending(f => f.online).ThenBy(f => f.username))
                {
                    var captured = friend; count++;
                    bool joined = inRoom && room.Players.Any(p => p.Id == captured.playerId);
                    var row = Row(friendContent, captured.username, joined ? "Already in room" : captured.online ? "Online" : "Offline", "Invite", () => _ = manager.InviteAsync(captured.playerId));
                    row.action.interactable &= inRoom && !room.IsLocked && captured.online && !joined;
                }
            }
            emptyFriends.gameObject.SetActive(count == 0);
            emptyFriends.text = showInvites ? "No room invitations yet.\nInvitations appear here while you are online." : "Add friends from the Social panel.\nOnline friends can receive room invitations.";
        }
        void OnDestroy()
        {
            if (manager != null) manager.Changed -= Render;
            if (online != null) online.Changed -= Render;
        }
    }
}
