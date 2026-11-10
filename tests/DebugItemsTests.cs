using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using DebugMod;
using Il2CppReloaded.Gameplay;

string mode = args.Single();
bool provider = mode is "present" or "origins";
if (mode == "origins") DropFixture.Payload = new object[] {
    new { type = "Potatomine", marker = "Magnetshroom", row = 2, x = 120.5f, y = 200f, age = 17, spawnX = (float?)0, spawnGroundY = (float?)-25.75f },
    new { type = "Scaredyshroom", marker = "Pumpkinshell", row = 3, x = 500f, y = 250f, age = 77, spawnX = (float?)null, spawnGroundY = (float?)null },
    new { type = "Potatomine", marker = "Magnetshroom", row = 1, x = 40f, y = 100f, age = 12 },
    new { type = "Potatomine", marker = "Magnetshroom", row = 0, x = 10f, y = 20f, age = 1, spawnX = "unknown", spawnGroundY = "unknown" },
    new { type = "Potatomine", marker = "Magnetshroom", row = 4, x = 30f, y = 80f, age = 2, spawnX = (float?)12.25f }
};
string originalProviderPayload = JsonSerializer.Serialize(DropFixture.Payload);
if (mode != "absent")
{
    var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("FusionMod"), AssemblyBuilderAccess.Run);
    var type = assembly.DefineDynamicModule("FusionMod").DefineType("FusionMod.DebugFusionIntegration", TypeAttributes.Public);
    if (provider || mode == "wrong-signature")
    {
        var method = type.DefineMethod("MetalDrops", MethodAttributes.Public | MethodAttributes.Static,
            provider ? typeof(object[]) : typeof(string), new[] { typeof(Board) });
        var il = method.GetILGenerator();
        if (provider)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(DropFixture).GetMethod(nameof(DropFixture.Read))!);
        }
        else il.Emit(OpCodes.Ldstr, "old incompatible endpoint");
        il.Emit(OpCodes.Ret);
    }
    type.CreateType();
}

void Check(bool value, string message) { if (!value) throw new Exception(message); }
var board = new Board();
board.m_gridItems.Add(new GridItem { DataID = 7 });
board.m_gridItems.Add(new GridItem { DataID = 8, mDead = true });
DebugMode.Active = false;
try { DebugCommands.Run(board); throw new Exception("ordinary board must be rejected"); }
catch (ApiError error) { Check(error.Code == "DEBUG_REQUIRED", "ordinary board error code"); }
Check(DropFixture.Calls == 0, "ordinary board must not invoke the optional provider");
DebugMode.Active = true;
var value = JsonSerializer.SerializeToElement(DebugCommands.Run(board));
Check(value.GetProperty("board").GetString() == "current", "response must identify the current board");
var items = value.GetProperty("items");
Check(items.GetArrayLength() == 1 && items[0].GetProperty("DataID").GetInt32() == 7,
    "native live item snapshots must retain their existing shape and dead filtering");
