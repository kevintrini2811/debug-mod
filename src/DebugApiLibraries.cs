using Il2CppReloaded.Gameplay;
using ReplantedModUi;

namespace DebugMod;

// Libraries own their registries, availability and UI refresh. DebugMod binds
// only standard delegates and native types; older/missing libraries stay usable.
internal static class ApiLibraries
{
    private static T? Bind<T>(string assembly, string method) where T : Delegate
    {
        string type = assembly + ".DebugCatalog";
        if (OptionalModApi.Type(assembly, type)?.GetMethod(method, OptionalModApi.Flags) == null) return null;
        return OptionalModApi.Bind<T>(assembly, type, method);
    }
    private static readonly Func<Board?, IEnumerable<(string Owner, string Id, string Name, string Info, SeedType NativeType, SeedType Marker, bool AlmanacUnlocked, bool? PlantUnlocked)>>? plants =
        Bind<Func<Board?, IEnumerable<(string, string, string, string, SeedType, SeedType, bool, bool?)>>>("PlantLibrary", "AlmanacEntries");
    private static readonly Func<IEnumerable<(string Owner, string Id, string Name, string Info, ZombieType NativeType, bool Duck, bool Unlocked)>>? zombies =
        Bind<Func<IEnumerable<(string, string, string, string, ZombieType, bool, bool)>>>("ZombieLibrary", "AlmanacEntries");
    private static readonly Action<string, string, bool, bool>? setPlant = Bind<Action<string, string, bool, bool>>("PlantLibrary", "SetUnlock");
    private static readonly Action<string, string, bool, bool>? setZombie = Bind<Action<string, string, bool, bool>>("ZombieLibrary", "SetUnlock");
    private static readonly Action? resetPlants = Bind<Action>("PlantLibrary", "ResetUnlocks");
    private static readonly Action? resetZombies = Bind<Action>("ZombieLibrary", "ResetUnlocks");
    internal static void SetPlant(string owner, string id, bool unlocked, bool almanac)
    {
        if (setPlant == null) throw new ApiError("LIBRARY_API_UNAVAILABLE", "Plant Library 1.0.4 or newer is required.");
        setPlant(owner, id, unlocked, almanac);
    }
    internal static void SetZombie(string owner, string id, bool duck, bool unlocked)
    {
        if (setZombie == null) throw new ApiError("LIBRARY_API_UNAVAILABLE", "Zombie Library 1.1.4 or newer is required.");
        setZombie(owner, id, duck, unlocked);
    }
    internal static void Reset() { resetPlants?.Invoke(); resetZombies?.Invoke(); }
    internal static object Plants(bool onlyUnlocked) => plants?.Invoke(ApiBoard.Current).Where(p => !onlyUnlocked || p.AlmanacUnlocked)
        .Select(p => new { owner = p.Owner, id = p.Id, name = p.Name, info = p.Info, type = p.NativeType.ToString(), marker = p.Marker.ToString(),
            unlocked = p.AlmanacUnlocked, plantUnlocked = p.PlantUnlocked }).ToArray() ?? Array.Empty<object>();
    internal static object Zombies(bool onlyUnlocked) => zombies?.Invoke().Where(z => !onlyUnlocked || z.Unlocked)
        .Select(z => new { owner = z.Owner, id = z.Id, name = z.Name, info = z.Info, type = z.NativeType.ToString(), duck = z.Duck, unlocked = z.Unlocked }).ToArray() ?? Array.Empty<object>();
}
