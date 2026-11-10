# Debug Mod 1.3.28

Version 1.3.28 records Basketball plant/projectile getter rectangles after all getter postfixes. Completed skipped originals retain their actual fallback result and `originalRan=false`; missing completion and getter/postfix exceptions remain unverified. Observation does not change gameplay or suppress exceptions.

Version 1.3.27 includes live projectile scalar snapshots in the existing `board` response. Plants, zombies, projectiles and `native.mMainCounter` are read during one main-thread command, allowing observations to share the same simulation tick. The new `projectiles` field uses the existing `projectiles` command's `{ board, items }` shape without renderer inspection. Opt-in `debug.basketball` can observe one explicitly bound native Basketball owner, actual geometric candidate and effective target return, and raw post-body state before Harmony finalizers. Missing hooks remain unverified. A later atomic `board` response independently shows final restored HP; projectile disappearance alone does not identify its cause. See the API documentation for limits and arming.

Version 1.3.26 adds the requested player’s existing native GamepadCursor slot to read-only `debug.cursor` telemetry. Stored DPad grid coordinates and raw board-local anchor values remain separate from the WidgetManager tool point, including before their first native synchronization. Missing or foreign board/player slots expose no dependent state. Inspection does not initialize or move a cursor.

Version 1.3.25 extends read-only `debug.cursor` telemetry with the selected player’s actual board-local widget point and native `Board.PixelToGridX/Y` result. Tool navigation can inspect the native grid independently of a missing or stale placement preview. Outside values remain raw; inspection does not move the cursor or simulate input.

Version 1.3.24 adds board-bound, read-only `debug.cursor` telemetry for native UI input investigations. It reports effective and raw plant cursor identity, InputSystem and native mouse coordinates, pause/frame state, and existing preview transforms/renderers. It does not refresh or move previews, select cards, or change gameplay. See `src/documentation.md` for the fixed response contract.

Version 1.3.22 refreshes the bundled Fusion cards for Nibble-shroom and Squish.

Version 1.3.21 adds optional `spawnX` and `spawnGroundY` to existing `debug.items.fusionDrops` snapshots. These read-only board-pixel coordinates report the Fusion owner's original materialization point when known. Older providers and restored drops with unknown provenance return null; current position and age retain their existing meanings.

Version 1.3.20 refreshes the bundled Fusion cards for Football-shroom and Doomplate-shroom.

Version 1.3.19 checks the splash container’s actual native panels instead of treating its persistent GameObject as a visible splash. A fully hidden, settled splash permits the normal menu; visible, transitioning or unknown panels remain blocked. The 85 executable startup-readiness assertions are retained in tests/StartupReadinessTests.csproj and compile the actual helper.

Version 1.3.18 waits for the actual native startup, menu transition, input layer and interactive binding before starting a debug level. The same owner guards cached chooser managers at launch. `level.start` also accepts a settled native level selection through its own validated context.

Version 1.3.17 removes the optional global Fusion camera-detachment call from game shutdown. Native shutdown and individually owned resource cleanup retain their existing responsibilities.

Version 1.3.16 adds `debug.timing` for temporary 100/120 Hz simulation tests through the native timing setter. Board tokens and debug ownership protect changes; board cleanup restores the original interval without changing native profiles. See [API documentation](src/documentation.md).

Version 1.3.15 extends `debug.items` with optional Fusion metal-drop snapshots (`fusionDrops`), including type, marker, row, position and age. Existing board validation and optional-mod behavior remain in effect.

Version 1.3.14 refreshes the bundled Fusion packet atlas with Hypno Chomper and Bolt-pult. Both original portraits use the existing chooser and seed-bank renderer.

Version 1.3.13 refreshes the bundled Fusion packet atlas with Squashwood, Torchweed and Roasted Spikeweed. Their original portraits are available in the debug chooser and seed bank through the existing card renderer.

Version 1.3.12 adds `debug.zombie.hit` for real native damage in temporary debug sessions. Board tokens, live zombie IDs and positive bounded damage are validated before native shield/helmet absorption and break/death callbacks run. See [API documentation](src/documentation.md).

Version 1.3.11 preserves native almanac entry models and pending sprite handles when refreshing temporary locks after profile/session changes. Debug shutdown retires its borrowed data provider before Unity unloads assets, so cleanup cannot begin new almanac loads. Profile actions also avoid redundant native same-profile notifications.

