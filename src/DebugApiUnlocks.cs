using System.Text.Json;
using HarmonyLib;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using Il2CppReloaded.Services;

namespace DebugMod;

// Session overrides do not forge adventure progress, purchases or achievements.
// Explicit false overrides also take precedence over the debug level's reveal.
internal static class ApiUnlocks
{
    internal static readonly Dictionary<SeedType, bool> Plants = new();
    internal static readonly Dictionary<(string Kind, int Type), bool> Entries = new();
    internal static object Plant(JsonElement args)
    {
        if (args.TryGetProperty("owner", out _))
        {
            string owner = DebugCommands.Str(args, "owner"), id = DebugCommands.Str(args, "id");
            bool value = args.GetProperty("unlocked").GetBoolean();
            ApiLibraries.SetPlant(owner, id, value, false);
            return new { owner, id, unlocked = value, scope = "session" };
        }
        var type = DebugCommands.EnumValue<SeedType>(args, "type");
        if (type < 0 || type >= SeedType.NumSeedTypes) throw new ApiError("INVALID_PLANT", type.ToString());
        bool unlocked = args.GetProperty("unlocked").GetBoolean();
        Plants[type] = unlocked;
        return new { type = type.ToString(), unlocked, scope = "session" };
    }
    internal static object Almanac(JsonElement args)
    {
        string kind = DebugCommands.Str(args, "kind");
        if (args.TryGetProperty("owner", out _))
        {
            string owner = DebugCommands.Str(args, "owner"), id = DebugCommands.Str(args, "id");
            bool value = args.GetProperty("unlocked").GetBoolean();
            if (kind == "plant") ApiLibraries.SetPlant(owner, id, value, true);
            else if (kind == "zombie") ApiLibraries.SetZombie(owner, id, DebugCommands.Bool(args, "duck"), value);
            else throw new ApiError("INVALID_ARGUMENT", "kind must be plant or zombie.");
            return new { owner, id, unlocked = value, scope = "session" };
        }
        int type;
        if (kind == "plant")
        {
            var seed = DebugCommands.EnumValue<SeedType>(args, "type");
            if (seed < 0 || seed >= SeedType.NumSeedTypes) throw new ApiError("INVALID_PLANT", seed.ToString());
            type = (int)seed;
        }
        else if (kind == "zombie")
        {
            var zombie = DebugCommands.EnumValue<ZombieType>(args, "type");
            if (zombie < ZombieType.Normal || zombie >= ZombieType.NumZombieTypes) throw new ApiError("INVALID_ZOMBIE", zombie.ToString());
            type = (int)zombie;
        }
        else throw new ApiError("INVALID_ARGUMENT", "kind must be plant or zombie.");
        bool unlocked = args.GetProperty("unlocked").GetBoolean();
        Entries[(kind, type)] = unlocked;
        Refresh();
        return new { kind, type, unlocked, scope = "session" };
    }
    internal static void Reset() { Plants.Clear(); Entries.Clear(); ApiControls.Modes.Clear(); ApiLibraries.Reset(); Refresh(); }
    private static void Refresh() => DebugAlmanac.RefreshCurrent();
    internal static bool? Entry(AlmanacEntryData entry)
    {
        string kind = entry.EntryType.ToString().Contains("Plant", StringComparison.OrdinalIgnoreCase) ? "plant" : "zombie";
        return Entries.TryGetValue((kind, kind == "plant" ? (int)entry.SeedType : (int)entry.ZombieType), out bool unlocked) ? unlocked : null;
    }
    internal static object List(JsonElement args)
    {
        var data = DebugCommands.Data(); var user = DebugCommands.User();
        var result = new List<object>();
        bool unlockedOnly = DebugCommands.Bool(args, "unlockedOnly");
        void Add(AlmanacEntryData entry)
        {
            bool unlocked = !user.IsLocked(entry);
            if (!unlockedOnly || unlocked) result.Add(new { entry.EntryName, kind = entry.EntryType.ToString(),
                plant = entry.SeedType.ToString(), zombie = entry.ZombieType.ToString(), unlocked,
                entry.EntryDescription, entry.EntrySunCost, entry.EntryRecharge });
        }
        foreach (var list in new[] { data.PlantAlmanacData, data.ZombieAlmanacData })
        {
            int count = list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<AlmanacEntryData>>().Count;
            for (int i = 0; i < count; ++i) Add(list[i]);
        }
        return new { native = result, plants = ApiLibraries.Plants(unlockedOnly), zombies = ApiLibraries.Zombies(unlockedOnly) };
    }
}

[HarmonyPatch(typeof(GameplayService), nameof(GameplayService.HasSeedType))]
internal static class ApiGameplayPlantUnlockPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(SeedType __0, ref bool __result)
    { if (ApiUnlocks.Plants.TryGetValue(__0, out bool unlocked)) __result = unlocked; }
}
[HarmonyPatch(typeof(UserService), nameof(UserService.HasSeedType))]
internal static class ApiUserPlantUnlockPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(SeedType __0, ref bool __result)
    { if (ApiUnlocks.Plants.TryGetValue(__0, out bool unlocked)) __result = unlocked; }
}
[HarmonyPatch(typeof(UserService), nameof(UserService.IsLocked), new[] { typeof(SeedType) })]
internal static class ApiPlantLockPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(SeedType __0, ref bool __result)
    { if (ApiUnlocks.Plants.TryGetValue(__0, out bool unlocked)) __result = !unlocked; }
}
[HarmonyPatch(typeof(UserService), nameof(UserService.IsLocked), new[] { typeof(AlmanacEntryData) })]
internal static class ApiAlmanacLockPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(AlmanacEntryData __0, ref bool __result)
    { if (ApiUnlocks.Entry(__0) is { } unlocked) __result = !unlocked; }
}
