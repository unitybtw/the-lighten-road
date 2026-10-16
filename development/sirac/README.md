# Siraç — Game Developer

Personal workspace for technical notes and isolated prototypes. The current proposal assigns Siraç the player movement / camera foundation and lantern fuel state / Dim-Flare behavior. The exact scope is in [the developer task split](../../docs/DEVELOPMENT_PLAN.md). Work is developed directly on `main` using scene isolation (`Sandbox/Sirac/`) and script ownership (`Scripts/Player/`, `Lantern/`).

Before starting a system, agree its scope with Kemal. Record its inputs, outputs, and shared dependencies in the task. Move reviewed runtime code into the shared Unity project at `Game/Assets/_Project/Scripts/`.

## Checkpoint 1 — movement and camera (2026-10-15)

Implemented for review in Unity **6000.3.2f1**:

- `PlayerMotor2D`: horizontal movement, grounded detection, a single jump, and collision. Airborne jump presses are discarded.
- `FollowCamera2D`: smooth orthographic camera follow.
- A reusable `PlayerPrototype` prefab with placeholder art and a separate `MovementSandbox` scene containing a floor, boundaries, and three steps.

![Movement sandbox captured in Unity Play Mode; placeholder visuals](previews/movement-sandbox.png)

Open `Game/Assets/_Project/Scenes/Sandbox/Sirac/MovementSandbox.unity` and press Play. Focus the Game view. Move with **A/D or Left/Right**; jump with **Space**. The Input System also binds the gamepad left stick and south button, but a physical gamepad has not been tested. No shared input asset was modified.

Initial tuning is movement speed **5 units/second**, jump velocity **9 units/second**, gravity scale **3**, and ground-check distance **0.06 units**. These are test values, not final balance. The grounded check ignores triggers and rejects side-wall / ceiling normals. The prefab has continuous 2D collision detection, frozen rotation, and a zero-friction / zero-bounce physics material to prevent wall clinging.

### Verification

All **4 Play Mode tests passed** through Unity MCP on 2026-10-15. They instantiate the actual `PlayerPrototype` prefab and exercise the Input System, Rigidbody2D, and colliders: movement in both directions and stopping; one jump with no airborne re-jump plus landing; camera follow; and falling along a side wall while holding toward it. The wall test first reproduced unwanted clinging, which was fixed with the prefab's physics material. The scripts compile without errors.

Tests use a synthetic keyboard and temporarily make test input independent of Editor focus, then restore the previous Input System settings. Normal play still requires focusing the Game view.

Before accepting the movement feel, play the sandbox and check stopping, jump height, platform edges, and camera comfort. Slopes, moving platforms, controller hardware, and final art animation have not been verified. This is a greybox prototype; there is no stage progression or failure flow yet.

## Checkpoint 2 — lantern fuel and Dim / Flare (2026-10-15)

Added to the same sandbox and reusable player prefab:

- `LanternFuel` owns current / maximum fuel, exposes `TrySpendFuel(float)` and `AddFuel(float)` for Kemal's future Power / Drain calls, and raises fuel-changed / depleted events. Spending fails when fuel is insufficient; recovery is capped at capacity and reports the amount accepted.
- `LanternController` toggles Dim / Flare with **F**, applies a warm URP 2D point light, drains fuel in Flare, and returns to Dim at zero. A Game view meter shows the mode and fuel; the light pulses below 25% fuel.
- `EmberPickup` is a reusable trigger prefab that restores a configurable amount. It is consumed only when it restores fuel, so a full lantern leaves it available.
- The sandbox greybox now uses lit sprites and a very low global light, so the lantern changes what is visible.

![Unity camera render of the lantern in Flare mode](previews/lantern-flare.png)

The sandbox uses **100 fuel capacity**, **80 starting fuel**, a **20-unit pickup**, **0 fuel/second in Dim**, **0.5 fuel/second in Flare**, **2 / 5.5 unit light radii**, and **F** as temporary values. These are inspector-configurable and remain open for review with Kemal and Furkan. There is no death / respawn flow or heartbeat audio yet. The depletion event is the handoff point for a later failure system.

Unity MCP was used to attach the components to the player prefab, create and place the pickup prefab, update and save `MovementSandbox`, and inspect live behavior. From the Game view, moving into the pickup restored fuel from 80 to 100 and removed the pickup; at full fuel the pickup remained. The F key also switched modes while the meter updated. The scene validator found no missing scripts or broken prefabs.

