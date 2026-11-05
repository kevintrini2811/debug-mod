using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using Il2CppReloaded.Services;
using ReplantedModUi;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DebugMod;

// Native services remain responsible for profile persistence and device routing.
internal static class ApiControls
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    internal static readonly string[] Commands = { "game.shutdown", "difficulty", "goldenbean.apply", "unlocks", "unlock.modes", "achievements", "profiles", "profile.create", "profile.delete", "profile.rename", "profile.switch", "gravestone.place", "suncollector", "mowers", "mower.add", "mower.remove", "mower.set", "controller", "controller.reset", "controller.status" };
    internal static readonly Dictionary<string, bool> Modes = new();
    [ThreadStatic] internal static LawnMowerType? CreatingMowerType;
    private static Gamepad? controller;
    private static float releaseAt;
    private static GamepadState heldState;
    private static bool holding;
    private static int quitFrame = -1;
    private static readonly Action? detachQuitTargets = OptionalModApi.Bind<Action>("FusionMod", "FusionMod.FusionMod", "DetachCameraTargets");
    private static readonly Func<Board, Plant, bool>? bean = OptionalModApi.Bind<Func<Board, Plant, bool>>("FusionMod", "FusionMod.GoldenBean", "Use");
    internal static bool Handles(string command) => Commands.Contains(command);
    internal static object Execute(string command, JsonElement args)
    {
        switch (command)
        {
            case "game.shutdown": quitFrame = Time.frameCount + 3; return new { accepted = true };
            case "difficulty": return Difficulty(args);
            case "suncollector": return SunCollector(args);
            case "controller": return Controller(args);
            case "controller.status": return ControllerStatus();
            case "controller.reset": ResetController(); return new { released = true };
            case "profiles": return Profiles();
            case "profile.create": case "profile.rename": case "profile.delete": case "profile.switch": return Profile(command, args);
            case "achievements":
                var profile = DebugCommands.User().ActiveUserProfile ?? throw new ApiError("GAME_NOT_READY", "No active profile.");
                return Enum.GetValues<AchievementType>().Where(t => (int)t >= 0 && (int)t < profile.mBEarnedAchievements.Length)
                    .Select(t => new { type = t.ToString(), value = (int)t, earned = profile.mBEarnedAchievements[(int)t], shown = (int)t < profile.mBShownAchievements.Length && profile.mBShownAchievements[(int)t] }).ToArray();
            case "unlock.modes":
                string mode = DebugCommands.Str(args, "mode").ToLowerInvariant();
                if (mode is not ("minigames" or "puzzle" or "survival" or "all")) throw new ApiError("INVALID_ARGUMENT", "mode: minigames, puzzle, survival or all.");
                bool unlocked = args.GetProperty("unlocked").GetBoolean();
                foreach (string name in mode == "all" ? new[] { "minigames", "puzzle", "survival" } : new[] { mode }) Modes[name] = unlocked;
                return new { scope = "session", modes = ModeStatus() };
            case "unlocks":
                var user = DebugCommands.User(); var data = DebugCommands.Data();
                int count = data.AllLevelsData.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<LevelEntryData>>().Count;
                return new { modes = ModeStatus(), almanac = ApiUnlocks.List(args),
                    seeds = Enum.GetValues<SeedType>().Where(t => t >= 0 && t < SeedType.NumSeedTypes).Select(t => new { type = t.ToString(), unlocked = user.HasSeedType(t) }).ToArray(),
                    objects = Enum.GetValues<ReloadedObjectType>().Select(t => new { type = t.ToString(), unlocked = !user.IsLocked(t) }).ToArray(),
                    levels = Enumerable.Range(0, count).Select(i => new { index = i, name = data.AllLevelsData[i].FullLevelName, unlocked = !user.IsLocked(data.AllLevelsData[i], user.GetRipMode()) }).ToArray() };
        }
        var board = ApiBoard.Require();
        if (command == "mowers") return Mowers(board);
        if (DebugCommands.Str(args, "board") != ApiBoard.Token) throw new ApiError("STALE_BOARD", "Use the current board token.");
        switch (command)
        {
            case "goldenbean.apply":
                if (bean == null) throw new ApiError("FUSION_API_UNAVAILABLE", "Golden coffee bean requires Plant Fusion.");
                uint id = args.GetProperty("id").GetUInt32();
                Plant? plant = null;
                for (int i = 0; i < board.m_plants.Count; ++i) if (board.m_plants[i] is { mDead: false } p && p.DataID == id) { plant = p; break; }
                if (plant == null) throw new ApiError("ENTITY_NOT_FOUND", "No active plant with this ID.");
                return new { applied = bean(board, plant) };
            case "gravestone.place":
                int col = DebugCommands.Int(args, "col", 0, 8), row = DebugCommands.Int(args, "row", 0, board.GetNumRows() - 1);
                if (!board.CanAddGraveStoneAt(col, row)) throw new ApiError("INVALID_PLACEMENT", "Native gravestone placement rejected this tile.");
                var grave = board.AddAGraveStone(col, row) ?? throw new ApiError("ALLOCATION_FAILED", "Could not allocate gravestone.");
                return DebugCommands.Snapshot(grave);
            case "mowers": return Mowers(board);
            case "mower.add": case "mower.remove": case "mower.set": return Mower(command, board, args);
        }
        throw new ApiError("UNKNOWN_COMMAND", command);
    }
    private static object ModeStatus()
    {
        var u = DebugCommands.User();
        return new { minigames = u.IsMinigameModeAvailable(), puzzle = u.IsPuzzleModeAvailable(), survival = u.IsSurvivalModeAvailable(),
            coop = u.IsCoopModeAvailable(), almanac = u.IsAlmanacAvailable(), store = u.IsStoreAvailable(), zenGarden = u.IsZenGardenAvailable(), cloudyDay = u.IsCloudyDayAvailable(), limbo = u.IsLimboContentAvailable() };
    }
    internal static string? LevelMode(LevelEntryData level)
    {
        string name = level.GameMode.ToString();
        return name.StartsWith("Survival", StringComparison.Ordinal) ? "survival" : name.StartsWith("Puzzle", StringComparison.Ordinal) ? "puzzle"
            : name.StartsWith("Challenge", StringComparison.Ordinal) && level.GameMode != GameMode.ChallengeZenGarden ? "minigames" : null;
    }
    private static object Profiles()
    {
        var u = DebugCommands.User();
        return new { active = u.ActiveUserIndex, items = Enumerable.Range(0, u.NumUserProfiles).Select(i => new { index = i, guid = u.UserProfiles[i].mGuid, name = u.UserProfiles[i].mName, id = u.UserProfiles[i].mId }).ToArray() };
    }
    private static object Profile(string command, JsonElement args)
    {
        if (DebugMode.Active || ApiBoard.Current != null || !DebugMode.ApiMenuReady) throw new ApiError("MAIN_MENU_REQUIRED", "Profile changes require the main menu without a debug session.");
        var u = DebugCommands.User();
        string? name = command is "profile.create" or "profile.rename" ? DebugCommands.Str(args, "name").Trim() : null;
        if (name != null && (name.Length is < 1 or > 32 || name.Any(char.IsControl) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || u.HasProfileNamed(name)))
            throw new ApiError("INVALID_ARGUMENT", "Expected a unique profile name, 1..32 characters without control/path characters.");
        if (command == "profile.create")
        {
            if (!u.CreateProfile(name!)) throw new ApiError("PROFILE_FAILED", "Native profile creation failed.");
            ApiUnlocks.Reset(); return Profiles();
        }
        string guid = DebugCommands.Str(args, "guid");
        int index = Enumerable.Range(0, u.NumUserProfiles).FirstOrDefault(i => u.UserProfiles[i].mGuid == guid, -1);
        if (index < 0) throw new ApiError("PROFILE_NOT_FOUND", "Unknown profile GUID.");
        if (command == "profile.switch")
        {
            if (!u.SetActiveProfile(index)) throw new ApiError("PROFILE_FAILED", "Native profile switch failed.");
            ApiUnlocks.Reset(); return Profiles();
        }
        if (command == "profile.delete" && u.NumUserProfiles <= 1) throw new ApiError("LAST_PROFILE", "Keep at least one profile.");
        // DeleteProfile and SetName act on the active profile. Restore previous selection.
        string previous = u.ActiveUserProfile.mGuid;
        if (!u.SetActiveProfile(index)) throw new ApiError("PROFILE_FAILED", "Native profile switch failed.");
        try
        {
            if (command == "profile.rename") { u.SetName(name!); if (!u.SaveDetails()) throw new ApiError("SAVE_FAILED", "Profile renamed in memory; native save failed."); }
            else
            {
                var selected = u.ActiveUserProfile;
                string backup = Path.Combine(DebugCommandApi.Root, "profile-backups", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "-" + selected.mGuid);
                Directory.CreateDirectory(backup);
                foreach (string file in new[] { selected.GetFilename(), selected.GetJSonProfileFilename() })
                    if (!string.IsNullOrEmpty(file) && File.Exists(file)) File.Copy(file, Path.Combine(backup, Path.GetFileName(file)), false);
                File.WriteAllText(Path.Combine(backup, "profile.json"), JsonSerializer.Serialize(new { selected.mGuid, selected.mName, selected.mId }));
                u.DeleteProfile();
            }
        }
        finally
        {
            for (int i = 0; i < u.NumUserProfiles; ++i) if (u.UserProfiles[i].mGuid == previous) { u.SetActiveProfile(i); break; }
            ApiUnlocks.Reset();
        }
        return Profiles();
    }
    private static object Difficulty(JsonElement args)
    {
        var type = OptionalModApi.Type("DifficultyMod", "DifficultyMod.Mod") ?? throw new ApiError("MOD_NOT_INSTALLED", "Difficulty Mod is not installed.");
        var mod = type.GetField("Instance", Flags)!.GetValue(null)!;
        var board = ApiBoard.Require();
        if (type.GetMethod("Owns", Flags, null, new[] { typeof(Board) }, null)!.Invoke(mod, new object[] { board }) is not true)
            throw new ApiError("LEVEL_NOT_READY", "Difficulty Mod does not own this board.");
        var active = type.GetField("Active", Flags)!;
        if (args.TryGetProperty("value", out _))
        {
            if (DebugCommands.Str(args, "board") != ApiBoard.Token) throw new ApiError("STALE_BOARD", "Use the current board token.");
            int requested = DebugCommands.Int(args, "value", 1, 5);
            var setter = type.GetMethod("SetApiDifficulty", Flags);
            if (setter != null) setter.Invoke(null, new object[] { board, requested });
            else
            {
                if (!DebugMode.Owns(board)) throw new ApiError("MOD_API_UNAVAILABLE", "Normal levels require Difficulty Mod 1.0.6 or newer.");
                active.SetValue(mod, Enum.ToObject(active.FieldType, requested));
            }
        }
        var value = active.GetValue(mod)!;
        return new { value = Convert.ToInt32(value), name = value.ToString(), scope = "current level", trophyEligible = type.GetField("ApiAdjustedRun", Flags)?.GetValue(mod) is not true };
    }
    private static object SunCollector(JsonElement args)
    {
        var type = OptionalModApi.Type("SunCollector", "SunCollector.SunCollectorMod") ?? throw new ApiError("MOD_NOT_INSTALLED", "Sun Collector is not installed.");
        var mod = type.GetField("Instance", Flags)!.GetValue(null)!;
        var entry = type.GetField("enabled", Flags)!.GetValue(mod)!;
        var value = entry.GetType().GetProperty("Value")!;
        if (args.TryGetProperty("enabled", out var enabled))
        {
            value.SetValue(entry, enabled.GetBoolean());
            type.GetField("collectionFailed", Flags)!.SetValue(mod, false);
        }
        return new { enabled = (bool)value.GetValue(entry)!, scope = "session" };
    }
    private static object Mowers(Board board)
    {
        var items = new List<object>();
        for (int i = 0; i < board.m_lawnMowers.Count; ++i) if (board.m_lawnMowers[i] is { mDead: false } m) items.Add(DebugCommands.Snapshot(m));
        return new { board = ApiBoard.Token, items };
    }
    private static object Mower(string command, Board board, JsonElement args)
    {
        bool all = DebugCommands.Bool(args, "all");
        uint? id = args.TryGetProperty("id", out var item) ? item.GetUInt32() : null;
        int? row = args.TryGetProperty("row", out _) ? DebugCommands.Int(args, "row", 0, board.GetNumRows() - 1) : null;
        if (!all && id == null && row == null) throw new ApiError("INVALID_ARGUMENT", "Specify id, row or all:true.");
        LawnMowerType? type = args.TryGetProperty("type", out _) ? DebugCommands.EnumValue<LawnMowerType>(args, "type") : null;
        if (type == LawnMowerType.NumMowerTypes) throw new ApiError("INVALID_ARGUMENT", "Invalid mower type.");
        var targets = new List<LawnMower>();
        for (int i = 0; i < board.m_lawnMowers.Count; ++i)
            if (board.m_lawnMowers[i] is { mDead: false } m && (all || (id.HasValue ? (uint)m.DataID == id : m.mRow == row))) targets.Add(m);
        if (command == "mower.add")
        {
            if (id.HasValue || !all && !row.HasValue) throw new ApiError("INVALID_ARGUMENT", "Add requires row or all:true, without id.");
            var created = new List<LawnMower>();
            try
            {
                foreach (int r in all ? Enumerable.Range(0, board.GetNumRows()) : new[] { row!.Value })
                {
                    if (board.FindLawnMowerInRow(r) is { mDead: false }) continue;
                    var mower = NewMower(board, r, type);
                    created.Add(mower);
                }
            }
            catch { foreach (var m in created) m.Die(); throw; }
        }
        else
        {
            if (!all && targets.Count == 0) throw new ApiError("ENTITY_NOT_FOUND", "No active mower matches.");
            if (command == "mower.set" && type == null) throw new ApiError("INVALID_ARGUMENT", "Expected mower type.");
            if (command == "mower.remove") foreach (var m in targets) m.Die();
            else
            {
                var replacements = new List<(LawnMower Old, LawnMower New)>();
                try
                {
                    foreach (var old in targets)
                    {
                        if (old.mMowerType == type!.Value) continue;
                        var replacement = NewMower(board, old.mRow, type);
                        replacements.Add((old, replacement));
                        replacement.mX = old.mX; replacement.mY = old.mY;
                        replacement.mMowerState = old.mMowerState; replacement.mRollingInCounter = old.mRollingInCounter;
                        replacement.mChompCounter = old.mChompCounter; replacement.mSquishedCounter = old.mSquishedCounter;
                        replacement.mAltitude = old.mAltitude; replacement.mMowerHeight = old.mMowerHeight;
                        replacement.mLastPortalX = old.mLastPortalX;
                        replacement.mController?.ForceUpdatePosition();
                    }
                }
                catch { foreach (var pair in replacements) pair.New.Die(); throw; }
                foreach (var pair in replacements) pair.Old.Die();
            }
        }
        return Mowers(board);
    }
    private static LawnMower NewMower(Board board, int row, LawnMowerType? type)
    {
        var mower = board.m_lawnMowers.DataArrayAlloc() ?? throw new ApiError("ALLOCATION_FAILED", "Could not allocate mower.");
        var previous = CreatingMowerType;
        try
        {
            CreatingMowerType = type;
            mower.LawnMowerInitialize(row, board.mApp);
            if (type.HasValue)
            {
                mower.EnableSuperMower(type == LawnMowerType.SuperMower);
                mower.mMowerType = type.Value;
            }
            return mower;
        }
        catch { mower.Die(); throw; }
        finally { CreatingMowerType = previous; }
    }
    private static object Controller(JsonElement args)
    {
        var state = new GamepadState();
        var names = new Dictionary<string, GamepadButton>(StringComparer.OrdinalIgnoreCase) {
            ["A"] = GamepadButton.South, ["B"] = GamepadButton.East, ["X"] = GamepadButton.West, ["Y"] = GamepadButton.North,
            ["LB"] = GamepadButton.LeftShoulder, ["RB"] = GamepadButton.RightShoulder, ["Back"] = GamepadButton.Select,
            ["Start"] = GamepadButton.Start, ["LS"] = GamepadButton.LeftStick, ["RS"] = GamepadButton.RightStick,
            ["DPadUp"] = GamepadButton.DpadUp, ["DPadDown"] = GamepadButton.DpadDown, ["DPadLeft"] = GamepadButton.DpadLeft, ["DPadRight"] = GamepadButton.DpadRight };
        if (args.TryGetProperty("buttons", out var buttons)) foreach (var button in buttons.EnumerateArray())
        {
            string name = button.GetString() ?? "";
            if (name.Equals("LT", StringComparison.OrdinalIgnoreCase)) state.leftTrigger = 1;
            else if (name.Equals("RT", StringComparison.OrdinalIgnoreCase)) state.rightTrigger = 1;
            else if (name.Equals("Guide", StringComparison.OrdinalIgnoreCase)) state.buttons |= 1u << 14;
            else if (names.TryGetValue(name, out var b)) state = state.WithButton(b);
            else throw new ApiError("INVALID_ARGUMENT", $"Unknown Xbox 360 button: {name}.");
        }
        float Number(JsonElement input, string key, float min, float max, float fallback = 0)
        { if (!input.TryGetProperty(key, out var p)) return fallback; if (!p.TryGetSingle(out float n) || !float.IsFinite(n) || n < min || n > max) throw new ApiError("INVALID_ARGUMENT", $"{key}: expected {min}..{max}."); return n; }
        Vector2 Stick(string key) => args.TryGetProperty(key, out var s) ? new(Number(s, "x", -1, 1), Number(s, "y", -1, 1)) : Vector2.zero;
        state.leftStick = Stick("leftStick"); state.rightStick = Stick("rightStick");
        state.leftTrigger = Number(args, "leftTrigger", 0, 1, state.leftTrigger); state.rightTrigger = Number(args, "rightTrigger", 0, 1, state.rightTrigger);
        int duration = args.TryGetProperty("milliseconds", out _) ? DebugCommands.Int(args, "milliseconds", 16, 10000) : 100;
        if (controller == null)
        {
            InputSystem.RegisterLayout("{\"name\":\"DebugApiXbox360\",\"extend\":\"Gamepad\",\"canRunInBackground\":true,\"controls\":[{\"name\":\"guide\",\"layout\":\"Button\",\"offset\":0,\"bit\":14,\"format\":\"BIT\"}]}", "DebugApiXbox360", new Il2CppSystem.Nullable<UnityEngine.InputSystem.Layouts.InputDeviceMatcher>());
            controller = InputSystem.AddDevice("DebugApiXbox360", "DebugApiXbox360").Cast<Gamepad>();
        }
        InputSystem.EnableDevice(controller);
        InputSystem.QueueStateEvent(controller, state);
        heldState = state; holding = true;
        releaseAt = Time.unscaledTime + duration / 1000f;
        return new { accepted = true, milliseconds = duration, device = controller.deviceId };
    }
    private static object ControllerStatus()
    {
        if (controller == null) return new { connected = false };
        var buttons = new Dictionary<string, bool> {
            ["A"] = controller.buttonSouth.isPressed, ["B"] = controller.buttonEast.isPressed,
            ["X"] = controller.buttonWest.isPressed, ["Y"] = controller.buttonNorth.isPressed,
            ["LB"] = controller.leftShoulder.isPressed, ["RB"] = controller.rightShoulder.isPressed,
            ["LT"] = controller.leftTrigger.isPressed, ["RT"] = controller.rightTrigger.isPressed,
            ["Back"] = controller.selectButton.isPressed, ["Start"] = controller.startButton.isPressed,
            ["LS"] = controller.leftStickButton.isPressed, ["RS"] = controller.rightStickButton.isPressed,
            ["DPadUp"] = controller.dpad.up.isPressed, ["DPadDown"] = controller.dpad.down.isPressed,
            ["DPadLeft"] = controller.dpad.left.isPressed, ["DPadRight"] = controller.dpad.right.isPressed,
            ["Guide"] = controller["guide"].Cast<UnityEngine.InputSystem.Controls.ButtonControl>().isPressed };
        var left = controller.leftStick.ReadValue(); var right = controller.rightStick.ReadValue();
        return new { connected = true, device = controller.deviceId, enabled = controller.enabled, canRunInBackground = controller.canRunInBackground, applicationFocused = Application.isFocused, buttons,
            leftStick = new { left.x, left.y }, rightStick = new { right.x, right.y },
            leftTrigger = controller.leftTrigger.ReadValue(), rightTrigger = controller.rightTrigger.ReadValue() };
    }
    internal static void Update()
    {
        if (quitFrame >= 0 && Time.frameCount >= quitFrame)
        {
            quitFrame = -1;
            ResetController();
            // Native quit handlers can release preview textures before Unity
            // invokes Application.quitting. Reuse Fusion's existing owner first.
            detachQuitTargets?.Invoke();
            Application.Quit();
        }
        if (controller != null && holding)
        {
            // A native device re-pair can reset its state. Keep the requested
            // hardware state until release, as a held physical controller does.
            if (Time.unscaledTime < releaseAt) InputSystem.QueueStateEvent(controller, heldState);
            else { InputSystem.QueueStateEvent(controller, new GamepadState()); holding = false; releaseAt = 0; }
        }
    }
    private static void ResetController()
    {
        if (controller != null) { InputSystem.ResetDevice(controller); InputSystem.RemoveDevice(controller); controller = null; }
        releaseAt = 0; holding = false;
    }
    internal static void Clear() { ResetController(); Modes.Clear(); quitFrame = -1; }
}

