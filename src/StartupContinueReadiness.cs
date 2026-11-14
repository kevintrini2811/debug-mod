using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DebugMod;

internal static class StartupContinueReadiness
{
    internal static Button? FindInteractiveLoadBar()
    {
        try
        {
            var buttons = Object.FindObjectsOfType<Button>()
                .Where(button => button && button.gameObject.activeInHierarchy && button.name == "LoadBar"
                    && InSplashPanel(button.transform)).ToArray();
            if (buttons.Length != 1) return null;

            var candidate = buttons[0];
            return candidate && candidate.isActiveAndEnabled && candidate.IsInteractable() ? candidate : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool InSplashPanel(Transform transform)
    {
        for (var parent = transform.parent; parent != null; parent = parent.parent)
            if (parent.name == "SplashScreenPanel" || parent.name == "SplashScreenPanel(Clone)") return true;
        return false;
    }
}
