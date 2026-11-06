using System.Reflection;
using HarmonyLib;
using Il2CppReloaded.Gameplay;
using ReplantedModUi;

namespace DebugMod;

internal static class OptionalGameplay
{
    private static readonly Func<Board, Plant?>? carried = OptionalModApi.Bind<Func<Board, Plant?>>("LawnGlove", "LawnGlove.LawnGloveMod", "GetCarriedPlant");
    private static readonly Func<bool>? suppress = OptionalModApi.Bind<Func<bool>>("FusionMod", "FusionMod.DebugFusionIntegration", "GetSuppressReplacement");
    private static readonly Action<bool>? setSuppress = OptionalModApi.Bind<Action<bool>>("FusionMod", "FusionMod.DebugFusionIntegration", "SetSuppressReplacement");
    internal static Plant? CarriedPlant(Board board) => carried?.Invoke(board);
    internal static bool SuppressReplacement
    {
        get => suppress?.Invoke() == true;
        set => setSuppress?.Invoke(value);
    }
}

// The glove owns its clock. Bind only when the optional assembly exists.
[HarmonyPatch]
internal static class DebugGloveCooldownPatch
{
    private static readonly Type? glove = OptionalModApi.Type("LawnGlove", "LawnGlove.LawnGloveMod");
    private static readonly MethodInfo? tick = OptionalModApi.Type("LawnGlove", "LawnGlove.GloveState")?.GetMethod("Tick");
    private static bool Prepare() => glove != null;
    private static IEnumerable<MethodBase> TargetMethods() => new[]
    {
        AccessTools.Method(glove!, "OnUpdate"), AccessTools.Method(glove!, "HandleBoardClick")
    };
    private static void Postfix(object ___State, Board ___Board)
    { if (DebugTools.SuppressCooldown(___Board)) tick!.Invoke(___State, new object[] { double.MaxValue }); }
}
