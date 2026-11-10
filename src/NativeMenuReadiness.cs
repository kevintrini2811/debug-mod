using Il2CppReloaded.TreeStateActivities;
using Il2CppReloaded.UI;
using Il2CppReloaded.Input;
using Il2CppTekly.PanelViews;
using Il2CppSource.UI;
using Il2CppTekly.Common.Presentables;
using Il2CppTekly.DataModels.Binders;
using Il2CppTekly.TreeState;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DebugMod;

// Object existence and injected services precede native startup completion.
internal static class NativeMenuReadiness
{
    private static bool SplashName(string name) => name is "SplashScreenPanel" or "SplashScreenPanel(Clone)";
    private static GameObject[] SplashRoots(bool includeInactive = false) => Object.FindObjectsOfType<GameObject>(true)
        .Where(value => value && (includeInactive || value.activeInHierarchy) && SplashName(value.name)).ToArray();

    private static bool SplashDismissed(GameObject root)
    {
        try
        {
            // The native prefab root is a persistent container, not a Presentable.
            var containers = root.GetComponents<PanelViewContainer>();
            if (containers.Length != 1) return false;
            var panels = containers[0].m_panels;
            if (panels == null || panels.Length == 0) return false;
            if (panels.Any(panel => panel == null || !panel.transform.IsChildOf(root.transform)
                || panel.State != PresentableState.Hidden || panel.IsAnimating || panel.gameObject.activeInHierarchy)) return false;
            if (panels.Select(panel => panel.Pointer).Distinct().Count() != panels.Length) return false;
            var canvases = root.GetComponentsInChildren<Canvas>(true);
            return canvases.Length != 0 && canvases.All(canvas => panels.Any(panel => canvas.transform.IsChildOf(panel.transform)));
        }
        catch { return false; } // Unknown native ownership can never authorize launch.
    }


    private static string[] BoundButtons(MainMenuPanelView view)
    {
        var unity = view.GetComponentsInChildren<UnityButtonBinder>(true)
            .Where(binder => binder && binder.isActiveAndEnabled && binder.m_buttonModel != null && binder.m_disposable != null
                && binder.m_button != null && binder.m_button.transform.IsChildOf(view.transform)
                && binder.m_button.gameObject.activeInHierarchy && binder.m_button.isActiveAndEnabled && binder.m_button.IsInteractable())
            .Select(binder => "UnityButtonBinder:" + binder.name + "/" + binder.m_button!.name);
        var native = view.GetComponentsInChildren<ButtonBinder>(true)
            .Where(binder => binder && binder.isActiveAndEnabled && binder.m_buttonModel != null && binder.m_disposable != null
                && binder.m_button != null && binder.m_button.transform.IsChildOf(view.transform)
                && binder.m_button.gameObject.activeInHierarchy && binder.m_button.isActiveAndEnabled && binder.m_button.IsInteractable())
            .Select(binder => "ButtonBinder:" + binder.name + "/" + binder.m_button!.name);
        return unity.Concat(native).ToArray();
    }

    private static bool SelectionReady(MainMenuPanelView view)
    {
        var selection = view.LevelSelect;
        if (selection == null || !selection.gameObject.activeInHierarchy || !selection.isActiveAndEnabled
            || !selection.ReachedCarousel || selection.m_selectedCarouselGroup?.levelCarousel == null
            || !selection.m_selectedCarouselGroup.levelCarousel.gameObject.activeInHierarchy
            || selection.m_backButton == null || !selection.m_backButton.gameObject.activeInHierarchy
            || !selection.m_backButton.isActiveAndEnabled || !selection.m_backButton.IsInteractable()
            || !selection.m_backButton.transform.IsChildOf(view.transform)
            || selection.m_tweens == null) return false;
        // Native _tweenCarousel keys this table by the selected carousel's own transform.
        return !selection.m_tweens.TryGetValue(selection.m_selectedCarouselGroup.levelCarousel.transform, out var tween) || !tween.isAlive;
    }

