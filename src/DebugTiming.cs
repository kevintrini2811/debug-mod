using System.Text.Json;
using NativeGameplay = Il2CppReloaded.Constants.Gameplay;

namespace DebugMod;

// The native interval is global, so its temporary override belongs to exactly
// one debug board and never writes the user's profile or settings service.
internal static class DebugTiming
{
    private static IntPtr owner;
    private static int originalRate, writtenRate;
    private static int Rate()
    {
        double seconds = NativeGameplay.TIME_FOR_UPDATE;
        if (Math.Abs(seconds - .01) < 1e-8) return 100;
        if (Math.Abs(seconds - 1d / 120) < 1e-8) return 120;
        throw new ApiError("UNSUPPORTED_TIMING", "The native interval is not a supported 100/120 Hz mode.");
    }

    internal static void Reset()
    {
        if (owner == IntPtr.Zero) return;
        int restore = originalRate, last = writtenRate;
        owner = IntPtr.Zero;
        originalRate = writtenRate = 0;
        // Preserve a later external timing change rather than taking ownership
        // of a global value that this command no longer controls.
        double current = NativeGameplay.TIME_FOR_UPDATE;
        if (Math.Abs(current - 1d / last) < 1e-8)
            NativeGameplay.SetReplantedTimeForUpdate(restore == 100);
    }

    internal static object Execute(JsonElement args)
    {
        var board = ApiBoard.Require();
        if (!DebugMode.Owns(board))
            throw new ApiError("DEBUG_REQUIRED", "Timing inspection requires a temporary debug session.");
        bool set = args.TryGetProperty("hz", out var requested);
        bool reset = args.TryGetProperty("reset", out var resetValue);
        if (set && reset) throw new ApiError("INVALID_ARGUMENT", "Use either hz or reset.");
        int hz = 0;
        if (set && (requested.ValueKind != JsonValueKind.Number || !requested.TryGetInt32(out hz) || hz is not (100 or 120)))
            throw new ApiError("INVALID_ARGUMENT", "hz must be 100 or 120.");
        if (reset && resetValue.ValueKind != JsonValueKind.True)
            throw new ApiError("INVALID_ARGUMENT", "reset must be true.");
        if (set || reset)
        {
            if (!args.TryGetProperty("board", out var token) || token.ValueKind != JsonValueKind.String || token.GetString() != ApiBoard.Token)
                throw new ApiError("STALE_BOARD", "Timing settings belong to a different board.");
            if (reset) Reset();
            else
            {
                if (owner != IntPtr.Zero && owner != board.Pointer) Reset();
                int current = Rate();
                if (owner == IntPtr.Zero) { originalRate = current; owner = board.Pointer; }
                NativeGameplay.SetReplantedTimeForUpdate(hz == 100);
                writtenRate = hz;
            }
        }
        return new { board = ApiBoard.Token, hz = Rate(), tickSeconds = NativeGameplay.TIME_FOR_UPDATE,
            overridden = owner == board.Pointer };
    }
}
