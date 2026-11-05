# Debug Mod 1.1.1: local Command API

## Requirements and call

The `DebugMod.dll` installed must have version 1.1.1. Start PvZ Replanted normally via Steam. The API is initialized when the mod is loaded and also works under Proton. Commands are executed on the main Unity thread. No network port is opened.

The client is located in the source project under `debug-mod/tools/debug_api.py`. From the working directory `/home/kevin/modding`:

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py status
python3 pvzreplanted/debug-mod/tools/debug_api.py help
python3 pvzreplanted/debug-mod/tools/debug_api.py COMMAND '{"argument":"value"}'
```

Optional: `--game-dir '/anderer/Spielpfad'`, `--timeout 30`. The second position argument is a JSON object. Returns are JSON. Exit code 0 means success; Exit code 1 means error. Before the game starts, the client delivers `GAME_NOT_READY`. During startup, `status` shows `ready: false` and `code: GAME_NOT_READY`. A visible start screen is not yet a finished main menu. With `startup.continue` its native button “CLICK TO START!” trigger, even under Wayland without input automation:

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py startup.continue
python3 pvzreplanted/debug-mod/tools/debug_api.py status
```

The command only triggers the active, operable `LoadBar` button in the native `SplashScreenPanel`. Loading, profile and state changes remain in the game. `STARTUP_NOT_READY` means that the button is missing or cannot yet be operated. After `accepted: true` wait for `status.ready: true`. Repeat in the finished main menu delivers `alreadyReady: true`; `INVALID_STATE` is reported during a level. License and profile dialogs are not automatically confirmed.

`accepted: true` acknowledges a requested state change. Level changes, intros and card selection then run through the game. Then check `status` or `chooser`. A timeout does not prove that a mutation has not occurred. Check the condition before repeating.

## Levels and mini-games

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py levels
python3 pvzreplanted/debug-mod/tools/debug_api.py level.start '{"index":0}'
python3 pvzreplanted/debug-mod/tools/debug_api.py level.leave
```

`levels` outputs all level definitions provided by the game: adventure, survival, puzzles, mini-games and other existing modes. Use their `index`; Numbers may change between game versions. `level.start` requires main menu or native level selection. The native selection decides on an existing saved level, special intros and the loading process. To switch, first use `level.leave` and wait for the main menu. A selected special mode must be supported by the game and the installed mods.

## Debug-Level

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.backgrounds
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.start '{"index":0}'
python3 pvzreplanted/debug-mod/tools/debug_api.py chooser
python3 pvzreplanted/debug-mod/tools/debug_api.py chooser.select '{"indices":[0,1,2],"confirm":true}'
```

`debug.backgrounds` provides backgrounds and opens the existing debug selection. `debug.start` uses their index. The debug level remains the owner of its temporary level copy and its memory protection.

`chooser` returns all current selection objects with index, native state and, if applicable, mod names. There are two phases in the debug session: first plants, then zombies. After confirming the plants, wait for `chooser` to report `zombiePhase: true`. Then choose the zombies:

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py chooser.select '{"indices":[0,1],"confirm":true}'
```

The zombie selection can be empty: `{"indices":[],"confirm":true}`. A maximum of ten cards are possible per debug bar. `confirm: false` only changes the selection. `chooser.select` also works with normal card selection; the native capacity and availability apply there. Hidden native plants are not unlocked silently.

The manual zombie bar can be replaced in the finished debug level:

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py catalog.zombies
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.roster '{"indices":[0,1,2]}'
```

This refers to the indices from `catalog.zombies.debug`. They differ from the indices under `registered`.

## Plant cards and catalogs

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py catalog.plants
python3 pvzreplanted/debug-mod/tools/debug_api.py catalog.zombies
python3 pvzreplanted/debug-mod/tools/debug_api.py bank
python3 pvzreplanted/debug-mod/tools/debug_api.py bank.set '{"plants":["Peashooter","Sunflower","Wallnut"]}'
```

The catalogs distinguish native enum names from registered mod variants. `catalog.plants` requires an existing board because costs and mod catalogs can depend on the level. `bank.set` replaces player 0 cards in the current level with up to ten native plants; the actual packet capacity must not be exceeded. You choose mod variants via `chooser.select` or place them directly via the catalog. Mod catalogs are re-determined when accessed; use indexes from the same game/mod configuration.

## Placing and repositioning

Coordinates start at zero: `col` is 0 to 8; `row` is 0 to 4 or 5. Roster choice does not limit API placements. Terrain, support and occupancy rules remain active.

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py plant.place '{"type":"Peashooter","col":2,"row":1}'
python3 pvzreplanted/debug-mod/tools/debug_api.py zombie.place '{"type":"Normal","col":8,"row":1}'
python3 pvzreplanted/debug-mod/tools/debug_api.py zombie.place '{"type":"Normal","col":8,"row":1,"entrance":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py plant.place '{"registered":0,"col":2,"row":1}'
python3 pvzreplanted/debug-mod/tools/debug_api.py zombie.place '{"registered":0,"col":8,"row":1}'
```