Open the scene in `Game/` using Unity 6000.3.2f1 and press Play. Focus Game view; move with **A/D or Left/Right**, jump with **Space**, and press **F** to switch the lantern. The visible meter and glow provide the current review surface.

## Checkpoint 3 — assembly definition, API refinement, and automated tests (2026-10-15)

Strengthened the lantern foundation for team integration and automated testing:

- **Assembly Definition (`TheLightenRoad.Lantern`):** Separated lantern code into its own assembly referencing `Unity.InputSystem`, `Unity.RenderPipelines.Universal.Runtime`, and `Unity.RenderPipelines.Universal.2D.Runtime`. This allows Kemal's future `TheLightenRoad.Interaction` and `TheLightenRoad.Mechanisms` assemblies to reference the lantern API directly without relying on `Assembly-CSharp`.
- **API additions for Kemal's Power / Drain system:**
  - `LanternFuel.CanSpendFuel(float amount)`: non-mutating check so interaction prompts can preview whether an Ember Vein Power interaction is affordable.
  - `LanternFuel.IsFull` and `LanternFuel.IsEmpty`: clean status queries.
  - `LanternFuel.Initialize(float current, float capacity)`: runtime reconfiguration for test fixtures and stage transitions (cumulative fuel carryover between chambers).
  - `LanternFuel.SetDrainRates(float dimDrain, float flareDrain)`: configurable drain rates.
  - Accessors for starting fuel, drain rates, low-fuel status (`LanternController.IsLowFuel`), low-fuel threshold, toggle key, and pickup amount/collection status (`EmberPickup.FuelAmount`, `EmberPickup.IsCollected`, `EmberPickup.SetFuelAmount`).
- **Comprehensive automated test coverage (22 Play Mode tests, all passed):**
  - `LanternFuelTests` (11 tests): default initialization; custom initialization with non-positive capacity rejection; `TrySpendFuel` deduction, failure, and negative amount rejection; `CanSpendFuel` queries; `AddFuel` capping at capacity, zero return when full, and negative amount rejection; `FuelChanged` and `FuelDepleted` event dispatches; and `TickLanternDrain` consumption across Dim and Flare.
  - `LanternControllerTests` (4 tests): Dim / Flare mode toggle and zero-fuel rejection; auto-revert from Flare to Dim when fuel depletes; low-fuel threshold detection; and synthetic keyboard **F** key input toggle.
  - `EmberPickupTests` (3 tests): exercises the actual `PlayerPrototype` and `EmberPickup` prefabs; confirms 80 to 100 fuel restoration and pickup destruction upon contact; confirms pickup is preserved when lantern is full; and confirms non-player colliders are ignored.
  - `PlayerMotor2DTests` (4 existing tests re-verified): horizontal movement, single grounded jump and airborne rejection, side-wall non-clinging, and camera follow.

### Live Verification

Verified in Unity 6000.3.2f1 via Unity MCP:
- All **22 Play Mode tests passed** (`TestResults.xml` recorded duration ~5.4s).
- Scene validation for `MovementSandbox.unity`: **0 missing scripts, 0 broken prefabs, 0 issues**.
- Unity console: **0 compile errors, 0 warnings**.

## Checkpoint 4 — pixel-art lantern HUD (2026-10-16)

Replaced the legacy debug IMGUI overlay with a fully procedural pixel-art HUD rendered via a pixel-perfect Screen Space Overlay Canvas (reference resolution $640 \times 360$):

- **Component & Assembly (`LanternHUD.cs`, `TheLightenRoad.UI`):**
  - Modular UI assembly cleanly referencing `TheLightenRoad.Lantern`, `UnityEngine.UI`, and `Unity.InputSystem`.
  - Automatically binds to the active player's `LanternController` and listens to `FuelChanged`, `FuelDepleted`, and `FlareChanged` events.
- **Visual Features:**
  - **Procedural Pixel-Art Lantern (`PixelSprites.cs`):** 11×15 brass lantern casing with an animated multi-harmonic flame, glass reflections, and a radial warm glow halo that intensifies during Flare mode.
  - **Segmented Fuel Bar & Drain Trail:** 20 distinct segments with pixel-snapped horizontal filling. Includes a two-phase drain trail (white-amber lag) so energy consumption is readable at a glance.
  - **Dynamic Palette Shift & Low Fuel Warning:** Healthy amber at high capacity, shifting toward hot orange and warning red as fuel diminishes. Below 25% capacity, the border and flame pulse with a heartbeat rhythm, and labels switch to `DIM LOW` / `FLARE LOW`.
  - **Mode Indicator & Input Key Hint:** Crisp labels displaying current mode (`DIM`, `FLARE`, `EMPTY`) along with the configurable toggle key binding (`[F]`).
  - **Pickup & Depletion Feedback:** Floating `+20` popup indicator upon collecting Ember Pickups and a pixel-snapped screen shake when fuel reaches zero.
