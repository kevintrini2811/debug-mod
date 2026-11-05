# Test: Debug Mod 1.1.0

As of November 3, 2026. Game version: PvZ Replanted 1.5.1469_Steam, Unity 6000.0.52f1, MelonLoader 0.7.3, Proton Experimental. The game starts and runtime tests were carried out after explicit approval.

## Installed files

- `DebugMod.dll` 1.1.0: `0f866855988ad6d5b706b838bd6df6f82eef8b52798fe72d92d90cc5ab902c92`
- `PlantLibrary.dll` 1.0.4: `f7ebd4a28043888eee6e4a353fb8e4b951f9daa7b58008f06e835fa5c281e260`
- `ZombieLibrary.dll` 1.1.4: `5223c26a907322335bf8ec70aa0ce8ba8e4da7f5c0aff3d1a54c146103cfbe2a`

The installed DLLs match the release builds byte-for-byte. All 24 existing checksum entries for the installed mods were checked. Last installation: `backups/install-20261103-132409-660717/installation.json`.

## Successful exams

- Release builds for Debug Mod, Plant Library and Zombie Library: each with no warnings or errors.
- Three offline tests of the Python client: missing/outdated sign of life, request/response mapping and cleanup, timeout with unclear effect.
- Existing Zombie Library Exam: `Registry contracts passed.`
- 52 API calls in full debug pass including expected errors: native and registered picks/unlocks, placement, re-placement, removal, resources, bank, roster, state queries, screenshot, FPS, speed, settings, pause and return to menu.
- Error cases: level not yet ready to play, wrong cell, unsuitable terrain, old board ID, inactive second player, protected profile storage, invalid volume and unknown command.
- Protocol check: wrong protocol version, old session, expired request and wrong argument type.
- Normal Survival Level: Start, Native Plant Choice, Budget 30 with Normal/TrafficCone/Football, Plant Bank, Placement, Sun and Return to Menu.
- Mini-game `ChallengeWhackAZombie`: Start to `Playing`, zombie query and return to the menu.
- Placements regardless of chosen roster, including empty zombie roster. Eye zombie with separated states as well as actual flying projectiles queried.
- Registered mod plant “Flame Pea” selected via the debug selection and placed directly from the mod catalog; native marker `Jalapeno` confirmed.

Raw answers are at `validation/`. `tools/smoke_debug_api.py` repeats the main run in the game that has already started. Additional protocol and variant responses were saved during targeted runtime testing.

## Limits of the test

The 151 native level definitions listed have not all been played through individually. Special modes can have their own spawn and placement conditions. Native persistent profile storage has not been tested with a changed coin level. In the debug level it was checked that `persist: true` remains locked; The coin value in the main memory was written back unchanged. Graphical approval of all mod almanac pages and all window/resolution modes remains open. Third-party mods can have additional special rules.

The runtime check found and fixed broken Harmony parameter names, Unity's broken generated screenshot wrapper, native unit ID serialization, missing second player, and menu switching via native state control. No errors or exceptions occurred in the final game log.

The game was left in the main menu after the trials.

## Zombie Selection 1.2.0 — actually executed on November 3rd, 2026

Release build with no warnings/errors. In the current game, 98 thematic images as well as gray chooser/bank frames and brains in card costs/resource counters were checked. After confirmation, all thematic images will disappear from the active UI. Aborting during zombie selection and selecting plants again also restores the native display. The tests end in the main menu.

Documents: `references/spreading-21-0-0/zombie-chooser.png`, `zombie-ui.json`, `restored-ui.json` and `abort-runtime.json`. The `debug.ui` API only reads active images within a temporary debug session.

## Debug Mod 1.2.1 — Zombie packs and running roster

The running debug roster uses the same gray bench and brain counter sprites as the zombie selection. Registered Fusion Zombies will receive 20 seed packs of original transparent portraits with the same 128x176 background as native zombies. Almanac images remain unchanged. The image test checks each exported frame and free paper space against the common template.

Release build: 0 warnings, 0 errors. Package testing: 1 test passed for all 20 cards. API Transport: 3 tests passed. New test commands `debug.plant.set` and `debug.fertilize` are limited to temporary debug boards and current board IDs. The real fertilizer path remains in fusion.

## Debug Mod 1.2.2 — finally checked on November 4th, 2026

Game launch and API testing are allowed since the API release. Steam was started as desktop user Kevin. The API waits for the visible, fully bound selection panel with `chooser.ready` and checks this state before making any changes. `debug.difficulty` reads the active difficulty; the life sign reports the actual assembly version.

