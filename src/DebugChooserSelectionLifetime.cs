using HarmonyLib;
using Il2CppSource.Binders;

namespace DebugMod;

// Native UnBind clears model references but leaves a positive focus count.
// OnDisable then attempts to clear those released models. Release focus while
// its references are still valid, before the ordinary native UnBind runs.
[HarmonyPatch(typeof(CoopSelectableBinder), nameof(CoopSelectableBinder.UnBind))]
internal static class DebugChooserSelectionLifetime
{
    internal static readonly HashSet<IntPtr> BoundInDebug = new();

    private static void Prefix(CoopSelectableBinder __instance)
    {
        bool owned = BoundInDebug.Remove(__instance.Pointer);
        if ((!DebugMode.Active && !owned) || __instance.m_selectedCount <= 0) return;
        var players = __instance.m_playerSelectedModels;
        if (__instance.m_selectedModel != null && players != null && players.Length >= 2
            && players[0] != null && players[1] != null) __instance.Unselect();
        // Also reset an already unbound binder; there is no remaining model
        // selection to clear during its later OnDisable callback.
        __instance.m_selectedCount = 0;
    }
}

[HarmonyPatch(typeof(CoopSelectableBinder), nameof(CoopSelectableBinder.Bind))]
internal static class DebugChooserSelectionBindPatch
{
    private static void Prefix(CoopSelectableBinder __instance)
    {
        if (DebugMode.Active) DebugChooserSelectionLifetime.BoundInDebug.Add(__instance.Pointer);
    }
}
