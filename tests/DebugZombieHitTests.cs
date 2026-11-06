using System.Text.Json;
using DebugMod;
using Il2CppReloaded.Gameplay;

static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static void Reject(string code, string json)
{
    int before = DebugCommands.Target.Hits;
    try { DebugCommands.Run(Args(json)); throw new Exception("request should fail: " + json); }
    catch (ApiError error) { Check(error.Code == code, "wrong rejection: " + error.Code); }
    Check(DebugCommands.Target.Hits == before, "invalid request invoked native damage");
}
const string Valid = "{\"board\":\"current\",\"id\":7,\"damage\":20}";
var result = JsonSerializer.SerializeToElement(DebugCommands.Run(Args(Valid)));
Check(DebugCommands.Target.Hits == 1 && DebugCommands.Target.Damage == 20, "exactly one native hit with requested damage");
Check(DebugCommands.Target.Flags == 0, "ordinary damage must not bypass armor or add special flags");
Check(result.GetProperty("zombie").GetProperty("nativeHitSeen").GetBoolean(), "snapshot must follow native call");
foreach (string damage in new[] { "0", "-1", "1000001", "2147483647", "1.5", "\"20\"", "null" })
    Reject("INVALID_ARGUMENT", "{\"board\":\"current\",\"id\":7,\"damage\":" + damage + "}");
Reject("INVALID_ARGUMENT", "{\"board\":\"current\",\"id\":7}");
Reject("INVALID_ARGUMENT", "{\"id\":7,\"damage\":20}");
Reject("INVALID_ARGUMENT", "{\"board\":null,\"id\":7,\"damage\":20}");
foreach (string id in new[] { "-1", "4294967296", "1.5", "\"7\"", "null" })
    Reject("INVALID_ARGUMENT", "{\"board\":\"current\",\"id\":" + id + ",\"damage\":20}");
Reject("STALE_BOARD", "{\"board\":\"old\",\"id\":7,\"damage\":20}");
Reject("ENTITY_NOT_FOUND", "{\"board\":\"current\",\"id\":8,\"damage\":20}");
DebugMode.Active = false; Reject("DEBUG_REQUIRED", Valid); DebugMode.Active = true;
DebugCommands.Target.Dying = true; Reject("ENTITY_NOT_FOUND", Valid); DebugCommands.Target.Dying = false;
DebugCommands.Target.mDead = true; Reject("ENTITY_NOT_FOUND", Valid); DebugCommands.Target.mDead = false;
DebugCommands.Target.mBoard = new Board { Pointer = (IntPtr)99 }; Reject("ENTITY_NOT_FOUND", Valid);
DebugCommands.Target.mBoard = DebugCommands.Current;
foreach (int damage in new[] { 1, 1000000 })
    DebugCommands.Run(Args("{\"board\":\"current\",\"id\":7,\"damage\":" + damage + "}"));
Check(DebugCommands.Target.Hits == 3, "inclusive damage bounds reach native hit");
Console.WriteLine("PASS native hit dispatch, zero flags, post-hit snapshot, damage bounds, debug ownership, stale token and live same-board ID checks.");

namespace Il2CppReloaded.Gameplay {
 [Flags] public enum DamageFlags { None = 0 }
 public sealed class Board { public IntPtr Pointer = (IntPtr)42; }
 public sealed class Zombie {
    public Board? mBoard; public bool mDead, Dying; public int Hits, Damage; public DamageFlags Flags;
    public bool IsDeadOrDying() => mDead || Dying;
    public void TakeDamage(int damage, DamageFlags flags) { ++Hits; Damage = damage; Flags = flags; }
 }
}
namespace DebugMod {
 internal sealed class ApiError(string code, string message) : Exception(message) { internal string Code => code; }
 internal static class ApiBoard { internal static string Token = "current"; }
 internal static class DebugMode { internal static bool Active = true; internal static bool Owns(Board board) => Active; }
 internal static partial class DebugCommands {
    internal static readonly Board Current = new();
    internal static readonly Zombie Target = new() { mBoard = Current };
    internal static object Run(JsonElement args) => HitZombie(args);
    private static Board EntityBoard(JsonElement args) => args.GetProperty("board").GetString() == ApiBoard.Token ? Current : throw new ApiError("STALE_BOARD", "stale");
    private static int Int(JsonElement args, string key, int min, int max) => args.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int n) && n >= min && n <= max ? n : throw new ApiError("INVALID_ARGUMENT", "damage");
    private static uint Id(JsonElement args) => args.GetProperty("id").GetUInt32();
    private static Zombie FindZombie(Board board, uint id) => id == 7 && !Target.mDead ? Target : throw new ApiError("ENTITY_NOT_FOUND", "missing");
    private static object ZombieInfo(Zombie zombie) => new { nativeHitSeen = zombie.Hits > 0 };
 }
}
