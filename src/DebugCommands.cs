using System.Reflection;
using System.Text.Json;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using Il2CppReloaded.Services;
using Il2CppReloaded.TreeStateActivities;
using Il2CppSource.TreeStateActivities;
using ReplantedModUi;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DebugMod;

internal static partial class DebugCommands
{
    private static readonly Func<Board, Plant, int, int, bool>? movePlant = OptionalModApi.Bind<Func<Board, Plant, int, int, bool>>(
        "LawnGlove", "LawnGlove.PlantMover", "TryMove");
    private static readonly Func<Zombie, bool>? eyes = OptionalModApi.Bind<Func<Zombie, bool>>("FusionMod", "FusionMod.ScaredContent", "Eyes");
    private static readonly Func<Zombie, bool>? awake = OptionalModApi.Bind<Func<Zombie, bool>>("FusionMod", "FusionMod.ScaredContent", "Awakened");
    private static readonly Func<Zombie, bool>? rage = OptionalModApi.Bind<Func<Zombie, bool>>("FusionMod", "FusionMod.ScaredContent", "Raging");
    private static readonly Func<Board, int, int, bool>? fertilize = OptionalModApi.Bind<Func<Board, int, int, bool>>(
        "FusionMod", "FusionMod.Fertilizer", "Apply");
    private static readonly Func<Board, int, int, bool, bool>? treasure = OptionalModApi.Bind<Func<Board, int, int, bool, bool>>("FusionMod", "FusionMod.TreasureMines", "SetTreasure");
    internal static readonly string[] Commands = { "help", "status", "startup.continue", "levels", "level.start", "level.leave", "debug.backgrounds", "debug.start",
        "catalog.plants", "catalog.zombies", "debug.roster", "chooser", "chooser.select", "bank", "bank.set", "board", "plants", "zombies", "waves",
        "debug.plant.slots", "debug.cursor", "debug.basketball", "debug.ui", "debug.preview", "debug.items", "debug.tools", "debug.timing", "debug.treasure.set", "debug.difficulty", "debug.plant.set", "debug.fertilize", "projectiles", "coins", "waves.set", "plant.place", "zombie.place", "plant.move", "zombie.move", "plant.remove", "zombie.remove", "debug.zombie.set", "debug.zombie.hit", "debug.balloon.pop", "debug.zombie.rise", "sun.set", "coins.level.set",
        "coins.profile.set", "unlock.plant", "unlock.almanac", "unlocks.reset", "almanac", "screenshot", "speed", "fps", "settings", "settings.set", "pause" };

    internal static object Status()
    {
        var board = ApiBoard.Current;
        bool ready = DebugMode.ApiMenuReady || board?.mApp != null;
        return new { ready, code = ready ? "READY" : "GAME_NOT_READY", board = ApiBoard.Token,
            scene = board?.mApp?.GameScene.ToString(), debug = DebugMode.Active,
            paused = board != null ? (bool?)Il2Cpp.TimeUtil.IsTimePaused() : null,
            fps = Time.unscaledDeltaTime > 0 ? 1f / Time.unscaledDeltaTime : 0 };
    }

    internal static int Int(JsonElement args, string key, int min = 0, int max = int.MaxValue)
    {
        if (!args.TryGetProperty(key, out var item) || !item.TryGetInt32(out int value) || value < min || value > max)
            throw new ApiError("INVALID_ARGUMENT", $"{key}: expected integer {min}..{max}.");
        return value;
    }
    private static uint Id(JsonElement args) => args.GetProperty("id").TryGetUInt32(out uint value) ? value
        : throw new ApiError("INVALID_ARGUMENT", "id: expected unsigned 32-bit entity ID.");
    internal static string Str(JsonElement args, string key) => args.GetProperty(key).GetString()
        ?? throw new ApiError("INVALID_ARGUMENT", $"{key}: expected string.");
    internal static bool Bool(JsonElement args, string key, bool fallback = false) => args.TryGetProperty(key, out var item) ? item.GetBoolean() : fallback;
    internal static int[] Indices(JsonElement args) => args.GetProperty("indices").EnumerateArray().Select(x => x.GetInt32()).ToArray();
    internal static T EnumValue<T>(JsonElement args, string key) where T : struct, Enum
    {
        string text = args.GetProperty(key).ToString();
        if (!Enum.TryParse<T>(text, true, out var value) || !Enum.IsDefined(value))
            throw new ApiError("INVALID_ARGUMENT", $"Unknown {typeof(T).Name}: {text}.");
        return value;
    }
    private static Board EntityBoard(JsonElement args)
    {
        var board = ApiBoard.Require();
        if (Str(args, "board") != ApiBoard.Token) throw new ApiError("STALE_BOARD", "Entity IDs belong to a different board.");
        return board;
    }
    internal static IDataService Data()
    {
        if (ApiBoard.Current?.mApp?.m_dataService != null) return ApiBoard.Current.mApp.m_dataService;
        return DebugMode.ApiChooser().m_dataService;
    }
    internal static IUserService User()
    {
        if (ApiBoard.Current?.mApp?.UserService != null) return ApiBoard.Current.mApp.UserService;
        return DebugMode.ApiChooser().m_userService;
    }
    private static ISettingsService Settings()
    {
        if (ApiBoard.Current?.mApp?.SettingsService != null) return ApiBoard.Current.mApp.SettingsService;
        var input = Object.FindObjectsOfType<InputServiceActivity>(true).FirstOrDefault(x => x.m_settingsService != null);
        return input?.m_settingsService ?? throw new ApiError("GAME_NOT_READY", "Settings service is not initialized.");
    }

