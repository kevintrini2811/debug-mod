using HarmonyLib;
using Il2CppSource.DataModels;
using Il2CppReloaded.Gameplay;
using Il2CppReloaded.Input;
using Il2CppTekly.DataModels.Binders;
using Il2CppTekly.DataModels.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DebugMod;

// Native Selectable events and submit actions remain in control. Resolve horizontal
// neighbors from live page entries because pooled cards retain stale native links.
internal static class DebugChooserNavigation
{
    private static int? pendingRow;
    private static bool enterFromRight;
    private static SeedChooserScreen? pendingScreen;
    private static IntPtr activeScreen;
    private static bool zombiePhase;

    internal static void PageChanged(SeedChooserScreen screen)
    {
        if (pendingRow != null && pendingScreen?.Pointer == screen.Pointer) return;
        pendingRow = 0; enterFromRight = false; pendingScreen = screen;
    }

    private static IEnumerable<(SeedChooserEntryModel Model, Selectable Selectable)> Entries(SeedChooserScreen screen)
    {
        var panel = DebugSeedChooser.BoundPanel(screen);
        if (panel == null) yield break;
        foreach (var binding in panel.GetComponentsInChildren<BinderContainer>())
        {
            if (!binding.m_hasBound || !binding.TryGet(binding.ResolveFullKey(), out IModel model)) continue;
            var entry = model?.TryCast<SeedChooserEntryModel>();
            if (entry?.m_seedChooserScreen?.Pointer != screen.Pointer || entry.m_chosenSeed == null) continue;
            var selectable = binding.GetComponent<Selectable>() ?? binding.GetComponentInChildren<Selectable>();
            if (selectable != null && selectable.IsInteractable()) yield return (entry, selectable);
        }
    }

    internal static bool Move(Selectable source, AxisEventData eventData)
    {
        var direction = eventData.moveDir;
        var screen = DebugSeedChooser.SelectionScreen;
        if (screen == null || direction is not (MoveDirection.Left or MoveDirection.Right)) return true;
        var selected = source.gameObject;
        if (selected == null || !selected) return true;
        var entries = Entries(screen).Where(entry => DebugPlantCards.OnPage(entry.Model.m_index)).ToArray();
        var current = entries.FirstOrDefault(entry => entry.Selectable.gameObject.Pointer == selected.Pointer
            || selected.transform.IsChildOf(entry.Selectable.transform));
        if (current.Model == null) return true;
        var seed = current.Model.m_chosenSeed;
        if (seed.mSeedState != ChosenSeedState.SeedInChooser) return true;
        bool right = direction == MoveDirection.Right;
        var neighbors = entries.Where(entry => entry.Model.m_chosenSeed.mSeedState == ChosenSeedState.SeedInChooser
            && entry.Model.m_chosenSeed.mEndY == seed.mEndY
            && (right ? entry.Model.m_chosenSeed.mEndX > seed.mEndX : entry.Model.m_chosenSeed.mEndX < seed.mEndX))
            .OrderBy(entry => Math.Abs(entry.Model.m_chosenSeed.mEndX - seed.mEndX)).ToArray();
        if (neighbors.Length > 0)
        {
            eventData.selectedObject = neighbors[0].Selectable.gameObject;
            return false;
        }
        int page = DebugPlantCards.CurrentPage;
        if (screen.mSeedsInFlight != 0 || right && page >= DebugPlantCards.LastPage || !right && page == 0) return true;
        pendingRow = seed.mEndY; enterFromRight = !right; pendingScreen = screen;
        DebugPlantCards.SetPage(page + (right ? 1 : -1));
        return false;
    }

    internal static object Selection()
    {
        var selected = EventSystem.current?.currentSelectedGameObject;
        if (!selected) selected = null;
        var screen = DebugSeedChooser.SelectionScreen;
        int? index = null;
        if (screen != null && selected != null)
            foreach (var entry in Entries(screen))
                if (entry.Selectable.gameObject.Pointer == selected.Pointer || selected.transform.IsChildOf(entry.Selectable.transform))
                { index = entry.Model.m_index; break; }
        return new { name = selected?.name, index };
    }

    internal static void Update()
    {
        var screen = DebugSeedChooser.SelectionScreen;
        if (screen == null)
        { activeScreen = IntPtr.Zero; pendingRow = null; pendingScreen = null; return; }
        if (activeScreen != screen.Pointer || zombiePhase != DebugSeedChooser.ZombiePhase)
        {
            activeScreen = screen.Pointer; zombiePhase = DebugSeedChooser.ZombiePhase;
            pendingRow = 0; enterFromRight = false; pendingScreen = screen;
        }
        // Rebinding the phase can destroy the previously selected pooled card
        // after the first Shown frame. Recover only a missing native selection.
        if (pendingRow == null && EventSystem.current != null && !EventSystem.current.currentSelectedGameObject)
            PageChanged(screen);
        if (pendingRow == null || !DebugSeedChooser.ApiReady(screen)) return;
        if (screen == null || screen.Pointer != pendingScreen?.Pointer)
        { pendingRow = null; pendingScreen = null; return; }
        var entries = Entries(screen).Where(entry => entry.Model.m_chosenSeed.mSeedState == ChosenSeedState.SeedInChooser
            && DebugPlantCards.OnPage(entry.Model.m_index)).ToArray();
        if (entries.Length == 0 || EventSystem.current == null) return;
        var target = entries.OrderBy(entry => Math.Abs(entry.Model.m_chosenSeed.mEndY - pendingRow.Value))
            .ThenBy(entry => enterFromRight ? -entry.Model.m_chosenSeed.mEndX : entry.Model.m_chosenSeed.mEndX).First();
        EventSystem.current.SetSelectedGameObject(target.Selectable.gameObject);
        InputNavigationManager.Instance.RefreshSelectionImmediate(target.Selectable.gameObject);
        foreach (var grid in target.Selectable.GetComponentsInParent<GridNavigationContainer>())
        {
            grid.ConfigureNavigationTargets();
            grid.LastKnownSelectionPlayer1 = target.Selectable.gameObject;
            grid.m_lastGridSelectionP1 = target.Selectable.gameObject;
        }
        pendingRow = null; pendingScreen = null;
    }
}

[HarmonyPatch(typeof(Selectable), nameof(Selectable.OnMove))]
internal static class DebugChooserPageNavigationPatch
{
    private static bool Prefix(Selectable __instance, AxisEventData eventData) =>
        DebugChooserNavigation.Move(__instance, eventData);
}