`registered` denotes an entry from `catalog.plants.registered` or `catalog.zombies.registered`. Libraries and Fusion remain responsible for variants, initialization and special rules. Any native markers will not be accepted. `entrance: true` maintains the zombie's natural entry position. Invalid cells return `INVALID_CELL`; `INVALID_PLACEMENT` provides reasons for violating placement rules. Not every special zombie can be created outside of its native mode. Failed zombie creation clears up the newly created zombies of this attempt.

You can get current IDs and board identification via `plants`, `zombies` and `status`. Moving and removing require the appropriate board ID. This means that old IDs cannot change other units after a level change.

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py plant.move '{"board":"BOARDKENNUNG","id":65537,"col":3,"row":1}'
python3 pvzreplanted/debug-mod/tools/debug_api.py zombie.move '{"board":"BOARDKENNUNG","id":65537,"col":7,"row":2}'
python3 pvzreplanted/debug-mod/tools/debug_api.py plant.remove '{"board":"BOARDKENNUNG","id":65537}'
python3 pvzreplanted/debug-mod/tools/debug_api.py zombie.remove '{"board":"BOARDKENNUNG","id":65537}'
```

Lawn Glove has existing rules for moving plants and stacking. Without Lawn Glove, the API supports common individual plants; Dependent documents, special conditions and doubtful plants are protected. Zombies use native row, pool and position updating.

## resources

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py sun.set '{"amount":5000}'
python3 pvzreplanted/debug-mod/tools/debug_api.py coins.level.set '{"amount":100}'
python3 pvzreplanted/debug-mod/tools/debug_api.py coins.profile.set '{"amount":1000}'
python3 pvzreplanted/debug-mod/tools/debug_api.py coins.profile.set '{"amount":1000,"persist":true}'
```

`sun.set` allows optional `player: 0` or `1`. In the debug level, the automatic reset to 9990 is then switched off for this session; its free placement remains active.

`coins.level.set` sets the native level counter `mCoinsCollected`. This is not an additional wallet or change to coin items that are already flying. `coins.profile.set` uses native profile units: one unit is equal to ten displayed coins. Without `persist: true`, only the RAM is changed; later native saves can inherit this value. `persist: true` invokes native profile storage. This explicit storage is locked during the debug session. Make a backup before making permanent profile changes.

## Zombie budget and automatic waves in normal levels

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py waves
python3 pvzreplanted/debug-mod/tools/debug_api.py waves.set '{"budget":30,"types":["Normal","TrafficCone","Pail"]}'
python3 pvzreplanted/debug-mod/tools/debug_api.py waves.set '{"firstWave":3,"budget":50,"types":["Normal","Football"]}'
```

`budget` is native zombie points per wave not yet spawned, no sun cost or hit points. The API fills each remaining wave with the chosen types in turn, as long as points last. It also updates the native zombie selection `mZombieAllowed`. It reports points spent, remaining budget and the finished plan. The budget is a ceiling; indivisible remaining points remain. An assignment that is too large will be rejected with `WAVE_CAPACITY` before writing. `firstWave` is zero-based and cannot denote a wave that has already started. If not specified, the change begins with the next wave that has not yet been spawned (`mCurrentWave`). Existing zombies remain. Special modes and later survival levels can regenerate their native table. In the debug level, automatic waves are switched off; `zombie.place` is used there to generate.

## Unlock, Block and Almanac

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.plant '{"type":"Peashooter","unlocked":false}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.plant '{"type":"Peashooter","unlocked":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.almanac '{"kind":"plant","type":"Peashooter","unlocked":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.almanac '{"kind":"zombie","type":"Yeti","unlocked":false}'
python3 pvzreplanted/debug-mod/tools/debug_api.py almanac
python3 pvzreplanted/debug-mod/tools/debug_api.py almanac '{"unlockedOnly":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlocks.reset
```