Version 1.3.10 skips the optional Sun Collector artwork hook when its target is unavailable. Starting Debug Mod without Sun Collector now completes Harmony patch registration.

Version 1.3.9 aligns registered zombie preview portraits by their lower center regardless of sprite pivot. `debug.preview` selects real roster cards, targets cells, snapshots actual preview renderers and optionally places through the normal click path. See [API documentation](src/documentation.md).

Version 1.3.8 frames all registered zombie thumbnails dynamically using the native packet background, including future registry variants and atlas sprites. `debug.ui` also reports actual sprite rectangles and texture dimensions for visual verification.

Version 1.3.7 adds `debug.tools` to inspect optional Lawn Glove state and temporarily retain real cooldowns on one debug board. Cooldown suppression remains enabled by default and resets on board/session changes. See [API documentation](src/documentation.md).

Version 1.3.6 adds optional `plants {"visuals":true}` diagnostics with actual Unity renderer and SortingGroup layers and orders. Plant snapshots retain their existing shape.


Version 1.3.5 removes duck-only entries from the debug chooser. Select the base zombie and let native terrain and Zombie Library choose its swimming appearance. The API adds `debug.zombie.rise` for native grave-rise tests and `speed {"next":true}` for the real speed-button event. See [API documentation](src/documentation.md).

## Dependencies

- Game/runtime: PvZ Replanted (Windows/IL2CPP), MelonLoader 0.7.3, and .NET 6.
- Required mods: none.
- Optional integrations: `FusionMod`, `LawnGlove`, `PlantLibrary`, `ZombieLibrary`, `SpeedMod`, `DifficultyMod`, `SunCollector`, `BloomEngine`, `PvZRepatched`, `ReplantAPI`.
- Building: .NET SDK with `net6.0` support and the game/MelonLoader reference assemblies declared in `DebugMod.csproj`.

## Zombie selection and graphics diagnostics (1.3.5)

All exported native zombie portraits will receive seed pack backgrounds, including the additional duck variants. The periodic gray coloring no longer overwrites the native state images of the start button. `zombies` with `visuals:true` additionally provides spine slots and actual renders for checking the ducks and arms.

## Projectile Graphics (1.3.3)

`projectiles` with `visuals:true` provides active and disabled renderers, sprite and texture identifiers. This allows runtime tests to check visible conversions. The standard call remains unchanged.

## Sighting protection and water tests (1.3.2)

Debug levels do not produce persistent zombie sightings in Zombie Library 1.2.0. The temporary almanac view remains available. The API moves native bob teams together and complements `debug.balloon.pop` for the true native landing path in the temporary debug level.

## Control and direct start (1.3.0)

After loading, `--debug-level=Day` automatically starts a temporary debug session with Peashooter and Normal-Zombie as placeholders. The local API complements Shutdown, Difficulty Mod, Sun Collector, Golden Coffee Bean, mode unlocks and their status, achievements, native profiles, gravestones, lawnmowers and all Xbox 360 buttons including guide and analog sticks. [Commands and Limits](src/documentation.md). Profile mutations are only possible in the main menu.

## Sun display (1.2.3)

The zombie selection temporary brain graphic is no longer copied to the Sun Collector cache. Its upper rotating display uses the original native sun. The fix is ​​only in Debug Mod. `debug.items` enables read checks of saved effects in the temporary debug level.

## Zombie Selection (1.2.1)

During zombie selection, the chooser, card cost, buttons, and top bar use the same gray tone as the debug zombie roster. Suns in card costs and resource counters are replaced with the existing brain icon. After confirmation, cancellation or session end, the native image, color and activation values ​​are restored. Plant selection, normal game, portraits and inputs retain their previous owners.

`debug.ui` returns the active Unity images with hierarchy path, sprite, color and size in a temporary debug session. The command only reads UI state. [API](src/documentation.md).

Standalone MelonLoader mod for PvZ Replanted. Debug level operations have been moved out of Plant Fusion 20.0.4. The mod also works without Fusion, Plant Library, Zombie Library, Lawn Glove or BloomEngine.

## Controls

Tap `debug` in the main menu. Select the background, then select up to ten plant cards and up to ten zombie cards. Plant cards cannot be left blank; the zombie selection can remain empty. Additional pages can be accessed using the arrows. Select a zombie card in the level and click on the playing field. Right click or escape cancels the placement. The natural row entry is retained on the right edge.

