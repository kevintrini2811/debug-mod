using System.Reflection;
using System.Reflection.Emit;
using DebugMod;
using HarmonyLib;
using UnityEngine;

string mode = args.Single();
MethodInfo? target = null;
if (mode != "absent")
{
    var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("SunCollector"), AssemblyBuilderAccess.Run);
    var type = assembly.DefineDynamicModule("SunCollector").DefineType("SunCollector.SunIndicator", TypeAttributes.Public);
    if (mode == "present")
    {
        var method = type.DefineMethod("CopyFace", MethodAttributes.Private | MethodAttributes.Static,
            typeof(Sprite), new[] { typeof(Sprite) });
        method.DefineParameter(1, ParameterAttributes.None, "source");
        var il = method.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ret);
    }
    target = type.CreateType()!.GetMethod("CopyFace", BindingFlags.Static | BindingFlags.NonPublic);
}
var harmony = new Harmony("DebugCollectorPatchTests." + mode);
var patched = harmony.CreateClassProcessor(typeof(DebugCollectorSourcePatch)).Patch();
if (mode == "present")
{
    if (target == null || Harmony.GetPatchInfo(target)?.Prefixes.Count != 1)
        throw new Exception("present Sun Collector target must receive exactly one prefix");
    var result = target.Invoke(null, new object[] { DebugZombieChooserTheme.Borrowed });
    if (!ReferenceEquals(result, DebugZombieChooserTheme.Original))
        throw new Exception("hook must substitute the original borrowed artwork before cropping");
    var other = new Sprite();
    if (!ReferenceEquals(target.Invoke(null, new object[] { other }), other))
        throw new Exception("unrelated artwork must pass through unchanged");
}
else if (patched != null && patched.Count != 0)
    throw new Exception("absent integration must skip patch processing");
harmony.UnpatchAll(harmony.Id);
Console.WriteLine($"PASS actual Harmony optional collector hook: {mode}.");

namespace UnityEngine { public sealed class Sprite {} }
namespace DebugMod {
 internal static class DebugZombieChooserTheme {
    internal static readonly UnityEngine.Sprite Borrowed = new(), Original = new();
    internal static UnityEngine.Sprite CollectorSource(UnityEngine.Sprite source) => ReferenceEquals(source, Borrowed) ? Original : source;
 }
}
