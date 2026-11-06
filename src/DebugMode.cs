using System.Reflection;
using HarmonyLib;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using Il2CppReloaded.Services;
using Il2CppReloaded.TreeStateActivities;
using Il2CppReloaded.UI;
using Il2CppSource.TreeStateActivities;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace DebugMod;

// Debug content has no SeedType registration or profile unlock. This session
// owns a cloned level definition and exactly one board; nothing is serialized.
internal static partial class DebugMode
{
    private sealed record Background(string Name, LevelEntryData Level, BackgroundType Type, bool NoFog = false);
    private static readonly List<Background> backgrounds = new();
    private static readonly List<int> zombies = new();
    private static readonly ZombieType[] zombieTypes = Enum.GetValues<ZombieType>()
        .Where(type => type >= ZombieType.Normal && type < ZombieType.NumZombieTypes && type != ZombieType.DuckyTube).ToArray();
    private static GameplayChooserActivity? chooser;
    private static DebugUi? ui;
    private static DebugLevelPicker? levelPicker;
    private static DebugCardArt? art;
    private static DebugZombiePreview? preview;
    internal static DebugCardArt Art => art!;
    private static ZombieCard[] registeredZombies = Array.Empty<ZombieCard>();
    internal static int ZombieCount => zombieTypes.Length + registeredZombies.Length;
    internal static void SetZombies(IEnumerable<int> chosen) { zombies.Clear(); zombies.AddRange(chosen); }
    private static bool refreshUi;
    private static LevelEntryData? level, previousLevel;
    private static Board? board;
    private static bool launching, picking;
    [ThreadStatic] internal static bool CreatingBoard;
    private static int backgroundIndex, word, selectedZombie = -1;
    private static string error = "";
    internal static bool Active => level != null;
    internal static bool NoFog(Board value) => Owns(value) && backgrounds[backgroundIndex].NoFog;
    internal static Board? SessionBoard => board != null && Owns(board) ? board : null;
    internal static bool Owns(Board? value) => Active && value != null
        && (board != null ? value.Pointer == board.Pointer : launching && value.mApp?.LevelData?.Pointer == level!.Pointer);
    internal static bool Owns(GameplayActivity value) => Active && value.LevelData?.Pointer == level!.Pointer;

    internal static void TraceShootingPot(Plant plant, string message)
    {
        if (Owns(plant.mBoard))
            Mod.Instance.LoggerInstance.Msg($"TriPot plant={(uint)plant.DataID}: {message}");
    }

    private static bool InMainMenu() => Object.FindObjectsOfType<MainMenuPanelView>()
        // LevelSelectScreen stays enabled while its carousel is hidden.
        // Its native enter/leave state, not GameObject activity, owns visibility.
        .Any(view => view && view.gameObject.activeInHierarchy
            && (view.LevelSelect == null || !view.LevelSelect.m_levelSelectIsActive));

    internal static void Update()
    {
        ReadInput();
        DebugAlmanac.Update();
        art?.Update();
        DebugSeedChooser.Update();
        DebugZombieChooserTheme.Update();
        DebugPlantCards.Update();
        DebugBankPreviews.Update(board);
        levelPicker?.Update();
        RefreshUi();
        ui?.SetVisible(picking || Owns(board) && !board!.mPaused && board.mApp.GameScene == GameScenes.Playing);
        ui?.Resize();
        ui?.SetPlacing(!picking && Placing);
        if (Owns(board) && !ApiSunOverride)
            foreach (var label in Object.FindObjectsOfType<Il2CppTMPro.TMP_Text>())
                if (label.name == "SunAmountLabel") label.text = "∞";
        UpdatePreview();
    }

