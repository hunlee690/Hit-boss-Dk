# Private multiplayer rooms

Open the main menu and use the existing Matter button. Create a private room, share its code or invite an online friend, select a mode, then wait for guests to press Ready. The host starts everyone together. Closing the panel keeps the room active; Leave room closes it for the host or removes a guest. Host departure ends the session; host migration is intentionally not enabled.

## Organization and extension

- `multiplayer scripts/Rooms`: Unity session adapter and room lifecycle.
- `multiplayer scripts/Invites`: ten-minute invitations sent through Unity Friends; acceptance is explicit.
- `multiplayer scripts/Network`: Relay/Netcode connections, synchronized scene loading, owner movement and animation, local camera control.
- `multiplayer scripts/Modes`: mode definitions and replaceable `NetworkModeRules` adapters.
- `multiplayer scripts/UI`: Matter room menu and reusable list rows.
- `multiplayer scripts/Editor`: one-time menu/prefab builder, available under Tools > Hit Boss.
- `multiplayer prefabs`: shared runtime manager, network player, rule adapter and row.
- `multiplayer modes`: definitions linked to the three existing scenes.

To add a mode, create a Multiplayer Mode asset, give it a stable unique ID, set its scene/player limits, assign a rules prefab derived from NetworkModeRules, and add it to the Multiplayer Manager prefab's modes array. Include the scene in Build Settings. The room menu automatically includes the new definition. Keep combat, scoring, teams and winning logic in the mode adapter, with server-authoritative network state. Room and invitation code should not need changes.

## Current scope

The initial shared-movement adapter supports online free play in the existing scenes. Local AI/match managers and player combat/throwables are disabled online because they are not network-authoritative. Networked damage, ragdolls, bombs, scores, match results and remote cosmetic choices are not implemented in this pass. Offline play retains its existing mode managers. Private-room profile progression must not be used for a trusted ranked economy until authoritative results exist.

The Unity project uses the previously linked production Authentication/Friends setup plus Multiplayer Services 2.3.3, Netcode for GameObjects 2.13.2 and Unity Transport. Relay allocations start when the host starts a match. No paid service upgrade was enrolled. Cloud Lobby/Relay runtime access still needs your live verification.

## Your play-test checklist

No Play-mode, live service, build or multiplayer runtime tests were run, at your request. Scripts were imported and the menu/prefabs were generated in Edit mode.

1. Build and run a second client on another computer/account (or use a separate authentication profile; two clients must have different player IDs).
2. In Matter, create a room on A and join its code on B. Check both names, host badge and ready states.
3. Change mode on A. B must ready up again. A cannot start before the minimum players are present and ready.
4. With both accounts already friends and online, send an invitation. Check Matter's invite badge, Join and Dismiss. Offline friends cannot be invited.
5. Start together. Both clients should load the selected scene, see each other move/jump and have only their own camera/input. Repeat with each mode.
6. Press Escape in the shared scene, then Leave room. Check return to the main menu. Repeat after the host exits and after an interrupted connection.
7. Check invalid/full/closed room codes, repeated clicks, reopening the panel and solo Play after leaving.

If anything fails, send the first red Console error and which checklist step triggered it. The original menu was backed up before installation in `Library/CodexMultiplayerBackup/main menu.before-multiplayer.unity`.
