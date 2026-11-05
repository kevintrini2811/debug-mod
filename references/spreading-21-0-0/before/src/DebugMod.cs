using MelonLoader;
using ReplantedModUi;
using Il2CppReloaded.Gameplay;
using System.Reflection;

[assembly: MelonInfo(typeof(DebugMod.Mod), "Debug Mod", "1.1.1", "Kevin / Codex")]
[assembly: MelonGame("PopCap Games", "PvZ Replanted")]
[assembly: MelonOptionalDependencies("FusionMod", "LawnGlove", "PlantLibrary", "ZombieLibrary", "SpeedMod",
    "DifficultyMod", "SunCollector", "BloomEngine", "PvZRepatched", "ReplantAPI")]

namespace DebugMod;

public sealed class Mod : MelonMod
{
    internal static Mod Instance = null!;
    private PropertyInfo? externalChooser, revealAlmanac;
    private Func<Board, bool>? previousChooser;
    private Func<bool>? previousAlmanac;
    private Func<Board, bool> owns = null!;
    private Func<bool> active = null!;

    public override void OnInitializeMelon()
    { Instance = this; DebugCommandApi.Start(); }

    public override void OnLateInitializeMelon()
    {
        externalChooser = OptionalModApi.Type("PlantLibrary", "PlantLibrary.Plants")?.GetProperty("ExternalChooser");
        revealAlmanac = OptionalModApi.Type("ZombieLibrary", "ZombieLibrary.Zombies")?.GetProperty("RevealAllAlmanacEntries");
        previousChooser = (Func<Board, bool>?)externalChooser?.GetValue(null);
        previousAlmanac = (Func<bool>?)revealAlmanac?.GetValue(null);
        owns = board => DebugMode.Owns(board) || previousChooser?.Invoke(board) == true;
        active = () => DebugMode.Active || previousAlmanac?.Invoke() == true;
        externalChooser?.SetValue(null, owns);
        revealAlmanac?.SetValue(null, active);
        LoggerInstance.Msg("Debug ready. Type DEBUG in the main menu. No progress or board saves during debug sessions.");
    }

    public override void OnLateUpdate()
    { DebugMode.Update(); DebugCommandApi.Update(); }

    public override void OnDeinitializeMelon()
    {
        ApiLibraries.Reset();
        DebugCommandApi.Stop();
        DebugMode.Clear();
        if (Equals(externalChooser?.GetValue(null), owns)) externalChooser!.SetValue(null, previousChooser);
        if (Equals(revealAlmanac?.GetValue(null), active)) revealAlmanac!.SetValue(null, previousAlmanac);
    }
}
