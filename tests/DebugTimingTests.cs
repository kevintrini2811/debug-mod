using System.Text.Json;
using DebugMod;
using Clock = Il2CppReloaded.Constants.Gameplay;

static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    { ++checks; if (!condition) throw new Exception(message); }
    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();
    private static JsonElement Call(string json) => JsonSerializer.SerializeToElement(DebugTiming.Execute(Args(json)));
    private static void Reject(string json, string code)
    {
        int writes = Clock.Writes;
        try { Call(json); throw new Exception("Expected rejection: " + json); }
        catch (ApiError error) { Check(error.Code == code, "expected rejection code"); }
        Check(Clock.Writes == writes, "invalid request makes no native write");
    }
    public static void Main()
    {
        Check(Call("{}").GetProperty("hz").GetInt32() == 120 && Clock.Writes == 0, "inspection has no side effect");
        foreach (string request in new[] { "{\"hz\":0}", "{\"hz\":60}", "{\"hz\":100.1}", "{\"hz\":\"100\"}", "{\"reset\":false}", "{\"reset\":true,\"hz\":100}" })
            Reject(request, "INVALID_ARGUMENT");
        Reject("{\"hz\":100}", "STALE_BOARD");
        Reject("{\"hz\":100,\"board\":\"old\"}", "STALE_BOARD");
        ApiBoard.Current.Debug = false;
        Reject("{\"hz\":100,\"board\":\"live\"}", "DEBUG_REQUIRED");
        ApiBoard.Current.Debug = true;
        Check(Call("{\"hz\":100,\"board\":\"live\"}").GetProperty("overridden").GetBoolean(), "valid request owns current debug board");
        Check(Math.Abs(Clock.TIME_FOR_UPDATE - .01) < 1e-9, "native 100 Hz setter used");
        Call("{\"hz\":120,\"board\":\"live\"}");
        Call("{\"hz\":100,\"board\":\"live\"}");
        DebugTiming.Reset();
        Check(Math.Abs(Clock.TIME_FOR_UPDATE - 1d / 120) < 1e-9, "multiple changes restore original native interval");
        Check(!Call("{}").GetProperty("overridden").GetBoolean(), "reset releases override ownership");
        int afterReset = Clock.Writes;
        DebugTiming.Reset(); Check(Clock.Writes == afterReset, "reset is idempotent");
        Clock.TIME_FOR_UPDATE = .01;
        Call("{\"hz\":120,\"board\":\"live\"}");
        Call("{\"reset\":true,\"board\":\"live\"}");
        Check(Math.Abs(Clock.TIME_FOR_UPDATE - .01) < 1e-9, "explicit reset restores original 100 Hz mode");
        Call("{\"hz\":120,\"board\":\"live\"}");
        Clock.TIME_FOR_UPDATE = .02;
        afterReset = Clock.Writes; DebugTiming.Reset();
        Check(Clock.Writes == afterReset && Clock.TIME_FOR_UPDATE == .02, "later external timing is preserved");
        Reject("{\"hz\":100,\"board\":\"live\"}", "UNSUPPORTED_TIMING");
        Clock.TIME_FOR_UPDATE = 1d / 120;
        Console.WriteLine($"PASS {checks} DebugTiming assertions.");
    }
}
namespace Il2CppReloaded.Constants {
    internal static class Gameplay {
        internal static double TIME_FOR_UPDATE = 1d / 120;
        internal static int Writes;
        internal static void SetReplantedTimeForUpdate(bool harder) { ++Writes; TIME_FOR_UPDATE = harder ? .01 : 1d / 120; }
    }
}
namespace DebugMod {
    internal sealed class Board { internal IntPtr Pointer = (IntPtr)123; internal bool Debug = true; }
    internal static class ApiBoard {
        internal static Board Current = new();
        internal static string Token = "live";
        internal static Board Require() => Current;
    }
    internal static class DebugMode { internal static bool Owns(Board board) => board.Debug; }
    internal sealed class ApiError : Exception {
        internal string Code;
        internal ApiError(string code, string message) : base(message) { Code = code; }
    }
}