- **Legacy IMGUI Overlay:** Gated behind `LanternController.ShowDebugOverlay` (disabled by default) to keep production builds clean while preserving debugging capabilities.

![Pixel-Art Lantern HUD states across Dim, Flare, Drain Trail, Low Fuel, Pickup, and Empty](previews/lantern-hud.png)

### Automated Test Coverage (28 Play Mode tests, all passed)

- Added 6 new automated tests in `LanternHUDTests.cs`:
  - `BuildsOverlayCanvasAndShowsCurrentFuel`: verifies canvas hierarchy construction, controller binding, percentage, and initial fill.
  - `DisplayedFillEasesDownWhileTrailLingers`: confirms the two-stage visual drain trail.
  - `FlareModeUpdatesLabel`: verifies real-time UI reaction to mode toggles.
  - `LowAndEmptyFuelAreFlagged`: confirms low-fuel threshold detection and empty status.
  - `FuelGainShowsPickupFeedback`: verifies the animated pickup feedback.
  - `LegacyDebugOverlayIsOffByDefault`: verifies IMGUI overlay is disabled by default.
- All **28 Play Mode tests passing** in Unity Test Runner.

## Checkpoint 5 — platformer game feel and juice (2026-10-16)

Elevated movement responsiveness in `PlayerMotor2D.cs` with classic high-feel platformer mechanics:

- **Coyote Time (`0.15s`):**
  - Allows jumping within a $0.15$-second window after walking off a platform ledge into midair. Eliminates the frustrating "missed jump" feeling at platform edges.
  - Automatically resets upon jump execution so airborne double jumps remain impossible.
- **Jump Buffering (`0.12s`):**
  - Remembers jump presses initiated within $0.12$ seconds before touching the ground, automatically triggering an immediate jump the exact frame the player lands.
- **Variable Jump Height (Jump Cut `0.5x`):**
  - Releasing Space early while ascending cuts upward velocity in half, allowing players to perform snappy short hops over obstacles or full-height jumps by holding the button.
- **Automatic Sprite Flipping:**
  - Automatically updates `FacingDirection` and sets `SpriteRenderer.flipX` based on horizontal movement input with deadzone filtering.
- **Procedural Squash & Stretch:**
  - Adds visual-only spring deformation on `visualTransform` without distorting the physical `CapsuleCollider2D`:
    - **Takeoff Stretch:** Stretches vertically (`0.85x, 1.25y`) to convey upward energy.
    - **Landing Squash:** Compresses vertically on ground contact (`1.25x, 0.75y`) to convey weight and momentum.
    - Smoothly recovers toward base scale via configurable recovery speed (`squashRecoverySpeed = 9`).

### Automated Test Coverage (32 Play Mode tests, all passed)

- Added 4 new Play Mode tests in `PlayerMotor2DTests.cs`:
  - `MovementInputFlipsSpriteDirection`: verifies `FacingDirection` and `SpriteRenderer.flipX` reaction to left/right input.
  - `CoyoteTimeAllowsJumpShortlyAfterLeavingLedge`: verifies walking past a ledge boundary and executing a jump in midair.
  - `JumpBufferExecutesUponLanding`: verifies pre-landing jump buffering and immediate takeoff on contact.
  - `VisualSquashAndStretchReactsToJump`: verifies vertical stretch on jump initiation and smooth recovery.
- Total **32 Play Mode tests** covering player motor, camera, lantern fuel, dim/flare controller, ember pickups, and HUD.

## Checkpoint 6 — 2D dynamic shadows and organic flame flicker (2026-10-16)

Integrated real-time 2D shadow casting and atmospheric flame undulation into `MovementSandbox.unity` and `LanternController.cs`:

- **2D Dynamic Shadow Casters:**
  - Attached `ShadowCaster2D` to all obstacles and boundaries in `MovementSandbox.unity` (`Step 01`, `Step 02`, `Step 03`, `Left Boundary`, `Right Boundary`).
  - Obstacles now cast crisp, dramatic 2D occlusion shadows across the floor and walls as the player moves with the lantern.
  - Added `ShadowSetupUtility.cs` under `Editor/` providing a 1-click Unity menu item (`Tools -> The Lighten Road -> Setup 2D Dynamic Shadows`) to automatically inspect, attach, and configure `ShadowCaster2D` on any active scene.