    private static void ReadInput()
    {
        // LateUpdate runs after Unity's dynamic input update. Read once per
        // rendered frame, before refreshing debug UI that could throw.
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (!Active && !picking)
        {
            foreach (var key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame || key.keyCode < Key.A || key.keyCode > Key.Z) continue;
                var expected = new[] { Key.D, Key.E, Key.B, Key.U, Key.G };
                if (!InMainMenu()) { word = 0; break; }
                word = key.keyCode == expected[word] ? word + 1 : key.keyCode == Key.D ? 1 : 0;
                if (word != expected.Length) continue;
                word = 0;
                Open();
                break;
            }
        }
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            selectedZombie = -1;
            refreshUi = true;
            if (picking) ClosePicker();
        }
    }

    private static BackgroundType Type(GameArea area) => area switch
    {
        GameArea.Night => BackgroundType.Night, GameArea.Pool => BackgroundType.Pool,
        GameArea.Fog => BackgroundType.Fog, GameArea.Roof => BackgroundType.Roof,
        GameArea.Boss => BackgroundType.Boss, GameArea.ZenGarden => BackgroundType.Greenhouse,
        GameArea.TreeOfWisdom => BackgroundType.TreeOfWisdom, GameArea.China => BackgroundType.China,
        _ => BackgroundType.Day
    };

    private static void Open()
    {
        chooser = ApiChooser();
        if (chooser == null) return;
        LoadPicker();
    }

    internal static bool ApiMenuReady => Object.FindObjectsOfType<MainMenuPanelView>()
        .Any(view => view && view.gameObject.activeInHierarchy)
        && Object.FindObjectsOfType<MainMenuActivity>().Any(menu => menu.m_container != null && menu.TreeState?.Manager != null);

    internal static GameplayChooserActivity ApiChooser()
    {
        if (ApiBoard.Current != null) throw new ApiError("MAIN_MENU_REQUIRED", "Return to the main menu before starting a level.");
        if (!ApiMenuReady) throw new ApiError("GAME_NOT_READY", "Main menu has not finished loading.");
        var menu = Object.FindObjectsOfType<MainMenuActivity>()
            .FirstOrDefault(value => value.m_container != null);
        var found = Object.FindObjectsOfType<GameplayChooserActivity>(true)
            .FirstOrDefault(value => value.TreeState != null && value.m_gameplayState != null
                && menu != null && value.TreeState.Manager?.Pointer == menu.TreeState.Manager?.Pointer);
        if (found == null || menu == null)
        {
            Mod.Instance.LoggerInstance.Error("Debug: native gameplay services are unavailable in the main menu.");
            throw new ApiError("GAME_NOT_READY", "Native gameplay services are not initialized.");
        }
        // The chooser is inactive in the main menu, so its PreLoad injection
        // has not run yet. Resolve its dependencies from the active menu scope
        // without entering the ordinary chooser (which can load a saved game).
        menu.m_container.Inject(found);
        return found;
    }

    private static void LoadPicker()
    {
        art?.Dispose();
        art = new DebugCardArt(chooser!.m_dataService);
        registeredZombies = LibraryCatalog.Zombies();
        backgrounds.Clear();
        var levels = chooser.m_dataService.AllLevelsData.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<LevelEntryData>>();
        var seen = new HashSet<string>();
        for (int i = 0; i < levels.Count; ++i)
        {
            var entry = chooser.m_dataService.AllLevelsData[i];
            if (entry.BackgroundPrefab == null || !entry.BackgroundPrefab.RuntimeKeyIsValid()) continue;
            string key = entry.BackgroundPrefab.AssetGUID;
            if (seen.Add(key)) backgrounds.Add(new($"{entry.GameArea} · {entry.LevelNumber}", entry, Type(entry.GameArea)));
            else if (entry.EntryThumbnail != null && entry.EntryThumbnail.RuntimeKeyIsValid())
            {
                int index = backgrounds.FindIndex(value => value.Level.BackgroundPrefab.AssetGUID == key);
                var current = backgrounds[index].Level.EntryThumbnail;
                if (current == null || !current.RuntimeKeyIsValid())
                    backgrounds[index] = new($"{entry.GameArea} · {entry.LevelNumber}", entry, Type(entry.GameArea));
            }
        }
        var nightPool = backgrounds.FirstOrDefault(value => value.Type == BackgroundType.Fog);
        if (nightPool != null) backgrounds.Add(new("Night pool · no fog", nightPool.Level, BackgroundType.Fog, true));
        backgrounds.Sort((left, right) => left.Type.CompareTo(right.Type));
        zombies.Clear();
        picking = true; error = "";
        refreshUi = true;
    }

    private static void ClosePicker()
    {
        picking = false;
        levelPicker?.Dispose(); levelPicker = null;
        ui?.Dispose(); ui = null;
        refreshUi = false;
        if (!Active) { art?.Dispose(); art = null; }
    }

    private static Action Click(Action action) => () =>
    {
        try { action(); }
        catch (Exception exception)
        {
            error = exception.Message;
            Mod.Instance.LoggerInstance.Error($"Debug UI: {exception}");
        }
        refreshUi = true;
    };

    private static void Button(Rect bounds, string text, Action action, bool enabled = true) =>
        ui!.Button(bounds, text, Click(action), enabled);

    private static void Card(Rect bounds, string text, Func<Sprite?> sprite, Action action,
        bool selected = false, bool enabled = true, Func<Sprite?>? badge = null) =>
        ui!.Card(bounds, text, sprite, Click(action), selected, enabled, badge);

    internal static Sprite? ZombiePreviewArt(int index) => index >= zombieTypes.Length ? registeredZombies[index - zombieTypes.Length].Portrait() ?? art!.ZombiePortrait(registeredZombies[index - zombieTypes.Length].NativeType) : art!.ZombiePortrait(zombieTypes[index]);
    internal static Sprite? ZombieArt(int index) => index >= zombieTypes.Length
        ? (registeredZombies[index - zombieTypes.Length].Portrait()
            ?? registeredZombies[index - zombieTypes.Length].Thumbnail()) is { } thumbnail
            ? art!.RegisteredPacket(thumbnail) : art!.Zombie(registeredZombies[index - zombieTypes.Length].NativeType)
        : art!.Zombie(zombieTypes[index]);
    private static Sprite? ZombieHeadArt(int index) => index >= zombieTypes.Length ? null : art!.ZombieHead(zombieTypes[index]);

    private static void RefreshUi()
    {
        if (!refreshUi) return;
        refreshUi = false;
        levelPicker?.Dispose(); levelPicker = null;
        ui?.Dispose(); ui = null;
        if (!picking && (!Owns(board) || board!.mApp.GameScene != GameScenes.Playing)) return;
        try
        {
            if (picking) DrawPicker();
            else { ui = new DebugUi(false); DrawDeck(); }
        }
        catch (Exception exception)
        {
            ui?.Dispose(); ui = null;
            Mod.Instance.LoggerInstance.Error($"Debug UI: {exception}");
            ClosePicker();
        }
    }

    private static void DrawPicker()
    {
        levelPicker = new DebugLevelPicker(backgrounds.Select((background, index) =>
            new DebugLevelPicker.Entry(background.Name, () => art!.Level(background.Level), Click(() =>
            {
                backgroundIndex = index; Launch();
                Mod.Instance.LoggerInstance.Msg($"Debug: Background {background.Name}; card selection opened.");
            }))).ToArray(), ClosePicker, error);
    }

    internal static string ZombieName(int index) => index >= zombieTypes.Length
        ? registeredZombies[index - zombieTypes.Length].Name : art!.ZombieName(zombieTypes[index]);

    private static void Launch()
    {
        if (chooser == null || Active) return;
        var background = backgrounds[backgroundIndex];
        previousLevel = chooser.m_gameplayService.CurrentLevelData;
        // The selected prefab supplies scenery. An ordinary survival template
        // supplies waves and gameplay, including for boss/garden backgrounds.
        var mode = background.Type switch
        {
            BackgroundType.Night => GameMode.SurvivalNormalStage2,
            BackgroundType.Pool => GameMode.SurvivalNormalStage3,
            BackgroundType.Fog => GameMode.SurvivalNormalStage4,
            BackgroundType.Roof or BackgroundType.Boss => GameMode.SurvivalNormalStage5,
            _ => GameMode.SurvivalNormalStage1
        };
        var template = chooser.m_dataService.GetFirstLevelDataForGameMode(mode)
            ?? throw new InvalidOperationException("Native survival level data is unavailable.");
        level = Object.Instantiate(template);
        level.name = "DebugSession";
        level.m_backgroundPrefab = background.Level.BackgroundPrefab;
        level.m_gameArea = background.Level.GameArea;
        level.m_preloadLabels = template.PreloadLabels.Concat(background.Level.PreloadLabels).Distinct().ToArray();
        level.m_reloadedGameMode = ReloadedGameMode.None;
        level.m_initialSun = 9990;
        launching = true; ClosePicker(); selectedZombie = -1;
        try
        {
            DebugSeedChooser.Begin();
            Mod.Instance.LoggerInstance.Msg($"Debug: Start {background.Name}; native card selection.");
            chooser.m_gameplayService.SetCurrentLevelData(level);
            chooser.ChooseGameplay();
        }
        catch
        {
            Clear(); Open();
            throw;
        }
    }

    internal static void BackgroundReady(Board value)
    {
        if (!Owns(value)) return;
        value.mBackground = backgrounds[backgroundIndex].Type;
        if (value.mBackground is not (BackgroundType.Pool or BackgroundType.Fog)) return;
        // PickBackground builds terrain before this override. A scenic pool
        // must also have native pool rows and squares, not just pool artwork.
        var terrain = value.mGridSquareType;
        if (terrain == null || value.mPlantRow == null) return;
        var array = terrain.Cast<Il2CppSystem.Array>();
        int columns = array.GetLength(0), rows = array.GetLength(1);
        if (columns != 9 || rows != 6 || value.mPlantRow.Length != 6)
            throw new InvalidOperationException("Unexpected native debug terrain dimensions.");
        for (int row = 0; row < rows; ++row)
        {
            bool water = row is 2 or 3;
            value.mPlantRow[row] = water ? PlantRowType.Pool : PlantRowType.Normal;
            for (int col = 0; col < columns; ++col)
                System.Runtime.InteropServices.Marshal.WriteInt32(terrain.Pointer,
                    0x20 + 4 * (col * rows + row), (int)(water ? GridSquareType.Pool : GridSquareType.Grass));
        }
    }

    internal static void Ready(GameplayActivity value)
    {
        if (!Owns(value) || !launching || value.Board == null) return;
        board = value.Board;
        launching = false;
        Mod.Instance.LoggerInstance.Msg("Debug: Board ready; native selection and intro remain active.");
    }

    internal static void Started(GameplayActivity value)
    {
        if (!Owns(value)) return;
        value.Board.ShowShovel = true;
        refreshUi = true;
    }

    private static void DrawDeck()
    {
        ui!.Deck(art!.Roster, art.Counter);
        for (int i = 0; i < zombies.Count; ++i)
        {
            int index = zombies[i];
            Card(new Rect(105 + i * 144, 657, 128, 176), ZombieName(index), () => ZombieArt(index), () =>
            {
                if (board != null && OptionalGameplay.CarriedPlant(board) == null)
                {
                    SelectZombie(index);
                }
            }, selectedZombie == index, badge: () => ZombieHeadArt(index));
        }
        if (!string.IsNullOrEmpty(error)) ui.Label(new Rect(10, 580, 1000, 30), error);
        if (selectedZombie >= 0) ui.Label(new Rect(10, 610, 1000, 30), "Place: " + ZombieName(selectedZombie)
            + " | Right edge: natural lane entry");
    }

    internal static bool Placing => Owns(board) && (selectedZombie >= 0
        || board!.mCursorObject.CursorType == CursorType.PlantFromBank);

    private static bool ZombieCell(Board value, int x, int y, out int col, out int row, out bool entrance)
    {
        int right = value.GridToPixelX(8, 0) + 40;
        entrance = x >= right;
        col = entrance ? 8 : value.PixelToGridX(x, y);
        row = value.PixelToGridY(entrance ? right : x, y);
        return x < 900 && col >= 0 && col < 9 && row >= 0 && row < value.GetNumRows();
    }

    internal static bool MouseDown(Board value, int x, int y, int clicks, int player)
    {
        if (!Owns(value) || value.mPaused || value.mApp.GameScene != GameScenes.Playing) return true;
        // Consume the debug deck screen region before
        // native board tools, the glove or plant clicks can act underneath it.
        var mouse = Mouse.current;
        if (!Placing && mouse != null && ui?.ContainsDeck(mouse.position.ReadValue()) == true) return false;
        if (selectedZombie < 0) return true;
        if (clicks < 0) { selectedZombie = -1; refreshUi = true; return false; }
        if (value.mSeedBank.ContainsPoint(x, y)) return true;
        if (value.mPaused || value.mApp.GameScene != GameScenes.Playing || player != 0) return false;
        if (!ZombieCell(value, x, y, out int col, out int row, out bool entrance)) return false;
        var registered = selectedZombie >= zombieTypes.Length ? registeredZombies[selectedZombie - zombieTypes.Length] : null;
        var type = registered?.NativeType ?? zombieTypes[selectedZombie];
        // DuckyTube is an artwork/catalog entry. Native CheckForPool admits
        // Normal, whose controller selects the tube from its pool row.
        if (type == ZombieType.DuckyTube) type = ZombieType.Normal;
        Zombie? zombie;
        var previousIds = new HashSet<uint>();
        for (int i = 0; i < value.m_zombies.Count; ++i)
            if (value.m_zombies[i] is { } existing) previousIds.Add(existing.DataID);
        bool previous = OptionalGameplay.SuppressReplacement;
        try
        {
            // A Normal card must create the native Normal unit, never win the
            // random eyes replacement intended for ordinary automatic spawns.
            OptionalGameplay.SuppressReplacement = true;
            zombie = registered != null ? registered.Spawn(value, row) : value.AddZombieInRow(type, row, -1, false);
        }
        catch (Exception exception)
        {
            // Native initialization can fail for a mode-specific zombie after
            // allocation. Roll back only this attempt, retaining existing foes.
            for (int i = value.m_zombies.Count - 1; i >= 0; --i)
            {
                var allocated = value.m_zombies[i];
                if (allocated == null || previousIds.Contains(allocated.DataID) || allocated.mDead) continue;
                if (allocated.mController != null) allocated.DieNoLoot();
                else allocated.mDead = true;
            }
            error = $"{type}: native spawning is unavailable in this level.";
            Mod.Instance.LoggerInstance.Warning($"Debug spawn {type}: {exception.Message}");
            selectedZombie = -1; refreshUi = true;
            return false;
        }
        finally { OptionalGameplay.SuppressReplacement = previous; }
        error = "";
        if (zombie == null || zombie.mDead) return false;
        zombie.mMindControlled = false;
        // Preserve the native entrance position and phase at the right edge.
        // Walking across the pool boundary owns splash, wading and drowning.
        if (!entrance)
        {
            zombie.mPosX = value.GridToPixelX(col, row);
            zombie.mPosY = zombie.GetPosYBasedOnRow(row);
        }
        zombie.mX = (int)zombie.mPosX; zombie.mY = (int)zombie.mPosY;
        zombie.CheckForPool();
        zombie.UpdateZombiePosition();
        selectedZombie = -1;
        refreshUi = true;
        return false;
    }

    internal static void Tick(Board value)
    {
        if (!Owns(value) || value.mPaused) return;
        if (!ApiSunOverride) value.mSunMoney[0].Amount = 9990;
        value.mLevelComplete = false;
        for (int i = value.m_zombies.Count - 1; i >= 0; --i)
            if (value.m_zombies[i] is { mDead: false } zombie
                && (zombie.mPosX < -160 || zombie.mMindControlled && zombie.mPosX > 1100)) zombie.DieNoLoot();
    }

    internal static void Disposed(Board value)
    {
        if (!Owns(value)) return;
        if (CreatingBoard) { board = null; launching = true; selectedZombie = -1; }
        else Clear();
    }

    internal static void CancelZombiePlacement() { selectedZombie = -1; apiPreviewPoint = null; refreshUi = true; }

    internal static void PlantSelected(SeedPacket packet)
    {
        if (Owns(packet.mApp?.Board)) { selectedZombie = -1; refreshUi = true; }
    }

    private static void UpdatePreview()
    {
        if (!Owns(board) || board!.mPaused || board.mApp.GameScene != GameScenes.Playing || selectedZombie < 0)
        { preview?.Dispose(); preview = null; return; }
        int x = apiPreviewPoint?.X ?? board.mPrevMouseX[0], y = apiPreviewPoint?.Y ?? board.mPrevMouseY[0];
        bool valid = ZombieCell(board, x, y, out int col, out int row, out bool entrance);
        var mouse = Mouse.current;
        if (!valid || board.mSeedBank.ContainsPoint(x, y) || mouse == null && apiPreviewPoint == null)
        { preview?.Dispose(); preview = null; return; }
        preview ??= new DebugZombiePreview(board);
        // GetPosYBasedOnRow is the native actor's top origin. Static portraits
        // have a foot pivot, so use the bottom of this board's actual row.
        // Row spacing is 100 on five-row lawns and 85 on six-row/roof lawns.
        int rowHeight = board.GridToPixelY(col, 1) - board.GridToPixelY(col, 0);
        preview.Update(ZombiePreviewArt(selectedZombie), board.GridToPixelX(col, row),
            board.GetPosYBasedOnRow(board.GridToPixelX(col, row), row) + rowHeight,
            entrance, board.GridToPixelX(0, row), rowHeight);
    }

    internal static void Clear()
    {
        DebugTools.Reset();
        ApiSunOverride = false;
        preview?.Dispose(); preview = null;
        DebugBankPreviews.Clear();
        DebugSeedChooser.Clear();
        if (chooser != null && chooser.m_gameplayService != null && level != null
            && chooser.m_gameplayService.CurrentLevelData?.Pointer == level.Pointer)
        {
            if (previousLevel != null) chooser.m_gameplayService.SetCurrentLevelData(previousLevel);
            else chooser.m_gameplayService.ClearCurrentLevelData();
        }
        if (level != null) Object.Destroy(level);
        ClosePicker();
        level = previousLevel = null; board = null;
        launching = picking = false; selectedZombie = -1; apiPreviewPoint = null; word = 0;
        zombies.Clear(); registeredZombies = Array.Empty<ZombieCard>();
        DebugAlmanac.Update();
        art?.Dispose(); art = null;
    }
}

