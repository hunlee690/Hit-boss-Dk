# Multiplayer

Matter opens private rooms: create a room, invite friends or share the code, choose a mode, add bots if needed, and start together. Online Match searches the selected mode and waits 15 seconds before filling empty slots with bots. Friends must actively enter matchmaking to join. Rooms lock when a match starts; the host leaving ends the match.

## Skate mode

Left mouse: slide. Right mouse: punch. Middle mouse: throw a bomb or place a mine. Pickups restore stamina or supply items. The host checks attack range, walls, cooldowns, stamina, damage, deaths, respawns, inventory, and scores. Bots chase and attack opponents. Health, stamina, kills/deaths, time, and final standings appear in the existing HUD. Kills and deaths update the owning human's profile; bots never write profile progress.

Esc opens pause in every mode. Online pause blocks your input while the match continues. Offline pause freezes the game. Resume restores the selected controller; Leave returns to the menu.

Other modes retain their movement preview and existing offline gameplay. Only skate has online combat and scoring. Player movement remains owner-controlled and matches run on a player host; this is not an anti-cheat or trusted ranked economy.

## Organization

- Rooms: session lifecycle and public search.
- Invites: friends invitations.
- Network: shared avatars, movement, appearance, bots, and scene connections.
- Modes: reusable mode definitions and rule interface.
- Skate: three scripts for combat state, match rules/HUD, and network items.
- UI: room menu and player rows.
- multiplayer prefabs / multiplayer modes: configured assets.

Add a mode by creating a MultiplayerMode asset and a NetworkModeRules prefab, adding its scene to Build Settings, then adding the definition to the Multiplayer Manager. Existing room and invitation code can be reused. Use the same updated build on every device (room protocol 3).

## Verification

Play-mode host checks passed for punch damage and cooldowns, one-time kill/death scoring, respawn, stamina/bomb pickups, throwing, mine damage and owner exclusion, slide knockdown/recovery, bomb explosion scoring, results, online pause, and offline pause/resume/leave in all three modes. No runtime warnings or errors appeared in these checks. Temporary setup/build/test scripts were removed after wiring the assets.

Cloud discovery and a second account joining a room were checked previously. Combat between two separate game clients still needs a two-device test. Create a private skate room, join its code on the second device, ready/start, and confirm attacks, health, respawns, standings, and leaving on both screens.