    private static PanelView[] ForeignPanels(MainMenuPanelView view, bool selection)
    {
        var screenPanel = selection ? view.LevelSelect!.GetComponent<PanelView>() : null;
        var carouselPanel = selection ? view.LevelSelect!.m_selectedCarouselGroup?.levelCarousel?.GetComponent<PanelView>() : null;
        return Object.FindObjectsOfType<PanelView>(true)
            .Where(panel => panel && panel.gameObject.activeInHierarchy && panel.State != PresentableState.Hidden
                && panel.Pointer != view.Pointer && !view.transform.IsChildOf(panel.transform)
                && (panel.TryCast<ConfirmPanelView>() != null || panel.TryCast<GameplayOptionsPanelView>() != null
                    || panel.Pointer != screenPanel?.Pointer && panel.Pointer != carouselPanel?.Pointer))
            .ToArray();
    }

    internal static bool TryGet(out MainMenuActivity? menu, out string reason, bool allowLevelSelection = false)
    {
        menu = null;
        if (SplashRoots().Any(root => !SplashDismissed(root))) { reason = "splash-presenting-or-unbound"; return false; }
        var views = Object.FindObjectsOfType<MainMenuPanelView>(true)
            .Where(view => view && view.gameObject.activeInHierarchy).ToArray();
        if (views.Length != 1) { reason = "menu-view-missing-or-ambiguous"; return false; }
        var view = views[0];
        if (view.State != PresentableState.Shown || view.IsAnimating || view.IsTransitioning())
        { reason = "menu-panel-not-settled"; return false; }
        bool selection = view.LevelSelect?.m_levelSelectIsActive == true;
        if (selection && (!allowLevelSelection || !SelectionReady(view)))
        { reason = "level-selection-not-allowed-or-unsettled"; return false; }
        if (ForeignPanels(view, selection).Length != 0)
        { reason = "foreign-or-modal-panel-active"; return false; }
        var navigation = InputNavigationManager._Instance_k__BackingField;
        var layer = navigation?.CurrentLayer;
        if (layer?.gameObject == null || !layer.gameObject.activeInHierarchy || layer.IgnoreInput1
            || !(layer.gameObject.transform.IsChildOf(view.transform)
                || selection && layer.gameObject.transform.IsChildOf(view.LevelSelect!.transform)))
        { reason = "menu-input-layer-missing-blocked-or-foreign"; return false; }
        var activities = Object.FindObjectsOfType<MainMenuActivity>(true)
            .Where(value => value && value.m_container != null && value.Mode == ActivityMode.Active
                && value.TreeState != null && value.TreeState.Mode == ActivityMode.Active
                && value.TreeState.Manager != null && value.TreeState.Manager.m_initialized
                && value.TreeState.Manager.Active?.Pointer == value.TreeState.Pointer).ToArray();
        if (activities.Length != 1) { reason = "menu-state-missing-or-ambiguous"; return false; }
        if (!selection && BoundButtons(view).Length == 0) { reason = "menu-has-no-bound-interactive-button"; return false; }
        menu = activities[0]; reason = "ready"; return true;
    }

