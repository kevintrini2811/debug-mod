using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using DebugMod;
using Il2CppReloaded.Gameplay;

string mode = args.Single();
if (mode != "absent")
{
    var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("FusionMod"), AssemblyBuilderAccess.Run);
    var type = assembly.DefineDynamicModule("FusionMod").DefineType("FusionMod.DebugFusionIntegration", TypeAttributes.Public);
    if (mode is "present" or "wrong-signature")
    {
        var method = type.DefineMethod("MetalDrops", MethodAttributes.Public | MethodAttributes.Static,
            mode == "present" ? typeof(object[]) : typeof(string), new[] { typeof(Board) });
        var il = method.GetILGenerator();
        if (mode == "present")
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
if (mode == "present")
{
    Check(DropFixture.Calls == 1 && ReferenceEquals(DropFixture.LastBoard, board),
        "bound delegate must receive the actual current board exactly once");
    Check(drops.GetArrayLength() == 1 && drops[0].GetProperty("type").GetString() == "Potatomine"
        && drops[0].GetProperty("marker").GetString() == "Magnetshroom"
        && drops[0].GetProperty("row").GetInt32() == 2 && drops[0].GetProperty("age").GetInt32() == 17
        && drops[0].GetProperty("x").GetSingle() == 120.5f && drops[0].GetProperty("y").GetSingle() == 200,
        "foreign scalar snapshot objects must survive JSON serialization unchanged");
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
if (mode == "present") Check(ReferenceEquals(DropFixture.LastBoard, next), "new board must reach the provider");
Console.WriteLine($"PASS debug.items native snapshots, optional Fusion endpoint, serialization and board isolation: {mode}.");

public static class DropFixture
{
    public static int Calls;
    public static Board? LastBoard;
    public static object[] Read(Board board)
    {
        ++Calls; LastBoard = board;
        return board.Pointer == (IntPtr)42
            ? new object[] { new { type = "Potatomine", marker = "Magnetshroom", row = 2, x = 120.5f, y = 200f, age = 17 } }
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
    internal sealed class ApiError(string code, string message) : Exception(message) { internal string Code => code; }
    internal static class ApiBoard { internal static string Token = "current"; }
    internal static class DebugMode { internal static bool Active = true; internal static bool Owns(Board board) => Active; }
    internal static partial class DebugCommands
    {
        internal static object Run(Board board) => Items(board);
        private static Dictionary<string, object?> Snapshot(GridItem item) => new() { ["DataID"] = item.DataID };
    }
}
