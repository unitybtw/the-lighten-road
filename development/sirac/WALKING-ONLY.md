# Walking-only movement

Sprint input (both Shift keys, shoulder hold and stick toggle) is retired. Movement has one walking speed, with the existing jump, climb and air control. Movement no longer spends endurance; its HUD is hidden. Main and pause control guides and the controller mapping preview omit sprint. Retired serialized whisper nodes are suppressed; existing enum indices and inert authoring APIs remain compatible with older scenes and contributor code.

Office Annex's service shaft is narrowed from three to two tiles, with matching tile/collider geometry and builder definition. Its route test now uses unmodified movement input. Older authored route hints no longer instruct sprinting.

Validation: project integrity and direct Unity Roslyn compilation of seven affected runtime/test assemblies passed. Unity Editor's Pipeline recompile timed out and MCP reported an unanswered ping; Play Mode traversal and visual inspection of this change remain unverified. Previous 170-test results describe the earlier sprint-enabled revision, not this change. Changes are local pending live Editor validation.

Follow-up validation with the reopened 6000.3.2f1 Editor: the 166-case player run passed 164, including both new pose-handover checks and walking-only movement tests. The two failures were the retired HUD visibility expectation (updated; all 7 HUD tests subsequently passed) and a manual-respawn timing check (passed in an isolated rerun). Office Annex then passed all 6 integration tests in 21.77 seconds, including the walking-only shaft jump, physical pushing and ledger/exit flow. No standalone build or physical-controller run was performed.
