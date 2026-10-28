# Homework 2 B: Lore Collectibles / Codex

## Play the implementation

Open `Game/Assets/_Project/Scenes/Sandbox/Sirac/FurkanTutorial.unity` with Unity 6000.3.2f1. An Archive Terminal is near the opening at (2.2, 0.9); another is in the upper archive detour at (37.4, 7.25). Approach within 1.7 world units and press E / Triangle / Y. The terminal opens its discovered record. Tab / View / Create reopens the journal; arrows or shoulder buttons browse, Escape / B / Circle closes it.

The two supplied English records are prototype narrative and puzzle clues for designer review. Both are editable assets under `Resources/Codex`. Audio clips are optional and currently unassigned. Add logs to the catalog before placing terminals: discovery/decryption use the catalog's unique valid IDs, not the number of terminal objects.

## Observer flow

`DiegeticWhisper` has an optional `ArchiveLog` field. Ordinary tutorial whispers keep their existing behavior. A configured terminal calls `CodexSession.Discover(id)`, which validates/deduplicates the ID and raises `SocialChannel.Raise(new SocialEvent { kind = LogFound, text = id })`. The terminal holds no reference to a fuel component or UI.

The channel uses the lecture `StoredDelivery` and `AmbientMembership` assets. Runtime copies provide a separate stored log for this play session, survive scene changes, and clear on a fresh Play/application session. Lecture policies and source assets are not used as a permanent save file.

- `ArchiveCodexMenu : ISocialListener` clears its local list before subscribing and rebuilds it from replay. It uses the existing static `ArchiveMenuStyle` for typography; the style helper was not converted into a behaviour. Live and repeated terminal interactions request the appropriate page independently of reward events.
- The lecture `PartyFeedUI : ISocialListener` receives the channel and formats the recovery feed. A separate visibility observer shows live notifications briefly and suppresses replay notifications.
- `LanternFuel : ISocialListener` immediately ignores `isReplay`, ignores other kinds/unknown IDs, and grants two legacy charges (one whole ember) once per live ID. Existing inventory clamping enforces four embers. At full inventory the reward is capped, not banked for later.

The existing chamber start (three embers), maximum (four), full-ember flare spend, and three-second darkness rules are reused. Codex opening pauses simulation and blocks gameplay input; close/disable restores time and focus. Death/room reset never clears the session catalog history, and newly created fuel/UI listeners receive replay without a second payout. A used terminal only requests its saved page.

## Lecture source provenance

Copied from the user's `Downloads/Observer.zip` / `03_Observer`: SocialChannel, ISocialListener, SocialEvent, SocialEventKind, DeliveryPolicy, MembershipPolicy, StoredDelivery, AmbientMembership, PartyFeedUI and their PartyMember/PartyRole type dependencies. The latter needs the user's week-5 `PlayerContext.cs` (copied from Desktop/ödevdev1/Scripts). Observer lives in its own assembly.

The copied lecture C# files are unchanged except adding `LogFound` to SocialEventKind and its formatting case in SocialEvent.ToString. New adapters, UI and game assets live outside those lecture files. `ArchiveCodexSetup` can install the catalog, policies, prefab and sample terminals from the editor menu, and does not duplicate an existing Archive Terminals root.

## Validation

Unity 6000.3.2f1: **212/212 frontend and player Play Mode tests passed**, zero failed or skipped. Native Game-view review confirmed E opens the first terminal, fuel goes from 3 to 4, and Escape/Tab closes/reopens the record.

Automated checks cover live rescue from zero fuel, cancellation of the darkness timer, duplicate-terminal suppression, re-created/re-enabled listeners without duplicate fuel, stored replay, four-ember cap, session reset, catalog percentage, UI reconstruction, paginated reading and pause/input restoration. Test XML and render previews are generated under the ignored `Game/Library` directory. No physical controller or standalone build certification is implied by Editor tests.

Kemal's Interaction, Mechanisms and sandbox files are outside this change. No course portal submission was performed.
