using HarmonyLib;
using UnityEngine.UI;

namespace DebugMod;

// Packet backgrounds are part of the cached sprites. Mesh generation only
// reads ownership and preserves aspect; it never mutates the canvas hierarchy.
[HarmonyPatch(typeof(Image), "GenerateSimpleSprite")]
internal static class DebugCardAspectPatch
{
    private static void Prefix(Image __instance, ref bool lPreserveAspect)
    {
        if (DebugMode.Active && DebugMode.Art.OwnsCardSprite(__instance.sprite)) lPreserveAspect = true;
    }
}