[HarmonyPatch]
internal static class ApiModeAvailabilityPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { "IsMinigameModeAvailable", "IsPuzzleModeAvailable", "IsSurvivalModeAvailable" }.Select(n => AccessTools.Method(typeof(UserService), n));
    private static void Postfix(MethodBase __originalMethod, ref bool __result)
    {
        string key = __originalMethod.Name == "IsMinigameModeAvailable" ? "minigames" : __originalMethod.Name == "IsPuzzleModeAvailable" ? "puzzle" : "survival";
        if (ApiControls.Modes.TryGetValue(key, out bool unlocked)) __result = unlocked;
    }
}
[HarmonyPatch(typeof(UserService), nameof(UserService.IsLocked), new[] { typeof(LevelEntryData), typeof(bool) })]
internal static class ApiModeLevelsPatch
{
    private static void Postfix(LevelEntryData __0, ref bool __result)
    { string? key = ApiControls.LevelMode(__0); if (key != null && ApiControls.Modes.TryGetValue(key, out bool unlocked)) __result = !unlocked; }
}

[HarmonyPatch(typeof(DataService), nameof(DataService.GetLawnMowerDefinition))]
internal static class ApiMowerDefinitionPatch
{
    private static void Prefix(ref LawnMowerType __0)
    { if (ApiControls.CreatingMowerType is { } type) __0 = type; }
}
