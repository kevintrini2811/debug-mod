# Debug Mod 1.3.7

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
