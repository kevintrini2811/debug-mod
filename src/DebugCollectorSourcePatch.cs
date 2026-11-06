using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ReplantedModUi;

namespace DebugMod;

// An empty TargetMethods list still makes Harmony try the class-level target.
// Prepare must skip the entire optional patch before target resolution.
[HarmonyPatch]
internal static class DebugCollectorSourcePatch
{
    private static MethodBase? TargetMethod() => OptionalModApi.Type("SunCollector", "SunCollector.SunIndicator")?
        .GetMethod("CopyFace", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Sprite) }, null);
    private static bool Prepare() => TargetMethod() != null;
    private static void Prefix(ref Sprite source) => source = DebugZombieChooserTheme.CollectorSource(source);
}