Release build: 0 warnings, 0 errors. The full run `../fusion-mod/tools/smoke_puff_duo.py` consists of: zombie selection, running roster, both recipe orders of both plants, fertilizer, old board ID, ground and balloon hits as well as sticky cherry damage and delayed explosion. The active difficulty settings have been honored and not changed. Selection packages and running rosters were visually inspected. Evidence: `../fusion-mod/references/puff-duo-21-1-0/runtime.json` and the five PNG files stored there.

Currently installed: Debug 1.2.2 and Fusion 21.1.1; all four installed DLLs match their release builds byte-for-byte. Checksums and installation backup are available in the associated `validation.json`. The temporary session was abandoned; Play in the main menu. The current log contains no errors or exceptions.


## 1.2.3: Native sun and saved effects

Release build and three offline client tests passed. The API runtime test of the four new Fusions uses `debug.items` to compare saved shield points after production and real bites. The top bar shows the native rotating sun again after zombie selection; the bottom zombie bar retains its brain symbol. Screenshot and answers are available at [Pumpkin/Glacier-Evidence](../fusion-mod/references/pumpkin-glacier-21-3-0/verification.json). The fix only changes Debug Mod and gives the unchanged Sun Collector the original bank graphic for its cache. The temporary sessions have ended.


## Debug Mod 1.3.0 and Plant Fusion 21.4.0 (2026-11-04)

The real game tests use the documented local API and temporary debug boards. `validation/controls-runtime.json` contains eight completed test groups: Read Profiles and Achievements, Reject Invalid Profile Mutations, Mode Unlocks including Level Status, All Five Difficulties, Sun Collector, Real Golden Coffee Bean, Native Tombstones, Individual and Collaborative Lawnmower Operations with all four types, and all Xbox 360 buttons including Guide, Analog Sticks, and Auto Release.

Valid write profile commands were not executed to protect existing profiles. Profile and achievement lists remained unchanged during testing. The controller test can use RS to operate the real saved Sun Collector switch; the test then restores both the save state and the previous configuration file.

`../fusion-mod/references/treasure-three-mines-21-4-0/runtime.json` documents 13 completed recipe and skill checks. These include the actual animated transitions of the potato projectiles from the plant to the neighboring rows, the delayed third shot at the edge of the field, 30/5400/1800 damage, knockback, re-digging, both fertilizer paths, real resources and maps, as well as accelerated treasure fields and removal after the third map. The pictures are next to it.

The startup parameter `--debug-level=Day` was checked during the real Steam/Proton startup. It reaches `Playing` without manual selection with Peashooter and Normal Zombie as placeholders. The new shutdown check is archived separately under `validation/shutdown-runtime.json`. Release builds of Fusion, Debug Mod and Difficulty Mod are error-free. The existing transport tests also pass. Native save/load cycles were not executed due to memory protection of the debug session; the projectile identification and the reset path are also checked offline.

The final shutdown test ends the actual game process even after controller input: `processExited: true` after 9.41 seconds. Before the native Quit, the existing Fusion camera targets are resolved via their owners. Graphics degradation leaves three native RenderTexture error messages; the process still ends completely. Additionally, there are 185 existing mine/projectile checks and 427 existing sunflower/fertilizer checks with offline doubles.


## Debug Mod 1.3.1 — cards and controllers, checked on November 5th, 2026

Release build with no warnings or errors; passed three API transport tests. The actual controller run consists of 25 checks: D-pad and left stick, all eight plant sides, forward/backward scrolling with matching line, vertical navigation after swapping pooled cards, incomplete last line, card confirmation, and both selection phases until temporary debug game in progress. A focus destroyed during a phase change is restored. The current zombie catalog contains 47 cards on one page; Navigation and confirmation have been checked, a second zombie tab cannot be checked at runtime due to a lack of entries. Fusion cards have full seed pack backgrounds in the screenshot.

Receipts: [Controller Answers](../fusion-mod/references/umbrella-ice-21-5-0/controller-runtime.json), [Fusion Cards](../fusion-mod/references/umbrella-ice-21-5-0/fusion-packets.png), [last plant page](../fusion-mod/references/umbrella-ice-21-5-0/last-plant-page.png) and [Zombie Selection](../fusion-mod/references/umbrella-ice-21-5-0/zombie-controller.png). The reproducible test is below `../fusion-mod/tools/smoke_chooser_controller.py`. `plant.place offsetY:70` was tested with both ice cream pot recipe orders via the true fusion path.

The final test session was left, the virtual gamepad was removed and the game was closed normally; the actual end of the process was confirmed. The installed DLLs match the release builds byte-for-byte. During Unity mining, PvZRepatched logs three RenderTexture messages; no exceptions occurred during selection and combat tests.
