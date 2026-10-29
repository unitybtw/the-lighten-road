# Light relay, gate binding and catalog checks — 29 October 2026

This pass corrects the light-driven relay/gate prototype in Siraç's Lantern folder and validation of archive record identity. Kemal's Interaction, Mechanisms and Sandbox/Kemal sources are unchanged. Ember costs, chamber reset rules and authored scene geometry are preserved.

## Light detection

The relay previously used the lantern's binary IsFlared flag and maximum mode radius. It could charge before the light had expanded, stop as soon as release began while the large light was still visible, count a disabled light, and measure distance from the player centre rather than the actual lamp. It also rejected an already-paid scout as soon as the last ember left the inventory.

Detection now uses the emitted Light2D's position, enabled state, positive intensity and current outer radius, capped by the relay's configured range blended across the same Flare transition. The faster Flare charging rate begins at half blend, matching the tutorial's transition threshold. A final reserved scout remains eligible while its light exists, then stops contributing when it finishes. Fixtures without a renderer use the same gradual controller blend instead of an instantaneous mode switch. The existing distance-based model remains; no wall-occlusion simulation was added.

## Gate binding

ArchiveGate2D now subscribes when its relay is assigned after Awake, when a parent relay is discovered, and when the Inspector changes the serialized reference. It unsubscribes the previously bound relay on reassignment/disable and synchronizes the current state when enabled again. Binding does not move the gate before its own positions have initialized. The original gate integration test now uses the real property connection instead of manually installing an extra listener that hid the bug.

## Catalog validation

Records with null, empty or whitespace-only IDs cannot be discovered, rewarded or counted as progress. An unassigned record array has zero entries and unsuccessful lookups rather than throwing. Beginning a session with a null catalog is rejected before clearing the existing channel and discoveries, so an invalid request cannot erase the current session.

## Verification

The initial relay run reproduced nine failures, including the strengthened existing integration test. The initial catalog run reproduced three more. A further regression covers changing the relay through the serialized field, as the Inspector does. See [light-records-validation.json](light-records-validation.json) for final results and before/after case evidence.

The focused relay/gate suite passed 17/17 cases. The full project Play Mode run passed 385/385 (zero failures, skips or inconclusive results, 318.82 seconds), and the integrity checker passed for all 599 tracked assets. Compilation reported zero errors and warnings.

All 28 saved preferences were verified against their pre-test snapshot with zero mismatches. Saved preferences, including the death counter and actual AdaptiveTriggersEnabled key, are restored after testing. Test-generated fonts/settings/scenes are excluded. Standalone builds and physical-controller/audio-device checks are outside this pass.