var drops = value.GetProperty("fusionDrops");
Check(drops.ValueKind == JsonValueKind.Array, "fusionDrops must always be an array");
if (provider)
{
    Check(DropFixture.Calls == 1 && ReferenceEquals(DropFixture.LastBoard, board),
        "bound delegate must receive the actual current board exactly once");
    Check(drops.GetArrayLength() == DropFixture.Payload.Length && drops[0].GetProperty("type").GetString() == "Potatomine"
        && drops[0].GetProperty("marker").GetString() == "Magnetshroom"
        && drops[0].GetProperty("row").GetInt32() == 2 && drops[0].GetProperty("age").GetInt32() == 17
        && drops[0].GetProperty("x").GetSingle() == 120.5f && drops[0].GetProperty("y").GetSingle() == 200,
        "foreign scalar snapshot objects must survive JSON serialization unchanged");
    var originals = JsonSerializer.SerializeToElement(DropFixture.Payload);
    for (int i = 0; i < drops.GetArrayLength(); ++i)
    {
        foreach (string key in new[] { "type", "marker", "row", "x", "y", "age" })
            Check(drops[i].GetProperty(key).GetRawText() == originals[i].GetProperty(key).GetRawText(),
                $"existing field {key} must stay unchanged for drop {i}");
        Check(drops[i].TryGetProperty("spawnX", out _) && drops[i].TryGetProperty("spawnGroundY", out _),
            "both additive keys must exist even for legacy providers");
    }
    if (mode == "present")
        Check(drops[0].GetProperty("spawnX").ValueKind == JsonValueKind.Null
            && drops[0].GetProperty("spawnGroundY").ValueKind == JsonValueKind.Null,
            "legacy missing origins must be null, never current x/y or zero defaults");
    else
    {
        Check(drops[0].GetProperty("spawnX").GetSingle() == 0
            && drops[0].GetProperty("spawnGroundY").GetSingle() == -25.75f,
            "known origins include genuine zero and negative board coordinates");
        for (int i = 1; i <= 3; ++i)
            Check(drops[i].GetProperty("spawnX").ValueKind == JsonValueKind.Null
                && drops[i].GetProperty("spawnGroundY").ValueKind == JsonValueKind.Null,
                "restored unknown, old and malformed origins must remain null");
        Check(drops[4].GetProperty("spawnX").GetSingle() == 12.25f
            && drops[4].GetProperty("spawnGroundY").ValueKind == JsonValueKind.Null,
            "a missing individual coordinate must not create a zero default");
    }
    string first = value.GetRawText();
    value = JsonSerializer.SerializeToElement(DebugCommands.Run(board));
    Check(first == value.GetRawText(), "repeated inspection must preserve age, position and origin");
    Check(DropFixture.Calls == 2 && originalProviderPayload == JsonSerializer.Serialize(DropFixture.Payload),
        "each read calls the provider exactly once and never mutates its snapshot objects");
}
else Check(drops.GetArrayLength() == 0 && DropFixture.Calls == 0,
    "absent, old and incompatible optional endpoints must leave native inspection available");
Check(board.m_gridItems.Count == 2 && !board.m_gridItems[0].mDead,
    "inspection must not remove or modify native items");
ApiBoard.Token = "next";
var next = new Board { Pointer = (IntPtr)99 };
value = JsonSerializer.SerializeToElement(DebugCommands.Run(next));
Check(value.GetProperty("board").GetString() == "next"
    && value.GetProperty("items").GetArrayLength() == 0
    && value.GetProperty("fusionDrops").GetArrayLength() == 0,
    "a new board must receive its own snapshots without retaining prior results");
if (provider) Check(ReferenceEquals(DropFixture.LastBoard, next), "new board must reach the provider");
Console.WriteLine($"PASS debug.items native snapshots, optional Fusion endpoint, serialization and board isolation: {mode}.");

public static class DropFixture
{
    public static int Calls;
    public static Board? LastBoard;
    public static object[] Payload = new object[] { new { type = "Potatomine", marker = "Magnetshroom", row = 2, x = 120.5f, y = 200f, age = 17 } };
    public static object[] Read(Board board)
    {
        ++Calls; LastBoard = board;
        return board.Pointer == (IntPtr)42
            ? Payload
            : Array.Empty<object>();
    }
}

namespace Il2CppReloaded.Gameplay
{
    public sealed class Board { public IntPtr Pointer = (IntPtr)42; public List<GridItem> m_gridItems = new(); }
    public sealed class GridItem { public int DataID; public bool mDead; }
}
namespace DebugMod
{
    internal sealed class ApiError : Exception
    {
        internal ApiError(string code, string message) : base(message) { Code = code; }
        internal string Code { get; }
    }
    internal static class ApiBoard { internal static string Token = "current"; }
    internal static class DebugMode { internal static bool Active = true; internal static bool Owns(Board board) => Active; }
    internal static partial class DebugCommands
    {
        internal static object Run(Board board) => Items(board);
        private static Dictionary<string, object?> Snapshot(GridItem item) => new() { ["DataID"] = item.DataID };
    }
}