[HarmonyPatch(typeof(GameplayActivity), nameof(GameplayActivity.NewGame))]
internal static class DebugReadyPatch
{
    private static void Prefix(GameplayActivity __instance, out bool __state)
    { __state = DebugMode.CreatingBoard; DebugMode.CreatingBoard = DebugMode.Owns(__instance); }
    private static void Postfix(GameplayActivity __instance) => DebugMode.Ready(__instance);
    private static void Finalizer(bool __state) => DebugMode.CreatingBoard = __state;
}
[HarmonyPatch(typeof(Board), nameof(Board.PickBackground))]
internal static class DebugBackgroundPatch
{
    private static void Postfix(Board __instance) => DebugMode.BackgroundReady(__instance);
}
[HarmonyPatch(typeof(Board), nameof(Board.DrawFog))]
internal static class DebugNoFogPatch
{
    private static bool Prefix(Board __instance)
    {
        if (!DebugMode.NoFog(__instance)) return true;
        // Use the native non-fog rendering path. Skipping DrawFog alone would
        // retain the controller's previous alpha; a Blover timer only fades it.
        __instance.mApp?.BackgroundController?.FogController?.ClearAlpha();
        return false;
    }
}
[HarmonyPatch(typeof(Board), nameof(Board.Update))]
internal static class DebugTickPatch
{
    private static void Prefix(Board __instance) => DebugMode.Tick(__instance);
}
[HarmonyPatch]
internal static class DebugAutomaticSpawnsPatch
{
    // Stop the schedulers rather than returning null from zombie factories:
    // native callers expect a valid zombie. Explicit debug cards keep using
    // AddZombieInRow and receive fully initialized native instances.
    private static IEnumerable<MethodBase> TargetMethods() => new[]
        { "UpdateSunSpawning", "UpdateZombieSpawning", "SpawnZombieWave", "SpawnZombiesFromGraves", "SpawnZombiesFromPool", "SpawnZombiesFromSky" }
        .Select(name => AccessTools.Method(typeof(Board), name));
    private static bool Prefix(Board __instance) => !DebugMode.Owns(__instance);
}
[HarmonyPatch(typeof(Board), nameof(Board.MouseDown))]
internal static class DebugPlacementPatch
{
    [HarmonyPriority(Priority.First + 1000)]
    private static bool Prefix(Board __instance, int x, int y, int theClickCount, int playerIndex) =>
        DebugMode.MouseDown(__instance, x, y, theClickCount, playerIndex);
}
[HarmonyPatch(typeof(SeedPacket), nameof(SeedPacket.Activated))]
internal static class DebugPlantCardPatch
{
    private static void Prefix(SeedPacket __instance) => DebugMode.PlantSelected(__instance);
}
[HarmonyPatch(typeof(Board), nameof(Board.DisposeBoard))]
internal static class DebugDisposePatch
{
    private static void Postfix(Board __instance) => DebugMode.Disposed(__instance);
}
[HarmonyPatch(typeof(Board), nameof(Board.ZombiesWon))]
internal static class DebugLossPatch
{
    private static bool Prefix(Board __instance, Zombie aZombie)
    {
        if (!DebugMode.Owns(__instance)) return true;
        aZombie?.DieNoLoot();
        return false;
    }
}
[HarmonyPatch]
internal static class DebugBoardPersistencePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { "TryToSaveGame", "UpdateLevelEndSequence", "SurvivalSaveScore" }
        .Select(name => AccessTools.Method(typeof(Board), name));
    private static bool Prefix(Board __instance) => !DebugMode.Owns(__instance);
}
[HarmonyPatch]
internal static class DebugAppPersistencePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { "SaveGame", "CheckForGameEnd", "UpdatePlayerProfileForFinishingLevel" }
        .Select(name => AccessTools.Method(typeof(GameplayActivity), name));
    private static bool Prefix(GameplayActivity __instance) => !DebugMode.Owns(__instance);
}
[HarmonyPatch(typeof(GameplayService), nameof(GameplayService.HasSeedType))]
internal static class DebugUnlockPatch
{
    private static void Postfix(SeedType seedType, ref bool __result)
    {
        if (DebugMode.Active && seedType >= 0 && seedType < SeedType.NumSeedTypes) __result = true;
    }
}
[HarmonyPatch(typeof(Board), nameof(Board.CanTakeSunMoney))]
internal static class DebugSunCheckPatch
{
    private static bool Prefix(Board __instance, ref bool __result)
    { if (!DebugMode.Owns(__instance)) return true; __result = true; return false; }
}
[HarmonyPatch(typeof(Board), nameof(Board.TakeSunMoney))]
internal static class DebugSunSpendPatch
{
    private static bool Prefix(Board __instance, ref bool __result)
    { if (!DebugMode.Owns(__instance)) return true; __result = true; return false; }
}

