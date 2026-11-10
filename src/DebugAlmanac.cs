using HarmonyLib;
using Il2CppReloaded.Data;
using Il2CppReloaded.DataModels;
using Il2CppReloaded.Services;
using Il2CppReloaded.TreeStateActivities;

namespace DebugMod;

// Session-only availability; never write the player's permanent unlocks.
[HarmonyPatch(typeof(UserService), nameof(UserService.IsAlmanacAvailable))]
internal static class DebugAlmanacAvailabilityPatch
{
    private static void Postfix(ref bool __result)
    { if (DebugMode.Active) __result = true; }
}

[HarmonyPatch(typeof(GameplayActivity), nameof(GameplayActivity.CanShowAlmanac))]
internal static class DebugAlmanacGameplayPatch
{
    private static void Postfix(GameplayActivity __instance, ref bool __result)
    { if (DebugMode.Owns(__instance)) __result = true; }
}

[HarmonyPatch(typeof(UserService), nameof(UserService.IsLocked), new[] { typeof(AlmanacEntryData) })]
internal static class DebugAlmanacEntryPatch
{
    private static void Postfix(ref bool __result)
    { if (DebugMode.Active) __result = false; }
}

// Yeti visibility has a separate model predicate even after the lock query.
[HarmonyPatch(typeof(AlmanacEntryModel), "EntryIsHidden")]
internal static class DebugAlmanacHiddenEntryPatch
{
    private static void Postfix(AlmanacEntryModel __instance, ref bool __result)
    { if (ApiUnlocks.Entry(__instance.Value) is { } unlocked) __result = !unlocked; else if (DebugMode.Active) __result = false; }
}

// Availability is cached in long-lived UI models before DebugMode becomes
// active. Refresh those models at the session boundary, including on exit.
internal static class DebugAlmanac
{
    private static bool active;
    private static bool shuttingDown;
    private static AppDataProvider? provider;

    internal static void RefreshCurrent() { if (provider != null) Refresh(provider); }

    internal static void Bind(AppDataProvider value)
    { if (shuttingDown) return; provider = value; if (DebugMode.Active) Refresh(value); }

    internal static void Update()
    {
        if (shuttingDown || active == DebugMode.Active) return;
        active = DebugMode.Active;
        if (provider != null) Refresh(provider);
    }

    internal static void Unbind(Il2CppTekly.Extensions.DataProviders.RootModelDataProvider value)
    { if (provider?.Pointer == value.Pointer) provider = null; }

    // Unity may already have unloaded Addressables by Melon deinitialization.
    // Retire our borrowed provider before session cleanup can refresh models.
    internal static void Shutdown() { shuttingDown = true; provider = null; }

    internal static void Refresh(AppDataProvider provider)
    {
        if (shuttingDown) return;
        var platform = provider.m_platformDataModel;
        if (platform?.m_isAlmanacAvailableModel != null)
            platform.m_isAlmanacAvailableModel.Value = DebugMode.Active || platform.m_userService.IsAlmanacAvailable();
        var almanac = provider.m_almanacModel;
        if (almanac?.m_dataService == null) return;
        // Native lock refresh also rebuilds visible/hidden entries (Yeti),
        // while preserving entry models and their owned async sprite handles.
        almanac.m_plantsModel?.UpdateEntriesLockedState(almanac.m_dataService.PlantAlmanacData);
        almanac.m_zombiesModel?.UpdateEntriesLockedState(almanac.m_dataService.ZombieAlmanacData);
    }
}

[HarmonyPatch(typeof(AppDataProvider), "OnBind")]
internal static class DebugAlmanacBindPatch
{
    private static void Postfix(AppDataProvider __instance)
    { DebugAlmanac.Bind(__instance); }
}

[HarmonyPatch(typeof(Il2CppSource.DataModels.PlatformDataModel), "OnUserLevelChanged")]
internal static class DebugAlmanacPlatformPatch
{
    private static void Postfix(Il2CppSource.DataModels.PlatformDataModel __instance)
    { if (DebugMode.Active) __instance.m_isAlmanacAvailableModel.Value = true; }
}

[HarmonyPatch(typeof(Il2CppSource.DataModels.SeedChooserDataModel), "UpdateSeedChooserScreen")]
internal static class DebugAlmanacChooserPatch
{
    private static void Postfix(Il2CppSource.DataModels.SeedChooserDataModel __instance)
    { if (DebugMode.Active) __instance.m_isAlmanacAvailableModel.Value = true; }
}

[HarmonyPatch(typeof(AlmanacEntryModel), "UpdateModelData")]
internal static class DebugAlmanacModelPatch
{
    private static void Prefix(AlmanacEntryModel __instance)
    { if (ApiUnlocks.Entry(__instance.Value) is { } unlocked) __instance.m_locked = !unlocked; else if (DebugMode.Active) __instance.m_locked = false; }
}

[HarmonyPatch(typeof(Il2CppTekly.Extensions.DataProviders.RootModelDataProvider), "Unbind")]
internal static class DebugAlmanacUnbindPatch
{
    private static void Prefix(Il2CppTekly.Extensions.DataProviders.RootModelDataProvider __instance) =>
        DebugAlmanac.Unbind(__instance);
}