Overrides apply until the end of the game or `unlocks.reset`; they do not change adventure levels, purchases or achievements. Plant selection and almanac entry are separate activations. If necessary, reopen plant selections that have already been opened. Almanac data is updated. Explicit locks take precedence over native debug reveal. For registered variants from `almanac.plants` and `almanac.zombies`, use `owner` and `id` from the answer instead:

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.plant '{"owner":"fusion","id":"ID_AUS_ALMANAC","unlocked":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.almanac '{"kind":"plant","owner":"fusion","id":"ID_AUS_ALMANAC","unlocked":false}'
python3 pvzreplanted/debug-mod/tools/debug_api.py unlock.almanac '{"kind":"zombie","owner":"fusion","id":"ID_AUS_ALMANAC","duck":false,"unlocked":true}'
```

Plant Library 1.0.4 and Zombie Library 1.1.4 have their own session-related overrides for this. The almanac lists also update when the mod almanac is open. Duck variants have separate locks. `plantUnlocked` can be `null` without a loaded board if normal activation requires a profile-dependent board. Without these library versions, the native API commands remain available; Report mod overrides `LIBRARY_API_UNAVAILABLE`.

## Status queries

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py board
python3 pvzreplanted/debug-mod/tools/debug_api.py plants
python3 pvzreplanted/debug-mod/tools/debug_api.py zombies
python3 pvzreplanted/debug-mod/tools/debug_api.py projectiles
python3 pvzreplanted/debug-mod/tools/debug_api.py waves
python3 pvzreplanted/debug-mod/tools/debug_api.py fps
```

`board` outputs all playing field cells with water status and IDs of the plants and zombies there. Additionally, the answer includes sun per active player, profile coins and the level coin counter. Full unit lists are included; Zombies outside the visible grid remain in the list.

`almanac` contains separate lists `native`, `plants` and `zombies`. The last two lists also provide `owner`, `id` and possibly `duck` for registered mod variants.

Snapshots contain native `m*` and `DataID` scalar fields: for example, health, maximum health, armor, position, row, phase, condition, status timer, target information, and mod markers. Plant top-up is available for bank cards under `mRefreshCounter`, `mRefreshTime` and `mRefreshing`; Attack timers belong to the respective plant object. Zombies additionally include the optional eye zombie condition `awake` and `aggressive`. Projectile snapshots include, among other things, type, movement, position, speed and target ID. Timers and counters are output in native units; they are not automatically seconds. The available field names depend on the game version. Errors when reading individual fields are shown in the respective field.

`waves` shows scheduled types and spawn countdown. Zombies that have not yet been spawned have no instance life, status effects, or active phase. Such values ​​are not invented. Random or special spawns outside of the wave table can only appear after they are natively planned or generated.

`fps` measures rendered images via Unity's unscaled frame time, not images supplemented by external frame generation. `frame` counts render frames; `simulationTick` contains the native `mMainCounter` if the board is present, otherwise `null`. `tickSeconds` returns the native time interval per tick. Compare simulation sticks only with the same `board` identifier. This allows rendering performance and actual simulation progress to be measured separately.