// NewGame consults serialization through its interface before the board exists.
// Protect that boundary too: debug must neither load nor replace a survival save.
[HarmonyPatch]
internal static class DebugSerializationWritePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { "Serialize", "ClearCurrent", "Clear", "DeleteSaveFile" }
        .Select(name => AccessTools.Method(typeof(SerializationService), name));
    private static bool Prefix() => !DebugMode.Active;
}
[HarmonyPatch]
internal static class DebugSerializationReadPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { "Deserialize", "get_SaveExists", "LevelDataHasSave", "LevelWillOverwrite" }
        .Select(name => AccessTools.Method(typeof(SerializationService), name));
    private static bool Prefix(ref bool __result)
    { if (!DebugMode.Active) return true; __result = false; return false; }
}

[HarmonyPatch(typeof(GameplayActivity), nameof(GameplayActivity.StartPlaying))]
internal static class DebugStartedPatch
{
    private static void Postfix(GameplayActivity __instance) => DebugMode.Started(__instance);
}
[HarmonyPatch(typeof(Board), nameof(Board.GetNumSeedsInBank))]
internal static class DebugBankCapacityPatch
{
    private static bool Prefix(Board __instance, ref int __result)
    { if (!DebugMode.Owns(__instance)) return true; __result = DebugSeedChooser.BankCapacity; return false; }
}
[HarmonyPatch(typeof(GameplayActivity), nameof(GameplayActivity.GetSeedsAvailable))]
internal static class DebugAvailablePlantsPatch
{
    private static bool Prefix(GameplayActivity __instance, ref int __result)
    { if (!DebugMode.Owns(__instance)) return true; __result = (int)SeedType.NumSeedsInChooser; return false; }
}
[HarmonyPatch(typeof(GameplayActivity), nameof(GameplayActivity.HasSeedType))]
internal static class DebugAppUnlockPatch
{
    private static void Postfix(GameplayActivity __instance, SeedType seedType, ref bool __result)
    {
        if (DebugMode.Owns(__instance) && seedType >= 0 && seedType < SeedType.NumSeedTypes) __result = true;
    }
}
[HarmonyPatch(typeof(Zombie), nameof(Zombie.DropLoot))]
internal static class DebugZombieLootPatch
{
    private static bool Prefix(Zombie __instance)
    {
        if (!DebugMode.Owns(__instance.mBoard)) return true;
        __instance.mDroppedLoot = true;
        return false;
    }
}

[HarmonyPatch]
internal static class DebugPlantCooldownPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new[]
    {
        AccessTools.Method(typeof(SeedPacket), nameof(SeedPacket.WasPlanted)),
        AccessTools.Method(typeof(SeedPacket), nameof(SeedPacket.Update)),
        AccessTools.Method(typeof(SeedPacket), nameof(SeedPacket.SetPacketType))
    };

    // Preserve native planting, consumption and fusion callbacks. Only the
    // recharge state belongs to the debug rule, including initial recharges.
    private static void Postfix(SeedPacket __instance)
    {
        if (!DebugMode.Owns(__instance.mBoard) || __instance.mApp.GameScene != GameScenes.Playing
            || __instance.mPacketType == SeedType.None) return;
        __instance.mRefreshCounter = 0;
        __instance.mRefreshing = false;
        __instance.mActive = true;
    }
}


