using Il2CppReloaded.Gameplay;
using ReplantedModUi;
namespace DebugMod;

internal static class DebugBankPreviews
{
    private static readonly Action<Board?>? update = OptionalModApi.Bind<Action<Board?>>("FusionMod", "FusionMod.DebugBankPreviews", "Update");
    private static readonly Action? clear = OptionalModApi.Bind<Action>("FusionMod", "FusionMod.DebugBankPreviews", "Clear");
    internal static void Update(Board? board) => update?.Invoke(board);
    internal static void Clear() => clear?.Invoke();
}