The session offers unlimited sun, instant card top-up, the available Almanac and, if Lawn Glove is installed, its instant top-up. Automatic sun and zombie waves, defeat, level completion and zombie loot remain locked in the debug level. The environment selection including pool, roof and fogless night pool remains.

## Libraries and optional mods

When you open the debug selection, all entries currently registered via `PlantLibrary.Plants.All` and `ZombieLibrary.Zombies.All` are adopted. Normally blocked mod plants and mod zombies that have not yet been seen also appear. Large catalogs receive additional pages. The limit of ten cards chosen applies per bar, not for the catalog. After subsequent registrations, reopen the debug selection.

Plant Library provides names, images, costs and placement rules. Variants retain their native identity and marker. When planting, `Board.AddPlant` remains responsible; this keeps the initialization and tick hooks of the registering mods in effect. Zombie Library creates mod zombies via `Zombies.Spawn`; their initialization, markers and spawn sound are retained. Pure duck variants also appear and can only be spawned in water rows.

With Fusion installed, its ingredient cards, completed Fusions, feeding, terrain rules, and imported previews are retained. Fusion still has these gameplay rules; Debug Mod has Selection, Session and Bank. There is no second debug level owner in Fusion.

Debug Mod declares all currently installed mods as optional dependencies. Conversely, the seven own mods declare `DebugMod` as optional. Integrations use native game types, standard delegates, and run-time bound methods; Debug Mod has no assembly reference to other mods. The remaining custom mods have no assembly reference to Debug Mod. Existing dependencies between the other mods are retained. Third-party mods such as BloomEngine, ReplantAPI and PvZRepatched are not changed and still do not require Debug Mod.

For the catalog connection, the updated Plant Library 1.0.4 and Zombie Library 1.1.4 are included in this release. If the libraries are missing completely, the native selection remains available.

## Memory protection

The debug level uses a temporary copy of the level definition. Previous level selection and native UI are restored upon exit. The previous locks for native board, profile and level saving as well as loading or deleting existing saves have been retained. The mod does not write its own progress files. Regular level API commands follow native storage rules; `coins.profile.set` can explicitly save the profile outside of the debug session with `persist: true`. Existing additional files from other mods retain their rules, such as Fusion activations that have already been implemented.

## Build and install

From the `/home/kevin/modding` directory:

```sh
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/debug-mod/DebugMod.csproj -c Release
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/fusion-mod/FusionMod.csproj -c Release
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/difficulty-mod/DifficultyMod.csproj -c Release
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/sun-collector-mod/SunCollector.csproj -c Release
rtk proxy python3 pvzreplanted/debug-mod/tools/install.py
```

Fusion builds the updated libraries, Lawn Glove and Speed ​​Mod. The installation saves all replaced DLLs and checksums under `backups/install-*`, uses the existing release locks and checks the installed bytes. In case of writing errors, it restores the previous files. It doesn't start the game or test programs. `installation.json` documents paths and SHA-256 values. To restore the game when the game is closed, copy the save files specified there to their original paths; Remove files with `backup: null`, especially the new `DebugMod.dll`. Reset the complete DLL set together.

The original source level is below `backups/source-*`. The native zombie portraits and optional method binding are shared sources under `../shared/`. Fusion tools `build_debug_cards.py` and `build_debug_zombies.py` continue to update debug assets offline.

## Manual acceptance

The original swap was only checked by build. The API extension is documented separately in `src/documentation.md` and `VALIDATION.md`. Further manual acceptance:

- Debug mod alone: ​​background selection, plant selection, zombie selection and return to menu.
- Entire Modset: Fusion Ingredients, Finished Fusions, Gauntlet, Previews and Unlimited Resources.
- More library mods: registered plants and zombies even without normal activation; In large catalogs, page changes and selection across page boundaries.
- Placing and removing multiple variants of the same native type; Initialization and behavior of the registering mod.
- Regular level after exiting: normal saves, waves, loot and unlocks.

## Local Command API

Version 1.1.0 adds a JSON file interface for the running game. The Python client also works on Proton on the Linux host. Commands, examples, error codes and storage rules are available in [src/documentation.md](src/documentation.md).

Debug Mod 1.2.1 unifies the zombie seed packages and adopts the gray selection roster including brain counters into the running debug level. `debug.plant.set` and `debug.fertilize` enable limited, board-bound plant and fertilizer testing in temporary debug sessions.