    internal static object Execute(string command, JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object) throw new ApiError("INVALID_ARGUMENT", "args must be an object.");
        if (command == "help") return new { protocol = 1, commands = Commands.Concat(ApiControls.Commands).ToArray(), coordinates = "zero-based col 0..8; row 0..5", entityIds = "Require board token from status." };
        if (command == "status") return Status();
        if (ApiControls.Handles(command)) return ApiControls.Execute(command, args);
        if (command == "debug.cursor") return Cursor(args);
        if (command == "fps") return new { instantaneous = Time.unscaledDeltaTime > 0 ? 1f / Time.unscaledDeltaTime : 0,
            frame = Time.frameCount, simulationTick = ApiBoard.Current?.mMainCounter,
            tickSeconds = Il2CppReloaded.Constants.Gameplay.TIME_FOR_UPDATE, board = ApiBoard.Token };
        switch (command)
        {
            case "debug.tools": return DebugTools.Execute(args);
            case "debug.timing": return DebugTiming.Execute(args);
            case "debug.basketball": return DebugBasketball.Execute(args);
            case "debug.difficulty":
                var difficultyBoard = ApiBoard.Require();
                if (!DebugMode.Owns(difficultyBoard)) throw new ApiError("DEBUG_REQUIRED", "Difficulty inspection requires a temporary debug session.");
                const BindingFlags fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                var difficultyType = OptionalModApi.Type("DifficultyMod", "DifficultyMod.Mod");
                var difficultyMod = difficultyType?.GetField("Instance", fields)?.GetValue(null);
                var ownsBoard = difficultyType?.GetMethod("Owns", fields, null, new[] { typeof(Board) }, null);
                object? difficulty = difficultyMod != null && ownsBoard?.Invoke(difficultyMod, new object[] { difficultyBoard }) is true
                    ? difficultyType?.GetField("Active", fields)?.GetValue(difficultyMod) : null;
                return new { value = difficulty == null ? 3 : Convert.ToInt32(difficulty), name = difficulty?.ToString() ?? "Normal" };
            case "debug.ui":
                if (!DebugMode.Active) throw new ApiError("DEBUG_REQUIRED", "UI inspection requires a temporary debug session.");
                string PathOf(Transform value) => value.parent == null ? value.name : PathOf(value.parent) + "/" + value.name;
                return Object.FindObjectsOfType<UnityEngine.UI.Image>().Select(image => new
                {
                    path = PathOf(image.transform), sprite = image.sprite?.name,
                    enabled = image.enabled, active = image.gameObject.activeInHierarchy,
                    texture = image.sprite?.texture is { } uiTexture
                        ? new { uiTexture.name, uiTexture.width, uiTexture.height } : null,
                    spriteRect = image.sprite is { } uiSprite
                        ? new { uiSprite.rect.x, uiSprite.rect.y, uiSprite.rect.width, uiSprite.rect.height } : null,
                    color = new { image.color.r, image.color.g, image.color.b, image.color.a },
                    width = image.rectTransform.rect.width, height = image.rectTransform.rect.height
                }).ToArray();
            case "startup.continue":
                if (ApiBoard.Current != null) throw new ApiError("INVALID_STATE", "Leave the current level before entering the main menu.");
                if (DebugMode.ApiMenuReady) return new { accepted = false, alreadyReady = true };
                var startButtons = Object.FindObjectsOfType<UnityEngine.UI.Button>()
                    .Where(button => button && button.gameObject.activeInHierarchy && button.name == "LoadBar"
                        && InSplashPanel(button.transform)).ToArray();
                if (startButtons.Length != 1 || !startButtons[0].isActiveAndEnabled || !startButtons[0].IsInteractable())
                    throw new ApiError("STARTUP_NOT_READY", "The native splash start button is not available or is still loading.");
                startButtons[0].onClick.Invoke();
                return new { accepted = true, alreadyReady = false };
            case "levels":
                var data = Data();
                var levels = data.AllLevelsData.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<LevelEntryData>>();
                return Enumerable.Range(0, levels.Count).Select(index =>
                { var level = data.AllLevelsData[index]; return new { index, level.LevelName, level.FullLevelName, mode = level.GameMode.ToString(), reloadedMode = level.ReloadedGameMode.ToString(), level.LevelNumber, area = level.GameArea.ToString(), level.SubIndex }; }).ToArray();
            case "level.start":
                if (DebugMode.Active) throw new ApiError("DEBUG_ACTIVE", "Leave the debug session first.");
                var chooser = DebugMode.ApiChooser(allowLevelSelection: true);
                int count = chooser.m_dataService.AllLevelsData.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<LevelEntryData>>().Count;
                var levelData = chooser.m_dataService.AllLevelsData[Int(args, "index", 0, count - 1)];
                chooser.m_gameplayService.SetCurrentLevelData(levelData);
                chooser.ChooseGameplay();
                return new { accepted = true, level = levelData.FullLevelName, note = "Native level transition; poll status/chooser. Existing native saves may be resumed." };
            case "level.leave":
                var leaving = ApiBoard.Require(false);
                var menu = Object.FindObjectsOfType<MainMenuActivity>(true).FirstOrDefault(item => item.TreeState != null
                    && item.TreeState.Manager?.Pointer == leaving.mApp.TreeState.Manager?.Pointer)
                    ?? throw new ApiError("GAME_NOT_READY", "Native main-menu state is unavailable.");
                // Transition through the state owner. Legacy DoBackToMain only
                // kills the board and leaves the gameplay state active.
                leaving.mApp.TreeState.TransitionTo(menu.TreeState);
                return new { accepted = true };
            case "debug.backgrounds": return DebugMode.ApiBackgrounds();
            case "debug.start": DebugMode.ApiLaunch(Int(args, "index")); return new { accepted = true };
            case "catalog.plants": return PlantCatalog(ApiBoard.Require(false));
            case "catalog.zombies":
                return new { native = Enum.GetValues<ZombieType>().Where(t => t >= ZombieType.Normal && t < ZombieType.NumZombieTypes).Select(t => new { type = t.ToString(), value = (int)t }).ToArray(),
                    registered = LibraryCatalog.Zombies().Select((card, index) => new { index, card.Name, type = card.NativeType.ToString() }).ToArray(),
                    debug = DebugMode.Active ? DebugMode.ApiZombieCatalog() : null };
            case "debug.preview": return DebugMode.ApiPreview(args);
            case "debug.zombie.hit": return HitZombie(args);
            case "debug.roster": DebugMode.ApiRoster(Indices(args)); return new { updated = true };
            case "chooser": return ChooserInfo();
            case "chooser.select":
                var screen = ApiBoard.Require(false).mApp.SeedChooserScreen ?? throw new ApiError("CHOOSER_NOT_READY", "No seed chooser.");
                return DebugSeedChooser.ApiSelect(screen, Indices(args), Bool(args, "confirm", true));
            case "almanac": return ApiUnlocks.List(args);
            case "unlock.plant": return ApiUnlocks.Plant(args);
            case "unlock.almanac": return ApiUnlocks.Almanac(args);
            case "unlocks.reset": ApiUnlocks.Reset(); return new { reset = true };
            case "settings": return SettingsInfo(Settings());
            case "settings.set": return SetSetting(Settings(), args);
            case "screenshot":
                string name = Guid.NewGuid().ToString("N") + ".png";
                CaptureScreenshot(Path.Combine(DebugCommandApi.Root, "screenshots", name));
                return new { accepted = true, file = name, note = "Capture completes after rendering. Client waits for PNG." };
            case "speed": return SetSpeed(args);
            case "coins.profile.set":
                var user = User(); int amount = Int(args, "amount", 0, 999999990);
                bool persist = Bool(args, "persist");
                if (persist && DebugMode.Active) throw new ApiError("DEBUG_SAVE_PROTECTED", "Persistent profile writes are blocked during debug sessions.");
                user.SetCoins(amount);
                bool saved = persist && user.SaveDetails();
                if (persist && !saved) throw new ApiError("SAVE_FAILED", "Coins changed in memory; profile save failed.");
                return new { amount = user.GetCoins(), persisted = saved, unit = "native profile coin units (one unit = 10 displayed coins)" };
        }
        if (!Commands.Contains(command)) throw new ApiError("UNKNOWN_COMMAND", $"Unknown command: {command}. Use help.");
        var board = command.EndsWith(".move") || command.EndsWith(".remove") || command is "debug.zombie.rise" or "debug.balloon.pop" or "debug.zombie.set" or "debug.plant.set" or "debug.plant.slots" or "debug.fertilize" or "debug.treasure.set" ? EntityBoard(args) : ApiBoard.Require(command != "bank");
        switch (command)
        {
            case "pause":
                ApiBoard.SetPaused(board, Bool(args, "paused"));
                return new { paused = Il2Cpp.TimeUtil.IsTimePaused(), controlPaused = ApiBoard.PauseOwned };
            case "sun.set":
                int player = args.TryGetProperty("player", out _) ? Int(args, "player", 0, 1) : 0;
                var sun = board.mSunMoney[player] ?? throw new ApiError("INVALID_PLAYER", "This player is not active on the board.");
                sun.Amount = Int(args, "amount", 0, 999999);
                if (DebugMode.Owns(board)) DebugMode.ApiSunOverride = true;
                return new { player, amount = board.mSunMoney[player].Amount };
            case "coins.level.set": board.mCoinsCollected = Int(args, "amount"); return new { amount = board.mCoinsCollected, field = "mCoinsCollected" };
            case "bank": return Bank(board);
            case "bank.set": return SetBank(board, args);
            case "debug.plant.slots":
                if (!DebugMode.Owns(board)) throw new ApiError("DEBUG_REQUIRED", "Slot inspection requires a temporary debug session.");
                int slotCol = Int(args, "col"), slotRow = Int(args, "row"); ApiBoard.Cell(board, slotCol, slotRow);
                return new { board = ApiBoard.Token, col = slotCol, row = slotRow,
                    placement = LibraryCatalog.SlotDiagnostics(board, slotCol, slotRow) };
            case "plants": return Plants(board, Bool(args, "visuals"));
            case "zombies": return Zombies(board, Bool(args, "visuals"));
            case "projectiles": return Projectiles(board, Bool(args, "visuals"));
            case "coins":
                var pickups = new List<Dictionary<string, object?>>();
                for (int i = 0; i < board.m_coins.Count; ++i) if (board.m_coins[i] is { mDead: false } coin) pickups.Add(Snapshot(coin));
                return new { board = ApiBoard.Token, items = pickups };
            case "debug.items": return Items(board);
            case "waves": return Waves(board);
            case "waves.set": return SetWaves(board, args);
            case "board": return BoardInfo(board);
            case "plant.place": return PlacePlant(board, args);
            case "zombie.place": return PlaceZombie(board, args);
            case "plant.move":
                var plant = FindPlant(board, Id(args));
                int pc = Int(args, "col"), pr = Int(args, "row"); ApiBoard.Cell(board, pc, pr);
                if (plant.mPlantCol == pc && plant.mRow == pr) return Snapshot(plant);
                if (movePlant != null)
                {
                    if (!movePlant(board, plant, pc, pr)) throw new ApiError("INVALID_PLACEMENT", "Plant cannot move: occupied cell, terrain or dependent support.");
                }
                else MoveNativePlant(board, plant, pc, pr);
                return Snapshot(plant);
            case "zombie.move":
                var zombie = FindZombie(board, Id(args));
                MoveZombie(board, zombie, Int(args, "col"), Int(args, "row")); return ZombieInfo(zombie);
            case "plant.remove": FindPlant(board, Id(args)).Die(); return new { removed = true };
            case "zombie.remove": FindZombie(board, Id(args)).DieNoLoot(); return new { removed = true };
            case "debug.zombie.rise":
                if (!DebugMode.Owns(board)) throw new ApiError("DEBUG_REQUIRED", "No active debug session.");
                var rising = FindZombie(board, Id(args));
                int graveCol = Int(args, "col", 0, 8), graveRow = Int(args, "row", 0, board.GetNumRows() - 1);
                if (board.GetGraveStoneAt(graveCol, graveRow) == null)
                    throw new ApiError("INVALID_PLACEMENT", "Expected an existing gravestone.");
                if (rising.mZombiePhase == ZombiePhase.RisingFromGrave || rising.IsDeadOrDying())
                    throw new ApiError("INVALID_ARGUMENT", "Expected a living zombie outside a grave rise.");
                rising.RiseFromGrave(graveCol, graveRow);
                return ZombieInfo(rising);
            case "debug.balloon.pop":
                if (!DebugMode.Owns(board)) throw new ApiError("DEBUG_REQUIRED", "No active debug session.");
                var balloon = FindZombie(board, Id(args));
                if (balloon.mZombieType != ZombieType.Balloon || balloon.mZombiePhase != ZombiePhase.BalloonFlying)
                    throw new ApiError("INVALID_ARGUMENT", "Expected a flying Balloon zombie.");
                balloon.LandFlyer(0);
                return ZombieInfo(balloon);
            case "debug.zombie.set":
                if (!DebugMode.Owns(board)) throw new ApiError("DEBUG_REQUIRED", "Test values require a temporary debug session.");
                var target = FindZombie(board, Id(args));
                int? health = args.TryGetProperty("health", out _) ? Int(args, "health", 1, 10000000) : null;
                int? freeze = args.TryGetProperty("freezeTicks", out _) ? Int(args, "freezeTicks", 0, 1000000) : null;
                if (health == null && freeze == null) throw new ApiError("INVALID_ARGUMENT", "Expected health or freezeTicks.");
                if (freeze > 0 && !target.CanBeFrozen())
                    throw new ApiError("INVALID_ARGUMENT", "This zombie cannot be frozen in its current native state.");
                if (health.HasValue) target.mBodyHealth = target.mBodyMaxHealth = health.Value;
                if (freeze.HasValue)
                {
                    if (freeze.Value == 0) target.RemoveIceTrap();
                    else { target.ApplyChill(true); target.mIceTrapCounter = freeze.Value; }
                }
                return ZombieInfo(target);
            case "debug.plant.set":
                if (!DebugMode.Owns(board)) throw new ApiError("DEBUG_REQUIRED", "Test values require a temporary debug session.");
                var testPlant = FindPlant(board, Id(args));
                testPlant.mPlantHealth = Int(args, "health", 1, testPlant.mPlantMaxHealth);
                testPlant.UpdateReanim(); testPlant.UpdateReanimColor();
                return Snapshot(testPlant);
            case "debug.treasure.set":
                if (!DebugMode.Owns(board)) throw new ApiError("DEBUG_REQUIRED", "Treasure tiles require a temporary debug session.");
                if (treasure == null) throw new ApiError("FUSION_API_UNAVAILABLE", "Treasure tiles require Plant Fusion.");
                return new { applied = treasure(board, Int(args, "col", 0, 8), Int(args, "row", 0, board.GetNumRows() - 1), Bool(args, "enabled", true)) };
            case "debug.fertilize":
                if (!DebugMode.Owns(board)) throw new ApiError("DEBUG_REQUIRED", "Fertilizer tests require a temporary debug session.");
                int col = Int(args, "col", 0, 8), row = Int(args, "row", 0, board.GetNumRows() - 1);
                if (fertilize == null) throw new ApiError("FUSION_API_UNAVAILABLE", "Fusion fertilizer is unavailable.");
                return new { applied = fertilize(board, col, row) };
        }
        throw new ApiError("UNKNOWN_COMMAND", command);
    }

    private static bool InSplashPanel(Transform transform)
    {
        for (var parent = transform.parent; parent != null; parent = parent.parent)
            if (parent.name == "SplashScreenPanel" || parent.name == "SplashScreenPanel(Clone)") return true;
        return false;
    }

    // Scalar-only snapshots avoid traversing IL2CPP object graphs. These include
    // native health, armor, phase, status timers, targeting and mod marker fields.
    internal static Dictionary<string, object?> Snapshot(object value)
    {
        var result = new Dictionary<string, object?>();
        if (value is Plant plant) result["DataID"] = (uint)plant.DataID;
        else if (value is Zombie zombie) result["DataID"] = (uint)zombie.DataID;
        else if (value is Projectile projectile) result["DataID"] = (uint)projectile.DataID;
        else if (value is Coin coin) result["DataID"] = (uint)coin.DataID;
        else if (value is LawnMower mower) result["DataID"] = (uint)mower.DataID;
        foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!(property.Name.StartsWith("m", StringComparison.Ordinal) || property.Name == "DataID") || property.GetIndexParameters().Length != 0) continue;
            var type = property.PropertyType;
            if (!type.IsPrimitive && !type.IsEnum && type != typeof(string)) continue;
            if (type == typeof(IntPtr) || type == typeof(UIntPtr)) continue;
            try { var item = property.GetValue(value); result[property.Name] = type.IsEnum ? item?.ToString() : item; }
            catch (Exception error) { result[property.Name] = new { error = error.GetBaseException().Message }; }
        }
        return result;
    }
    private static object ZombieInfo(Zombie zombie) => new { native = Snapshot(zombie), eyes = eyes?.Invoke(zombie) == true
        ? new { awake = awake?.Invoke(zombie), aggressive = rage?.Invoke(zombie) } : null };
    private static IEnumerable<Plant> LivePlants(Board b) { for (int i = 0; i < b.m_plants.Count; ++i) { var p = b.m_plants[i]; if (p != null && !p.mDead) yield return p; } }
    private static IEnumerable<Zombie> LiveZombies(Board b) { for (int i = 0; i < b.m_zombies.Count; ++i) { var z = b.m_zombies[i]; if (z != null && !z.mDead) yield return z; } }
    private static Plant FindPlant(Board b, uint id) => LivePlants(b).FirstOrDefault(p => p.DataID == (uint)id) ?? throw new ApiError("ENTITY_NOT_FOUND", "No active plant with this ID.");
    private static Zombie FindZombie(Board b, uint id) => LiveZombies(b).FirstOrDefault(z => z.DataID == (uint)id) ?? throw new ApiError("ENTITY_NOT_FOUND", "No active zombie with this ID.");
    private static object Plants(Board b, bool visuals = false) => new { board = ApiBoard.Token,
        items = LivePlants(b).Select(plant => {
            var state = Snapshot(plant);
            if (visuals && plant.mController != null)
                state["visuals"] = new {
                    renderers = plant.mController.GetComponentsInChildren<Renderer>(true).Select(r => new {
                        name = r.name, enabled = r.enabled, active = r.gameObject.activeInHierarchy,
                        order = r.sortingOrder, layer = r.sortingLayerID,
                        renderer = r.GetIl2CppType().Name }).ToArray(),
                    groups = plant.mController.GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>(true).Select(g => new {
                        name = g.name, enabled = g.enabled, active = g.gameObject.activeInHierarchy,
                        order = g.sortingOrder, layer = g.sortingLayerID }).ToArray()
                };
            return state;
        }).ToArray(), bank = Bank(b) };
    private static object Zombies(Board b, bool visuals = false) => new { board = ApiBoard.Token,
        items = LiveZombies(b).Select(z => visuals ? new { state = ZombieInfo(z), rendering = ZombieVisuals(z) } : ZombieInfo(z)).ToArray() };
    private static object ZombieVisuals(Zombie zombie)
    {
        var controller = zombie.mController;
        var skin = controller?.AnimationController?.SkeletonAnimation;
        var slots = new List<object>();
        if (skin?.skeleton != null)
            for (int i = 0; i < skin.skeleton.DrawOrder.Count; ++i)
            {
                var slot = skin.skeleton.DrawOrder.Items[i]; var bone = slot.Bone;
                var region = slot.Attachment?.TryCast<Il2CppSpine.RegionAttachment>();
                slots.Add(new { index = i, name = slot.Data.Name, attachment = slot.Attachment?.Name,
                    setupAttachment = slot.Data.AttachmentName,
                    alpha = slot.A, bone = bone.Data.Name, a = bone.A, b = bone.B, c = bone.C, d = bone.D,
                    x = bone.WorldX, y = bone.WorldY, width = region?.Width, height = region?.Height });
            }
        var tracks = new List<object>();
        var skins = new List<string>();
        if (skin?.skeleton?.Data != null)
            for (int i = 0; i < skin.skeleton.Data.Skins.Count; ++i)
                skins.Add(skin.skeleton.Data.Skins.Items[i].Name);
        if (skin?.state != null)
            for (int i = 0; i < skin.state.Tracks.Count; ++i)
                if (skin.state.Tracks.Items[i] is { } track)
                    tracks.Add(new { index = i, animation = track.Animation?.Name, time = track.TrackTime });
        return new { slots, tracks, skin = skin?.skeleton?.Skin?.Name,
            skins,
            renderers = controller?.GetComponentsInChildren<Renderer>(true).Select(r => new {
            name = r.name, enabled = r.enabled, active = r.gameObject.activeInHierarchy, order = r.sortingOrder,
            layer = r.sortingLayerID, sprite = r.TryCast<SpriteRenderer>()?.sprite?.name,
            x = r.transform.position.x, y = r.transform.position.y,
            width = r.bounds.size.x, height = r.bounds.size.y }).ToArray() };
    }
    private static object Projectiles(Board b, bool visuals)
    {
        var items = new List<object>();
        for (int i = 0; i < b.m_projectiles.Count; ++i)
        {
            var shot = b.m_projectiles[i];
            if (shot == null || shot.mDead) continue;
            var snapshot = Snapshot(shot);
            if (visuals && shot.mController != null)
            {
                string Path(Transform part)
                {
                    var parts = new List<string>();
                    for (var current = part; current != null; current = current.parent)
                    { parts.Add(current.name); if (current.Pointer == shot.mController.transform.Pointer) break; }
                    parts.Reverse(); return string.Join("/", parts);
                }
                snapshot["visuals"] = shot.mController.GetComponentsInChildren<Renderer>(true)
                    .Select(renderer => new {
                        path = Path(renderer.transform), enabled = renderer.enabled,
                        active = renderer.gameObject.activeInHierarchy,
                        sprite = renderer.TryCast<SpriteRenderer>()?.sprite?.name,
                        texture = renderer.TryCast<SpriteRenderer>()?.sprite?.texture?.name,
                        renderer = renderer.GetIl2CppType().Name,
                        x = renderer.transform.position.x, y = renderer.transform.position.y
                    }).ToArray();
            }
            items.Add(snapshot);
        }
        return new { board = ApiBoard.Token, items };
    }
    private static object BoardInfo(Board b)
    {
        var plants = LivePlants(b).ToArray(); var zombies = LiveZombies(b).ToArray();
        return new { board = ApiBoard.Token, native = Snapshot(b), sun = new int?[] { b.mSunMoney[0]?.Amount, b.mSunMoney[1]?.Amount },
            profileCoins = b.mApp.UserService.GetCoins(), levelCoins = b.mCoinsCollected, cells = Enumerable.Range(0, b.GetNumRows()).SelectMany(row => Enumerable.Range(0, 9).Select(col => new {
            col, row, water = b.IsPoolSquare(col, row), plants = plants.Where(p => p.mPlantCol == col && p.mRow == row).Select(p => (uint)p.DataID).ToArray(),
            zombies = zombies.Where(z => z.mRow == row && b.PixelToGridX(z.mPosX, z.mPosY) == col).Select(z => (uint)z.DataID).ToArray() })).ToArray(),
            plants = Plants(b), zombies = Zombies(b), projectiles = Projectiles(b, false) };
    }
    private static object Bank(Board b) => Enumerable.Range(0, b.mSeedBank.mNumPackets).Select(index => new { index, state = Snapshot(b.mSeedBank.mSeedPackets[index]) }).ToArray();
    private static object SetBank(Board b, JsonElement args)
    {
        var types = args.GetProperty("plants").EnumerateArray().Select(item =>
        {
            if (!Enum.TryParse<SeedType>(item.ToString(), true, out var type) || type < 0 || type >= SeedType.NumSeedTypes) throw new ApiError("INVALID_PLANT", item.ToString());
            return type;
        }).ToArray();
        if (types.Length < 1 || types.Length > b.mSeedBank.mSeedPackets.Length || types.Length > 10) throw new ApiError("INVALID_SELECTION", "Expected 1..10 available packets.");
        b.ClearCursor();
        DebugPlantCards.ClearApiBank();
        for (int i = 0; i < types.Length; ++i) b.mSeedBank.mSeedPackets[i].SetPacketType(types[i], SeedType.None);
        b.mSeedBank.mNumPackets = types.Length;
        if (DebugMode.Owns(b)) DebugSeedChooser.ApiBankCount(types.Length);
        b.mSeedBank.UpdateWidth(); b.mSeedBank.mIsDirty = true;
        return Bank(b);
    }
    private static object ChooserInfo()
    {
        var screen = ApiBoard.Require(false).mApp.SeedChooserScreen ?? throw new ApiError("CHOOSER_NOT_READY", "No seed chooser.");
        return new { visible = screen.Visible, ready = DebugSeedChooser.ApiReady(screen), zombiePhase = DebugSeedChooser.ZombiePhase,
            page = DebugPlantCards.CurrentPage, lastPage = DebugPlantCards.LastPage, selection = DebugChooserNavigation.Selection(), seedsInFlight = screen.mSeedsInFlight,
            choices = Enumerable.Range(0, screen.mChosenSeeds.Count).Select(index => new { index, type = screen.mChosenSeeds[index].mSeedType.ToString(), state = screen.mChosenSeeds[index].mSeedState.ToString(),
                slot = screen.mChosenSeeds[index].mSeedIndexInBank, marker = screen.mChosenSeeds[index].mImitaterType.ToString(), name = DebugPlantCards.ApiName(screen.mChosenSeeds[index]) }).ToArray() };
    }
    private static object PlantCatalog(Board b) => new { native = Enum.GetValues<SeedType>().Where(t => t >= 0 && t < SeedType.NumSeedTypes).Select(t => new { type = t.ToString(), value = (int)t }).ToArray(),
        registered = LibraryCatalog.Plants(b).Select((card, index) => new { index, card.Name, type = card.NativeType.ToString(), marker = card.Marker.ToString() }).ToArray() };
    private static object PlacePlant(Board b, JsonElement args)
    {
        int col = Int(args, "col"), row = Int(args, "row"); ApiBoard.Cell(b, col, row);
        Card? card = null; SeedType type, marker;
        if (args.TryGetProperty("registered", out _))
        {
            var cards = LibraryCatalog.Plants(b).ToArray(); card = cards[Int(args, "registered", 0, cards.Length - 1)]; type = card.NativeType; marker = card.Marker;
        }
        else { type = EnumValue<SeedType>(args, "type"); marker = args.TryGetProperty("marker", out _) ? EnumValue<SeedType>(args, "marker") : SeedType.None; }
        if (type < 0 || type >= SeedType.NumSeedTypes || marker != SeedType.None && card == null)
            throw new ApiError("INVALID_PLANT", "Use catalog.plants registered indices for mod variants; native type must be a plant.");
        int offsetY = args.TryGetProperty("offsetY", out _) ? Int(args, "offsetY", 0, 79) : 40;
        if (LibraryCatalog.PlaceApi(b, card, type, marker, col, row, offsetY) is { } nativePlaced)
        {
            if (!nativePlaced) throw new ApiError("INVALID_PLACEMENT", "Native or Fusion placement rules rejected this cell.");
        }
        else if (card?.Place != null)
        {
            if (!card.Place(b, col, row)) throw new ApiError("INVALID_PLACEMENT", "Mod placement rules rejected this cell.");
        }
        else
        {
            var reason = card?.CanPlant?.Invoke(b, col, row) ?? b.CanPlantAt(col, row, type);
            if (reason != PlantingReason.Ok) throw new ApiError("INVALID_PLACEMENT", reason.ToString());
            var created = b.AddPlant(col, row, type, marker);
            if (created == null || created.mDead || !created.mIsOnBoard) throw new ApiError("SPAWN_FAILED", "Plant creation failed.");
        }
        return new { col, row, plants = LivePlants(b).Where(p => p.mPlantCol == col && p.mRow == row).Select(Snapshot).ToArray() };
    }
    private static object PlaceZombie(Board b, JsonElement args)
    {
        int col = Int(args, "col"), row = Int(args, "row"); ApiBoard.Cell(b, col, row);
        ZombieCard? card = null; ZombieType type;
        if (args.TryGetProperty("registered", out _))
        { var cards = LibraryCatalog.Zombies(); card = cards[Int(args, "registered", 0, cards.Length - 1)]; type = card.NativeType; }
        else type = EnumValue<ZombieType>(args, "type");
        bool duck = type == ZombieType.DuckyTube;
        if (duck) type = ZombieType.Normal;
        if (type < ZombieType.Normal || type >= ZombieType.NumZombieTypes || !b.RowCanHaveZombieType(row, type) || duck && !b.IsPoolSquare(col, row))
            throw new ApiError("INVALID_PLACEMENT", "Zombie type cannot spawn in this row.");
        var previous = LiveZombies(b).Select(z => z.DataID).ToHashSet();
        bool suppression = OptionalGameplay.SuppressReplacement;
        Zombie? zombie = null;
        try
        {
            OptionalGameplay.SuppressReplacement = true;
            zombie = card != null ? card.Spawn(b, row) : b.AddZombieInRow(type, row, -1, false);
            if (zombie == null || zombie.mDead) throw new ApiError("SPAWN_FAILED", "Zombie creation failed.");
            if (!Bool(args, "entrance")) MoveZombie(b, zombie, col, row);
            return ZombieInfo(zombie);
        }
        catch
        {
            foreach (var allocated in LiveZombies(b).Where(z => !previous.Contains(z.DataID)).ToArray())
                if (allocated.mController != null) allocated.DieNoLoot(); else allocated.mDead = true;
            throw;
        }
        finally { OptionalGameplay.SuppressReplacement = suppression; }
    }
    private static void MoveZombie(Board b, Zombie z, int col, int row)
    {
        ApiBoard.Cell(b, col, row);
        if (!b.RowCanHaveZombieType(row, z.mZombieType)) throw new ApiError("INVALID_PLACEMENT", "Zombie cannot occupy this row.");
        float delta = b.GridToPixelX(col, row) - z.mPosX;
        // A native bobsled factory returns its leader. Move the existing team
        // before water entry can detach its members from the sled.
        if (z.mZombieType == ZombieType.Bobsled && z.mFollowerZombieID != null)
            foreach (var followerId in z.mFollowerZombieID)
            {
                var follower = b.ZombieTryToGet(followerId);
                if (follower == null || follower.mDead) continue;
                follower.SetRow(row); follower.mPosX += delta; follower.mPosY = follower.GetPosYBasedOnRow(row);
                follower.mX = (int)follower.mPosX; follower.mY = (int)follower.mPosY;
            }
        z.SetRow(row); z.mPosX = b.GridToPixelX(col, row); z.mPosY = z.GetPosYBasedOnRow(row);
        z.mX = (int)z.mPosX; z.mY = (int)z.mPosY; z.CheckForPool(); z.UpdateZombiePosition();
    }
    private static void MoveNativePlant(Board b, Plant p, int col, int row)
    {
        // The optional glove owns full fusion/stack movement. Native fallback
        // allows one ordinary plant, keeping support and two-cell invariants.
        if (p.mImitaterType != SeedType.None || p.mSeedType is SeedType.Cobcannon or SeedType.InstantCoffee
            || p.mSquished || p.mOnBungeeState != PlantOnBungeeState.NotOnBungee || p.NotOnGround())
            throw new ApiError("INVALID_PLACEMENT", "This plant requires LawnGlove movement support.");
        if (p.mSeedType is SeedType.Lilypad or SeedType.Flowerpot && LivePlants(b).Any(other => other.DataID != p.DataID && other.mPlantCol == p.mPlantCol && other.mRow == p.mRow))
            throw new ApiError("INVALID_PLACEMENT", "Moving support would strand another plant.");
        var reason = b.CanPlantAt(col, row, p.mSeedType);
        if (reason != PlantingReason.Ok) throw new ApiError("INVALID_PLACEMENT", reason.ToString());
        float dx = b.GridToPixelX(col, row) - b.GridToPixelX(p.mPlantCol, p.mRow), dy = b.GridToPixelY(col, row) - b.GridToPixelY(p.mPlantCol, p.mRow);
        p.mPlantCol = col; p.mRow = row; p.mX += dx; p.mY += dy;
        int order = p.CalcRenderOrder(); p.mShadowRenderOrder += order - p.mRenderOrder; p.mRenderOrder = order; p.UpdateReanim();
    }
    private static object Waves(Board b)
    {
        var array = b.mZombiesInWave.Cast<Il2CppSystem.Array>();
        int count = array.GetLength(0), size = array.GetLength(1);
        var waves = new List<object>();
        for (int wave = Math.Clamp(b.mCurrentWave, 0, count); wave < Math.Min(count, b.mNumWaves); ++wave)
        {
            var zombies = new List<string>();
            for (int slot = 0; slot < size; ++slot)
            {
                int raw = System.Runtime.InteropServices.Marshal.ReadInt32(b.mZombiesInWave.Pointer, 0x20 + 4 * (wave * size + slot));
                if (raw == (int)ZombieType.Invalid) break;
                zombies.Add(((ZombieType)raw).ToString());
            }
            waves.Add(new { wave, zombies });
        }
        return new { b.mCurrentWave, b.mNumWaves, b.mZombieCountDown, automaticSpawning = !DebugMode.Owns(b), waves,
            note = "Future zombies have no health, status or phase until instantiated; wave table can change for endless/special modes." };
    }
    private static object SetWaves(Board b, JsonElement args)
    {
        if (DebugMode.Owns(b)) throw new ApiError("DEBUG_SPAWNS_DISABLED", "Debug sessions disable automatic waves; use zombie.place.");
        var array = b.mZombiesInWave.Cast<Il2CppSystem.Array>();
        int count = array.GetLength(0), size = array.GetLength(1);
        int first = args.TryGetProperty("firstWave", out _) ? Int(args, "firstWave", b.mCurrentWave, Math.Min(count, b.mNumWaves) - 1) : b.mCurrentWave;
        int budget = Int(args, "budget", 1, 100000);
        var types = args.GetProperty("types").EnumerateArray().Select(item =>
        {
            if (!Enum.TryParse<ZombieType>(item.ToString(), true, out var type) || type < ZombieType.Normal || type >= ZombieType.NumZombieTypes
                || !Enumerable.Range(0, b.GetNumRows()).Any(row => b.RowCanHaveZombieType(row, type)))
                throw new ApiError("INVALID_ZOMBIE", $"Cannot spawn {item} on this board.");
            var definition = Data().GetZombieDefinition(type);
            if (definition == null || definition.Value <= 0) throw new ApiError("INVALID_ZOMBIE", $"No native wave cost for {type}.");
            return (Type: type, Cost: definition.Value);
        }).Distinct().ToArray();
        if (types.Length == 0 || !types.Any(t => t.Cost <= budget)) throw new ApiError("INVALID_BUDGET", "Roster is empty or every zombie exceeds the budget.");
        var plan = new List<ZombieType>(); int remaining = budget;
        while (plan.Count < size - 1)
        {
            bool added = false;
            foreach (var item in types)
                if (item.Cost <= remaining && plan.Count < size - 1)
                { plan.Add(item.Type); remaining -= item.Cost; added = true; }
            if (!added) break;
        }
        if (types.Any(t => t.Cost <= remaining)) throw new ApiError("WAVE_CAPACITY", "Budget exceeds native wave capacity; use a smaller budget.");
        if (first >= Math.Min(count, b.mNumWaves)) throw new ApiError("NO_FUTURE_WAVES", "No unspawned waves remain.");
        // Validate the complete plan before writing any native wave memory.
        for (int type = 0; type < b.mZombieAllowed.Length; ++type)
            b.mZombieAllowed[type] = types.Any(item => (int)item.Type == type);
        for (int wave = first; wave < Math.Min(count, b.mNumWaves); ++wave)
            for (int slot = 0; slot < size; ++slot)
                System.Runtime.InteropServices.Marshal.WriteInt32(b.mZombiesInWave.Pointer, 0x20 + 4 * (wave * size + slot),
                    slot < plan.Count ? (int)plan[slot] : (int)ZombieType.Invalid);
        return new { firstWave = first, budget, spent = budget - remaining, remaining, perWave = plan.Select(t => t.ToString()).ToArray(), waves = Waves(b) };
    }

    private static object SettingsInfo(ISettingsService settings) => typeof(ISettingsService).GetProperties().Where(p => p.GetIndexParameters().Length == 0)
        .Select(p => new { name = p.Name, writable = p.CanWrite, type = p.PropertyType.Name, value = p.GetValue(settings)?.ToString(),
            values = p.PropertyType.IsEnum ? Enum.GetNames(p.PropertyType) : null }).ToArray();
    private static object SetSetting(ISettingsService settings, JsonElement args)
    {
        string name = Str(args, "name");
        var property = typeof(ISettingsService).GetProperty(name) ?? throw new ApiError("INVALID_SETTING", name);
        if (!property.CanWrite || name is "PreviousResolution" or "PreviousWindowMode") throw new ApiError("READ_ONLY", name);
        var item = args.GetProperty("value"); object value;
        var type = property.PropertyType;
        if (type == typeof(bool)) value = item.GetBoolean();
        else if (type == typeof(float))
        {
            float number = item.GetSingle();
            float max = name.Contains("Volume") ? 1f : 10f;
            if (!float.IsFinite(number) || number < 0 || number > max) throw new ApiError("INVALID_ARGUMENT", $"Expected value 0..{max}.");
            value = number;
        }
        else if (type.IsEnum)
        {
            value = Enum.Parse(type, item.ToString(), true);
            if (!Enum.IsDefined(type, value)) throw new ApiError("INVALID_ARGUMENT", "Unknown setting value.");
        }
        else if (type == typeof(ReloadedResolution))
        {
            int width = Int(item, "width", 320, 16384), height = Int(item, "height", 200, 16384);
            value = new ReloadedResolution { Width = width, Height = height };
        }
        else throw new ApiError("UNSUPPORTED_SETTING", name);
        property.SetValue(settings, value);
        return new { name, value = property.GetValue(settings)?.ToString() };
    }
    private static unsafe void CaptureScreenshot(string path)
    {
        // Unity 6's generated string wrapper references an unavailable IL2CPP
        // ReadOnlySpan.GetPinnableReference. Pass the native UTF-16 span directly.
        fixed (char* text = path)
        {
            var span = new UnityEngine.Bindings.ManagedSpanWrapper(text, path.Length);
            ScreenCapture.CaptureScreenshot_Injected(ref span, 1, ScreenCapture.StereoScreenCaptureMode.LeftEye);
        }
    }

    private static object SetSpeed(JsonElement args)
    {
        ApiBoard.Require();
        var cycler = Object.FindObjectsOfType<Il2Cpp.TimeCycler>().FirstOrDefault(c => c && c.gameObject.activeInHierarchy)
            ?? throw new ApiError("SPEED_NOT_READY", "No active native time cycler.");
        if (args.TryGetProperty("index", out _) && Bool(args, "next"))
            throw new ApiError("INVALID_ARGUMENT", "Specify index or next, not both.");
        if (args.TryGetProperty("index", out _)) cycler._setTimeIndex(Int(args, "index", 0, cycler.m_cycles.Length - 1));
        if (Bool(args, "next")) cycler.m_button.onClick.Invoke();
        return new { selected = cycler.m_cycleIndex, effective = Il2Cpp.TimeUtil.GetFlowingTimeScale(),
            cycles = Enumerable.Range(0, cycler.m_cycles.Length).Select(index => new { index, scale = cycler.m_cycles[index].TimeScale }).ToArray() };
    }
}