## Screenshot, speed and settings

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py screenshot
python3 pvzreplanted/debug-mod/tools/debug_api.py speed
python3 pvzreplanted/debug-mod/tools/debug_api.py speed '{"index":1}'
python3 pvzreplanted/debug-mod/tools/debug_api.py settings
python3 pvzreplanted/debug-mod/tools/debug_api.py settings.set '{"name":"MusicVolume","value":0.25}'
python3 pvzreplanted/debug-mod/tools/debug_api.py settings.set '{"name":"SoundEffectVolume","value":0.5}'
python3 pvzreplanted/debug-mod/tools/debug_api.py settings.set '{"name":"VSync","value":false}'
python3 pvzreplanted/debug-mod/tools/debug_api.py settings.set '{"name":"Resolution","value":{"width":1920,"height":1080}}'
python3 pvzreplanted/debug-mod/tools/debug_api.py pause '{"paused":true}'
python3 pvzreplanted/debug-mod/tools/debug_api.py pause '{"paused":false}'
```

`pause` has exactly one entry in the native pause counter of `TimeUtil`. Repeated pauses do not add additional pauses. `paused` and `status.paused` report `TimeUtil.IsTimePaused()`; `controlPaused` shows the API's own pause. Resume, board change, board end and API shutdown only release this own pause. Other break owners remain. The API does not require a visible pause button for this.

Screenshots end up under `UserData/DebugModApi/screenshots/`. The client waits for a complete PNG file and returns its Linux path. Direct protocol users first receive `accepted: true` and have to wait for the end of the file themselves.

`speed` provides the current native speed levels and their indices; Installed speed mod extensions are included. `speed` with `index` uses native timing. Game pauses and existing slowdowns remain their responsibility; the response contains the effective value of the native timing. When the CPU is overloaded, the actual simulation achieved may be slower; this can be measured via `fps.simulationTick` and `fps.tickSeconds`.

`settings` provides all native settings with type, writability and enum values. `settings.set` uses native setters including their events and storage behavior. Supported are sound, music, speech, window mode, resolution, VSync, vibration, high contrast, half speed, screen shake, gameplay speed, controller speed and other writable scalar settings. Volume levels are between 0 and 1. Historical values ​​`PreviousResolution` and `PreviousWindowMode` remain protected. Changes can be saved permanently by the game.

## File protocol for other clients

Base directory: `<Spiel>/UserData/DebugModApi/`. `status.json` contains protocol version, mod version, game session, PID and `updatedAt` in Unix milliseconds. Signs of life older than five seconds are considered unavailable. `owner.lock` prevents two API owners in the same game directory.

1. Read the current `session` from `status.json`.
2. Generate a UUID as 32 hex characters without hyphens.
3. First write JSON as a temporary file. Atomically rename it to `requests/<UUID>.json`.
4. Read the appropriate file from `responses/<UUID>.json`. Remove the answer after reading.

```json
{
  "protocol": 1,
  "session": "AKTUELLE_SESSION",
  "expiresAt": 1793710800000,
  "command": "sun.set",
  "args": {"amount": 5000}
}
```

`expiresAt` must be in the near future. Expired requests are rejected without execution. Restarts create a new session; old requests return `STALE_SESSION`. The maximum request size is 64 KiB. A maximum of four requests are processed per API run. If a client requires a specific order, it waits for each response before sending the next command. Multiple commands do not form a transaction.

```json
{"protocol":1,"id":"UUID","session":"SESSION","ok":true,"result":{"amount":5000}}
```

```json
{"protocol":1,"id":"UUID","session":"SESSION","ok":false,"error":{"code":"INVALID_PLACEMENT","message":"NeedsPot"}}
```

Important error codes are `GAME_NOT_READY`, `BOARD_NOT_READY`, `LEVEL_NOT_READY`, `MAIN_MENU_REQUIRED`, `CHOOSER_NOT_READY`, `INVALID_ARGUMENT`, `INVALID_CELL`, `INVALID_PLACEMENT`, `ENTITY_NOT_FOUND`, `STALE_BOARD`, `STALE_SESSION`, `REQUEST_EXPIRED`, `UNKNOWN_COMMAND`, `DEBUG_SAVE_PROTECTED`, `SAVE_FAILED` and `COMMAND_FAILED`. The API does not accept arbitrary method calls, shell commands, or output paths. Access is limited to processes with file rights in the local game directory.


## Testing and installation

```sh
# Build ohne Spielstart:
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/debug-mod/DebugMod.csproj -c Release
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/plant-library/PlantLibrary.csproj -c Release
rtk proxy pvzreplanted/.tools/dotnet/dotnet build pvzreplanted/zombie-library/ZombieLibrary.csproj -c Release
rtk proxy python3 pvzreplanted/debug-mod/tools/install.py --only DebugMod PlantLibrary ZombieLibrary

# Offline-Clienttests ohne Spielstart:
python3 -m unittest discover -s pvzreplanted/debug-mod/tools -p test_debug_api.py -v

# Explicit runtime test in an already running game, starting from the main menu:
python3 pvzreplanted/debug-mod/tools/smoke_debug_api.py
```

The runtime test starts a temporary debug session, changes its contents and ends it again. Among other things, it checks selection, placement, repositioning, invalid input, resources, native and registered activations, screenshot, settings and speed. The test writes its JSON responses to `debug-mod/validation/debug-runtime.json`. It does not request permanent profile storage. Additional manual or executed checks are in `debug-mod/VALIDATION.md`.

The installer only replaces the selected DLLs, updates affected checksums and backs up previous files under `debug-mod/backups/install-*`. It doesn't start the game. To restore the game when the game is closed, restore the files using the `installation.json` there. For the original DebugMod.dll 1.0.0, the backup level is below `backups/install-20261103-125712-879568`. The original libraries and their checksums were saved under `backups/install-20261103-130931-042599`. In the case of a complete dismantling, observe both manifestos; later backups contain intermediate versions of the API extension.

## Reproducible combat load at debug level

`debug.zombie.set` requires a current board ID, an active zombie ID and a temporary debug session. `health` sets body life and maximum body life to 1 to 1,000,000. `freezeTicks` sets the duration of native icing to 1 to 1,000,000 simulation ticks; 0 removes the icing natively. At least one of the two fields is required. Both values ​​are checked before the first change. Armor remains separate. Board changes protect old IDs like `zombie.move`.

```sh
python3 pvzreplanted/debug-mod/tools/debug_api.py debug.zombie.set '{"board":"BOARDKENNUNG","id":65537,"health":1000000,"freezeTicks":100000}'
```

This means that test targets remain stationary during FPS comparisons and survive many projectiles. The session does not change scores.