    // Diagnostics are independent of the launch decision. A failed getter cannot hide other witnesses.
    private static object? Read(Func<object?> getter)
    {
        try { return getter(); }
        catch (Exception error) { return new { error = error.GetType().Name, message = error.Message[..Math.Min(200, error.Message.Length)] }; }
    }
    private static object PanelSnapshot(PanelView panel) => new
    {
        name = Read(() => panel.name), pointer = Read(() => panel.Pointer.ToString()), id = Read(() => panel.Id),
        active = Read(() => panel.gameObject.activeInHierarchy), state = Read(() => panel.State.ToString()),
        animating = Read(() => panel.IsAnimating)
    };
    private static object SplashSnapshot(GameObject root) => new
    {
        name = Read(() => root.name), pointer = Read(() => root.Pointer.ToString()), active = Read(() => root.activeInHierarchy),
        dismissed = Read(() => SplashDismissed(root)),
        containers = Read(() => root.GetComponents<PanelViewContainer>().Select(container => new
        {
            pointer = Read(() => container.Pointer.ToString()),
            panels = Read(() => container.m_panels?.Select(PanelSnapshot).ToArray())
        }).ToArray()),
        canvases = Read(() => root.GetComponentsInChildren<Canvas>(true).Select(canvas => new
        {
            name = Read(() => canvas.name), pointer = Read(() => canvas.Pointer.ToString()),
            active = Read(() => canvas.gameObject.activeInHierarchy), enabled = Read(() => canvas.enabled),
            sortingOrder = Read(() => canvas.sortingOrder)
        }).ToArray()),
        canvasGroups = Read(() => root.GetComponentsInChildren<CanvasGroup>(true).Select(group => new
        {
            name = Read(() => group.name), pointer = Read(() => group.Pointer.ToString()), active = Read(() => group.gameObject.activeInHierarchy),
            enabled = Read(() => group.enabled), alpha = Read(() => group.alpha), interactable = Read(() => group.interactable),
            blocksRaycasts = Read(() => group.blocksRaycasts), ignoreParentGroups = Read(() => group.ignoreParentGroups)
        }).ToArray()),
        canvasRenderers = Read(() => root.GetComponentsInChildren<CanvasRenderer>(true).Select(renderer => new
        {
            name = Read(() => renderer.name), pointer = Read(() => renderer.Pointer.ToString()),
            active = Read(() => renderer.gameObject.activeInHierarchy), culled = Read(() => renderer.cull), alpha = Read(() => renderer.GetAlpha())
        }).ToArray())
    };

    internal static object Snapshot() => new
    {
        splash = Read(() => SplashRoots(includeInactive: true).Select(SplashSnapshot).ToArray()),
        panels = Read(() => Object.FindObjectsOfType<MainMenuPanelView>(true).Select(view => new
        {
            panel = PanelSnapshot(view), transitioning = Read(() => view.gameObject.activeInHierarchy && view.State == PresentableState.Shown ? (bool?)view.IsTransitioning() : null),
            levelSelect = Read(() => view.LevelSelect?.m_levelSelectIsActive), selectionPointer = Read(() => view.LevelSelect?.Pointer.ToString()),
            reachedCarousel = Read(() => view.LevelSelect?.ReachedCarousel), selectionBackButton = Read(() => view.LevelSelect?.m_backButton?.Pointer.ToString()),
            selectedCarousel = Read(() => view.LevelSelect?.m_selectedCarouselGroup?.levelCarousel?.Pointer.ToString()),
            selectionReady = Read(() => view.LevelSelect?.m_levelSelectIsActive == true && SelectionReady(view)),
            foreignPanels = Read(() => ForeignPanels(view, view.LevelSelect?.m_levelSelectIsActive == true).Select(PanelSnapshot).ToArray()),
            boundInteractiveButtons = Read(() => BoundButtons(view))
        }).ToArray()),
        visiblePanels = Read(() => Object.FindObjectsOfType<PanelView>(true).Where(panel => panel.gameObject.activeInHierarchy).Select(PanelSnapshot).ToArray()),
        input = new
        {
            manager = Read(() => InputNavigationManager._Instance_k__BackingField?.Pointer.ToString()),
            layer = Read(() => InputNavigationManager._Instance_k__BackingField?.CurrentLayer?.gameObject?.name),
            layerPointer = Read(() => InputNavigationManager._Instance_k__BackingField?.CurrentLayer?.gameObject?.Pointer.ToString()),
            ignored = Read(() => InputNavigationManager._Instance_k__BackingField?.CurrentLayer?.IgnoreInput1)
        },
        activities = Read(() => Object.FindObjectsOfType<MainMenuActivity>(true).Select(value => new
        {
            name = Read(() => value.name), pointer = Read(() => value.Pointer.ToString()), mode = Read(() => value.Mode.ToString()),
            injected = Read(() => value.m_container != null), state = Read(() => value.TreeState?.Pointer.ToString()),
            stateMode = Read(() => value.TreeState?.Mode.ToString()), manager = Read(() => value.TreeState?.Manager?.Pointer.ToString()),
            initialized = Read(() => value.TreeState?.Manager?.m_initialized), managerActive = Read(() => value.TreeState?.Manager?.Active?.Pointer.ToString())
        }).ToArray())
    };
}
