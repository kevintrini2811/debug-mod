using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using DebugMod;
using Il2CppReloaded.Gameplay;
using LawnGlove;

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static JsonElement Args(string text = "{}") => JsonDocument.Parse(text).RootElement;
static JsonElement Call(string text = "{}") => JsonSerializer.SerializeToElement(DebugTools.Execute(Args(text)));
static void Error(string code, string args)
{
    try { Call(args); throw new Exception("Expected " + code); }
    catch (ApiError error) { Check(error.Code == code, error.Code); }
}

bool present = !args.Contains("--absent");
var state = new GloveState();
if (present)
{
    // An optional assembly fixture exercises the production assembly resolver.
    var module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("LawnGlove"), AssemblyBuilderAccess.Run).DefineDynamicModule("Fixture");
    var builder = module.DefineType("LawnGlove.LawnGloveMod", TypeAttributes.Public);
    builder.DefineDefaultConstructor(MethodAttributes.Public);
    builder.DefineField("Instance", builder, FieldAttributes.Public | FieldAttributes.Static);
    builder.DefineField("Board", typeof(Board), FieldAttributes.Public);
    builder.DefineField("State", typeof(GloveState), FieldAttributes.Public);
    var type = builder.CreateType()!;
    var mod = Activator.CreateInstance(type);
    type.GetField("Instance")!.SetValue(null, mod);
    type.GetField("Board")!.SetValue(mod, ApiBoard.Current);
    type.GetField("State")!.SetValue(mod, state);
    state.Toggle(); state.TryArm(); state.CommitMove();
}
var first = Call();
Check(first.GetProperty("available").GetBoolean() == present, "optional assembly detection");
Check(first.GetProperty("ready").GetBoolean() == present, "board ownership");
Check(first.GetProperty("suppressCooldown").GetBoolean(), "default suppression");
if (present)
{
    Check(first.GetProperty("selectedTool").GetString() == "Glove", "selection snapshot");
    Check(first.GetProperty("remaining").GetDouble() == 10, "read preserves charge");
    Check(!first.GetProperty("armed").GetBoolean(), "armed snapshot");
}
else Check(first.GetProperty("remaining").ValueKind == JsonValueKind.Null, "missing mod has null state");

Error("STALE_BOARD", "{\"suppressCooldown\":false}");
Error("STALE_BOARD", "{\"board\":\"old\",\"suppressCooldown\":false}");
Error("INVALID_ARGUMENT", "{\"board\":\"current\",\"suppressCooldown\":0}");
Check(!Call("{\"board\":\"current\",\"suppressCooldown\":false}").GetProperty("suppressCooldown").GetBoolean(), "disable for board");
Check(!DebugTools.SuppressCooldown(ApiBoard.Current), "patch respects override");
var original = ApiBoard.Current;
ApiBoard.Current = new Board { Pointer = (IntPtr)2 };
DebugMode.Board = ApiBoard.Current;
Check(DebugTools.SuppressCooldown(ApiBoard.Current), "override cannot leak to other board");
if (present) Check(!Call().GetProperty("ready").GetBoolean(), "other glove board is not ready");
ApiBoard.Current = original; DebugMode.Board = original;
DebugTools.Reset();
Check(DebugTools.SuppressCooldown(original), "session reset restores default even for reused pointer");
DebugMode.Board = null;
Error("DEBUG_REQUIRED", "{\"board\":\"current\",\"suppressCooldown\":false}");
Check(!DebugTools.SuppressCooldown(original), "native levels never suppress cooldown");
Console.WriteLine($"DebugTools ({(present ? "optional mod present" : "absent")}): snapshots, no read mutation, defaults, stale board, validation, temporary ownership and reset passed.");

namespace Il2CppReloaded.Gameplay { public sealed class Board { public IntPtr Pointer = (IntPtr)1; } }
namespace DebugMod
{
    internal static class ApiBoard
    {
        internal static Board Current = new();
        internal static string Token => "current";
        internal static Board Require() => Current;
    }
    internal static class DebugMode
    {
        internal static Board? Board = ApiBoard.Current;
        internal static bool Owns(Board board) => Board?.Pointer == board.Pointer;
    }
    internal sealed class ApiError(string code, string message) : Exception(message) { internal string Code => code; }
}