- **Organic Flame Flicker:**
  - Added continuous Perlin-noise-driven flame modulation to `LanternController.cs`:
    - Simulates subtle, organic breathing of the flame (`flickerIntensity = 0.04`, `flickerRadius = 0.05`, `flickerSpeed = 8`).
    - Dynamic shadows subtly shift and dance in real-time, elevating the subterranean ruin atmosphere from static lighting to an immersive living torchlight feel.
- **Inspector Tuning Parameters (`LanternController.cs` & `PlayerPrototype.prefab`):**
  - Configurable `enableShadows` toggle, `shadowIntensity` ($0.85$), and `shadowSoftness` ($0.25$).
  - Configurable `enableFlicker` toggle, intensity, and speed.

### Automated Test Coverage (33 Play Mode tests)

- Added automated test in `LanternControllerTests.cs` verifying dynamic shadow and organic flicker property configuration.
- Total **33 Play Mode tests** covering player motor, game feel, camera, lantern fuel, dim/flare controller, shadows/flicker, ember pickups, and HUD.

## Checkpoint 7 — Atmospheric Particle FX: Ambient Dust Motes, Lantern Sparks, and Footstep Dust (2026-10-16)

Elevated subterranean ruin immersion with three reactive 2D particle systems using URP 2D Lit and Unlit shaders:

- **1. Ambient Dust Motes (`DustMotesAtmosphere.cs`):**
  - Ethereal motes lazily drifting across the sandbox environment (`areaSize = (26, 14)`).
  - Uses `Universal Render Pipeline/2D/Sprite-Lit-Default` with soft circular gradient particles.
  - In darkness, motes are almost imperceptible; when illuminated by the player's lantern `Light2D`, they ignite into sparkling warm amber motes, providing breathtaking depth and illumination feedback.
  - Perlin noise turbulence module provides gentle, organic subterranean air currents.
- **2. Lantern Sparks & Embers (`LanternSparksFX.cs`):**
  - Hot fire sparks and embers rising from the lantern flame into the air.
  - Simulated in **World Space**: when the player walks or runs, embers trail behind organically instead of staying locked to the player transform.
  - **Dim Mode:** Calm, slow embers ($4\text{ sparks/s}$, gentle upward float).
  - **Flare Mode (Shift):** Energetic fiery blast ($22\text{ sparks/s}$, faster speed, wider cone, bright yellow-white to deep crimson-ember color over lifetime).
  - **Zero Fuel:** Flame dies and spark emission immediately ceases when fuel reaches 0.
- **3. Footstep & Movement Dust (`PlayerFootstepDustFX.cs`):**
  - Ground movement and kinetic impacts emit subtle dust puffs at the player's feet:
    - **Run Dust:** Emits tiny dust puffs kicking backwards away from `FacingDirection` while moving on stone ground.
    - **Jump Puff:** Bursts outwards horizontally upon takeoff.
    - **Landing Shockwave:** Expanding dust cloud upon touchdown with particle count and velocity scaling dynamically with landing impact velocity.
- **Editor Tooling & Asset Pipelines:**
  - Procedural soft textures (`Particle_SoftCircle.png`, `Particle_Spark.png`, `Particle_DustPuff.png`) and fallback procedural generator (`ParticleTextures.cs`).
  - Added 1-click Editor utility: `Tools -> The Lighten Road -> Setup Atmospheric Particle Effects` in `EffectsSetupUtility.cs`.

### Automated Test Coverage (36 Play Mode tests)

- Added `ParticleEffectsTests.cs` verifying:
  - `DustMotesAtmosphereInitializesAndConfiguresModules`: verifies ParticleSystem creation, box shape, world simulation space, and emission.
  - `LanternSparksFXRespondsToDimAndFlare`: verifies emission rate boost during Flare mode and shutdown on fuel depletion.
  - `PlayerFootstepDustFXInitializesChildSystems`: verifies dynamic instantiation of run dust and burst particle child systems.
- Total **36 Play Mode tests** across player motor, game feel, camera, lantern fuel, dim/flare controller, shadows/flicker, atmospheric particle FX, ember pickups, and HUD.

### Next checkpoint

Continue expanding player systems in `Sandbox/Sirac/`:
1. Player sprite integration with Maksim's 24-frame Light character idle/flare spritesheet.
2. Death & Respawn loop on fuel depletion or hazard contact (`PlayerRespawnController`).




