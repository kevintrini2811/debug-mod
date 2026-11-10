using System.Text.Json;
using DebugMod;
using Il2CppReloaded.Gameplay;
using Clock = Il2CppReloaded.Constants.Gameplay;

static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    { ++checks; if (!condition) throw new Exception(message); }
    private static JsonElement Call(object args) => JsonSerializer.SerializeToElement(
        DebugTiming.Execute(JsonSerializer.SerializeToElement(args)));
    private static void Reject(object args, string code)
    {
        int before = Clock.Writes;
        try { Call(args); throw new Exception("expected API rejection"); }
        catch (ApiError error) { Check(error.Code == code, "correct API rejection"); }
        Check(before == Clock.Writes, "rejected command leaves native interval unchanged");
    }
    public static void Main()
    {
        foreach (int original in new[] { 100, 120 })
        {
            Clock.TIME_FOR_UPDATE = 1d / original;
            var first = new Board(); ApiBoard.Observe(first);
            string firstToken = ApiBoard.Token;
            int requested = original == 100 ? 120 : 100;
            Call(new { hz = requested, board = firstToken });
            Check(Math.Abs(Clock.TIME_FOR_UPDATE - 1d / requested) < 1e-9, "native setter uses requested mode");
            int written = Clock.Writes;
            ApiBoard.Observe(first);
            Check(Clock.Writes == written && ApiBoard.Token == firstToken, "same-board observation retains override and token");
            var second = new Board(); ApiBoard.Observe(second);
            Check(Math.Abs(Clock.TIME_FOR_UPDATE - 1d / original) < 1e-9, "board switch restores original interval");
            Check(ApiBoard.Token != firstToken && ApiBoard.Current == second, "board switch rotates current command token");
            Reject(new { hz = requested, board = firstToken }, "STALE_BOARD");
            Check(!Call(new { }).GetProperty("overridden").GetBoolean(), "board switch releases timing ownership");
            string secondToken = ApiBoard.Token;
            Call(new { hz = requested, board = secondToken });
            written = Clock.Writes;
            ApiBoard.Clear(first);
            Check(Clock.Writes == written && ApiBoard.Current == second, "stale board disposal cannot reset current override");
            ApiBoard.Clear(second);
            Check(Math.Abs(Clock.TIME_FOR_UPDATE - 1d / original) < 1e-9, "owning board disposal restores interval");
            Check(ApiBoard.Current == null && ApiBoard.Token == "", "owning disposal invalidates API context");
            written = Clock.Writes; DebugTiming.Reset();
            Check(Clock.Writes == written, "deinit reset after disposal is idempotent");

            // Dispose/Observe defines a new epoch even if native pointers recycle.
            var reused = new Board { Pointer = second.Pointer }; ApiBoard.Observe(reused);
            Check(ApiBoard.Token != secondToken, "reused native pointer gets fresh board token after disposal");
            Reject(new { hz = requested, board = secondToken }, "STALE_BOARD");
            Call(new { hz = requested, board = ApiBoard.Token });
            Clock.TIME_FOR_UPDATE = .02; written = Clock.Writes;
            ApiBoard.Clear(reused);
            Check(Clock.TIME_FOR_UPDATE == .02 && Clock.Writes == written,
                "board disposal preserves a later external timing change");
        }
        Clock.TIME_FOR_UPDATE = 1d / 120;
        var normal = new Board { Debug = false }; ApiBoard.Observe(normal);
        Reject(new { hz = 100, board = ApiBoard.Token }, "DEBUG_REQUIRED");
        Reject(new { }, "DEBUG_REQUIRED");
        ApiBoard.Clear(normal);
        Check(Clock.TIME_FOR_UPDATE == 1d / 120, "normal board inspection never acquires global timing");
        Console.WriteLine($"PASS {checks} actual ApiBoard/DebugTiming lifecycle checks; external native setter is doubled.");
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] internal sealed class HarmonyPatch : Attribute
    { internal HarmonyPatch(Type type, string method) {} }
}
namespace UnityEngine { internal static class Object {} }
namespace Il2CppReloaded.Constants
{
    internal static class Gameplay
    {
        internal static double TIME_FOR_UPDATE = 1d / 120; internal static int Writes;
        internal static void SetReplantedTimeForUpdate(bool harder)
        { ++Writes; TIME_FOR_UPDATE = harder ? .01 : 1d / 120; }
    }
}
namespace Il2CppReloaded.Gameplay
{
    internal enum GameScenes { Playing, Loading }
    internal enum ZombieType { Normal }
    internal sealed class App { internal GameScenes GameScene = GameScenes.Playing; }
    internal sealed class Board
    {
        private static int next = 100;
        internal IntPtr Pointer = (IntPtr)(++next);
        internal App mApp = new(); internal bool Debug = true;
        internal int GetNumRows() => 5;
        public void Update() {} public void DisposeBoard() {}
    }
}
namespace Il2CppReloaded.TreeStateActivities
{
    internal sealed class GameplayActivity
    { public Il2CppReloaded.Gameplay.Board? Board; public void NewGame() {} }
}
namespace Il2Cpp
{
    internal static class TimeUtil
    { internal static void PauseTime() {} internal static void UnpauseTime() {} }
}
namespace DebugMod
{
    internal static class DebugTools { internal static void Reset() {} }
    internal sealed class ApiError : Exception
    { internal string Code; internal ApiError(string code, string message) : base(message) { Code = code; } }
    internal static partial class DebugMode
    {
        internal static bool Active;
        private static List<Background> backgrounds = new();
        private static int backgroundIndex;
        private static Il2CppReloaded.Gameplay.ZombieType[] zombieTypes = Array.Empty<Il2CppReloaded.Gameplay.ZombieType>();
        private static Card[] registeredZombies = Array.Empty<Card>();
        private static int ZombieCount => 0;
        private static bool refreshUi;
        private static string ZombieName(int index) => "";
        private static void Open() {} private static void Launch() {} private static void SetZombies(int[] indices) {}
        internal static bool Owns(Board board) => board.Debug;
        private sealed class Background { internal string Name = "test"; internal string Type = "Day"; internal bool NoFog; }
        private sealed class Card { internal ZombieType NativeType; }
    }
}
