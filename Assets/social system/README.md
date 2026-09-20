# Accounts, profiles, and friends

The existing `main menu` scene contains `Online Manager` and a `SocialPanelController` on the existing main-menu group. The original social/profile panels are reused. The profile viewer is a sibling under the Canvas so it can cover the menu.

## Organization

- `social scripts/Accounts`: persistent online startup and guest sign-in.
- `social scripts/Profile`: profile data, level calculation, queued progression saves.
- `social scripts/Friends`: friend-list and request operations.
- `social scripts/Services`: provider interfaces and Unity implementations.
- `social scripts/UI`: menu bindings and reusable player rows.
- `social scripts/Editor`: menu installer and explicit developer validation tools.
- `social prefabs`: reusable social player row.

## Cloud configuration

Unity project: **Hit boss Dk**, ID `d1e43084-437d-4311-a82d-7ab10ff2ed1e`.
Environment: **production**.
Installed: Authentication 3.8.0, Cloud Save 3.4.1, Friends 1.2.0.

Cloud Save Public Player Data keys:

- `social_profile_v1`: username, game-owned player ID, kills, deaths, XP, schema version, recent progression batch IDs.
- `social_username`: lowercase username without Unity's discriminator.

Cloud Save index `social_username_1` is configured on Player / Public, key `social_username`, ascending. Search is exact and case-insensitive; the optional `#tag` narrows duplicate base names. Rename republishes the search key. Profile initialization republishes it for newly configured indexes.

## UI

- Click the left profile card to view your stats and change your username.
- The existing username input also saves through its new Save button.
- Search by exact username or full `name#tag`, then click a result name to inspect their profile or Add to send a request.
- Requests contains Accept/Decline; Sent contains Cancel.
- Click a friend's name to view their public profile and remove the friendship if desired.
- Presence comes from Friends, with live relationship/presence events and manual Refresh.

## Progression

The current single-player `MatchParticipant.AddKill/AddDeath` hooks record human progression. AI scores do not update the player's profile. The configurable default is **25 XP per kill**, zero XP for death. Level 2 costs 100 XP; each subsequent level costs 50 XP more. Level is derived from cumulative XP rather than separately stored.

Saves are batched every 10 seconds, on scene changes, and on mobile pause. Each delta is first saved locally, scoped to cloud project, environment, and authenticated player ID. Cloud write locks reject conflicting updates; later retries load the current record and merge pending deltas. Batch IDs prevent duplicate application after an uncertain response; requests contain at most 64 deltas and retain 256 recent IDs. This protects normal retry windows, not arbitrarily old replays across many devices.

## Account and authority boundaries

Sign-in currently uses Unity's persistent anonymous account/session. It does not provide cross-device account recovery or Google/Steam/password login. Link a recoverable identity provider before release; clearing app data can lose access to an unlinked guest account.

Current stats are development progression written by the authenticated client to its own public profile. They are **not cheat-resistant**. Before ranked multiplayer, purchases, or rewards depend on these stats, replace progression writes with server/Cloud Code validated match results. Use a local-player ownership check when networking `MatchParticipant`; `!isAI` is only sufficient for the current single-player setup.

Gameplay references our own managers; only `Services` references the Unity service SDKs. Replace providers behind the interfaces for future backend migration.

## Validation

`Tools > Hit Boss > Validate Profile Logic` checks XP boundaries, invalid names, progression persistence, uncertain-response retry idempotency, and a large pending queue.

`Tools > Hit Boss > Validate Live Social Services (Play Mode)` deliberately creates/reuses two isolated guest test accounts named SocialTestA and SocialTestB in production. It checks saved stats, search, public profiles, send/decline/cancel/accept requests, presence, and removal. It signs those accounts out afterward. It does not alter the normal player's stats. These test profiles remain available for development searches.

Live checks passed during implementation. The main-menu profile card and search/profile-request buttons were also exercised directly. There is no multiplayer gameplay transport yet; that is the next roadmap stage.

Before using the installer on another scene, open that scene and ensure it has the expected original MainMenu and panel names. It refuses duplicate installation. The original menu was backed up under `Library/CodexSocialBackup/main menu.before-social.unity` before changes.
