using HarmonyLib;
using Il2CppReloaded.DataModels;

namespace DebugMod;

// Native bank refreshes add each packet separately. Publish the completed
// snapshot once, so adding one card does not rebuild every intermediate deck.
[HarmonyPatch(typeof(SeedBankDataModel), nameof(SeedBankDataModel.UpdateEntries))]
internal static class DebugBankModelsPatch
{
    private static void Prefix(SeedBankDataModel __instance, out bool? __state)
    {
        __state = null;
        if (!DebugMode.Owns(__instance.m_gameplayActivity)) return;
        __state = __instance.m_entriesModel.DisableModifiedTrigger;
        __instance.m_entriesModel.DisableModifiedTrigger = true;
    }

    private static void Finalizer(SeedBankDataModel __instance, bool? __state)
    {
        if (__state is not { } disabled) return;
        __instance.m_entriesModel.DisableModifiedTrigger = disabled;
        if (!disabled) __instance.m_entriesModel.m_modified.Emit(__instance.m_entriesModel);
    }
}
