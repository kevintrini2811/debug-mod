using System.Text.Json;

namespace DebugMod;

// File IPC works from the Linux host and the Windows/Proton player without
// opening a network port. Only LateUpdate touches Unity or native game objects.
internal static class DebugCommandApi
{
    internal static readonly string Root = Path.Combine(MelonLoader.Utils.MelonEnvironment.GameRootDirectory, "UserData", "DebugModApi");
    internal static string Session { get; private set; } = "";
    private static DateTime nextPoll, nextHeartbeat;
    private static FileStream? lease;
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    internal static void Start()
    {
        try
        {
            Directory.CreateDirectory(Root);
            lease = new FileStream(Path.Combine(Root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            Session = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(Path.Combine(Root, "requests"));
            Directory.CreateDirectory(Path.Combine(Root, "responses"));
            Directory.CreateDirectory(Path.Combine(Root, "screenshots"));
            Heartbeat();
            Mod.Instance.LoggerInstance.Msg($"Debug API 1: {Root}");
        }
        catch (Exception error) { lease?.Dispose(); lease = null; Mod.Instance.LoggerInstance.Error($"Debug API unavailable: {error.Message}"); }
    }

    internal static void Write(string path, object value)
    {
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Json));
        File.Move(temporary, path, true);
    }

    private static void Heartbeat()
    {
        Write(Path.Combine(Root, "status.json"), new { protocol = 1,
            version = typeof(Mod).Assembly.GetName().Version?.ToString(3), session = Session,
            pid = Environment.ProcessId, updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), state = DebugCommands.Status() });
        nextHeartbeat = DateTime.UtcNow.AddSeconds(1);
    }

    internal static void Update()
    {
        if (lease == null || DateTime.UtcNow < nextPoll) return;
        nextPoll = DateTime.UtcNow.AddMilliseconds(40);
        try
        {
            if (DateTime.UtcNow >= nextHeartbeat) Heartbeat();
            // Bounded work per frame; a malformed or expired request cannot
            // prevent the remaining requests from being processed.
            foreach (string path in Directory.EnumerateFiles(Path.Combine(Root, "requests"), "*.json").Take(4))
                Process(path);
        }
        catch (Exception error) { Mod.Instance.LoggerInstance.Warning($"Debug API I/O: {error.Message}"); }
    }

    private static void Process(string path)
    {
        string id = Path.GetFileNameWithoutExtension(path);
        if (!Guid.TryParseExact(id, "N", out _)) { File.Delete(path); return; }
        string response = Path.Combine(Root, "responses", id + ".json");
        if (File.Exists(response)) { File.Delete(path); return; }
        object result;
        try
        {
            if (new FileInfo(path).Length > 65536) throw new ApiError("REQUEST_TOO_LARGE", "Request exceeds 64 KiB.");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var request = document.RootElement;
            if (request.GetProperty("protocol").GetInt32() != 1) throw new ApiError("PROTOCOL_MISMATCH", "Expected protocol 1.");
            if (request.GetProperty("session").GetString() != Session) throw new ApiError("STALE_SESSION", "Game session has changed.");
            if (request.GetProperty("expiresAt").GetInt64() < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                throw new ApiError("REQUEST_EXPIRED", "Command was not executed.");
            string command = request.GetProperty("command").GetString() ?? "";
            var args = request.GetProperty("args");
            result = new { protocol = 1, id, session = Session, ok = true, result = DebugCommands.Execute(command, args) };
        }
        catch (Exception error)
        {
            while (error is System.Reflection.TargetInvocationException && error.InnerException != null) error = error.InnerException;
            if (error is not (ApiError or ArgumentException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException))
                Mod.Instance.LoggerInstance.Error($"Debug API failed: {error}");
            result = new { protocol = 1, id, session = Session, ok = false,
                error = new { code = error is ApiError known ? known.Code : error is ArgumentException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException ? "INVALID_REQUEST" : "COMMAND_FAILED", message = error.Message } };
        }
        // Persist the response before deleting the request. On a write failure
        // do not execute the mutation a second time during this process.
        try { Write(response, result); }
        finally { File.Delete(path); }
    }

    internal static void Stop()
    {
        ApiBoard.ReleasePause();
        if (lease == null) return;
        try { Write(Path.Combine(Root, "status.json"), new { protocol = 1, session = Session, stopped = true, updatedAt = 0 }); }
        finally { lease.Dispose(); lease = null; }
    }
}

internal sealed class ApiError : Exception
{
    internal string Code { get; }
    internal ApiError(string code, string message) : base(message) => Code = code;
}
