# Office playable route

Scene: `Game/Assets/_Project/Scenes/Sandbox/Sirac/Office.unity`.

The existing Office is now a short playable room using its original tiles, furniture,
books, music and lighting assets. This is separate from OfficeAnnex.

## Route

1. Walk through reception; the ledger remains on its desk.
2. Push the crate right to the raised records platform. Jump onto the crate, then
   press towards the ledge and **W / Up** to climb. A second jump is unnecessary.
3. The records gallery sets a checkpoint and refills the lantern for the chamber.
4. Jump the service shaft at normal walking speed. Falling retries from the checkpoint
   and returns the crate to its starting position.
5. Read the second book in upper files, climb the final ledge with **W / Up**, and
   reach the office door for the existing lantern-led chamber completion sequence.

Both books retain their original IDs and story text (`office-ledger`, `office-margin`).
The exit completes this room; a destination scene has not been assigned.

## Structure

- Separate tilemap colliders define the entry, records gallery, upper files and exit.
  The climb probe therefore reads the actual ledge height rather than the room ceiling.
- Crate pushing and checkpoint behavior reuse the existing OfficeAnnex components.
  `OfficeLevelRoute` resets the crate when the player respawns.
- Furniture sits on each floor, with book desks behind the player. The closed shell,
  rear wall and ceiling fixtures extend through the upper route.
- `FollowCamera2D.SetWorldBounds` opts this scene into viewport containment. Bounds
  include the whole camera view, including shake, teleport snaps and paused resizing.
  Older scenes retain their existing camera bounds behavior.

The one-time authoring menu is **Tools → The Lighten Road → Office → Complete playable
route**. It requires a saved Office in Edit Mode and refuses to overwrite an existing
`Office playable route` root. Edit the saved scene normally for subsequent changes.

## Verification

Unity Editor Play Mode: **14/14 tests passed** (5 Office integration tests, 9 camera
tests). The route test physically pushes the crate, mounts it, climbs both ledges,
jumps the shaft and completes the exit using keyboard input. Other checks cover
respawn, both books, floor collision, pushing in both directions and view containment
at 4:3, 16:9, 21:9 and 32:9. Compilation reported no errors or warnings.

Game-camera captures at 1920 × 1080 were visually reviewed. A standalone build and a
physical controller run were not tested. Local reports and captures are under
`Game/Library/Readability/OfficeLevel/` (ignored by Git).

Kemal's Interaction, Mechanisms and sandbox code were not modified.
