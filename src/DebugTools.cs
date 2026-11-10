using System.Reflection;
using System.Text.Json;
using Il2CppReloaded.Gameplay;
using ReplantedModUi;

namespace DebugMod;

internal static class DebugTools
{
    private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly Type? glove = OptionalModApi.Type("LawnGlove", "LawnGlove.LawnGloveMod");
    private static IntPtr cooldownBoard;

    internal static void Reset() => cooldownBoard = IntPtr.Zero;
    internal static bool SuppressCooldown(Board board) => DebugMode.Owns(board) && cooldownBoard != board.Pointer;

    internal static object Execute(JsonElement args)
    {
        var board = ApiBoard.Require();
        if (!DebugMode.Owns(board)) throw new ApiError("DEBUG_REQUIRED", "Tool inspection requires a temporary debug session.");
        if (args.TryGetProperty("suppressCooldown", out var setting))
        {
            if (!args.TryGetProperty("board", out var token) || token.GetString() != ApiBoard.Token)
                throw new ApiError("STALE_BOARD", "Tool settings belong to a different board.");
            if (setting.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new ApiError("INVALID_ARGUMENT", "suppressCooldown: expected boolean.");
            cooldownBoard = setting.GetBoolean() ? IntPtr.Zero : board.Pointer;
        }
        var mod = glove?.GetField("Instance", Fields)?.GetValue(null);
        var owner = mod == null ? null : Read(mod, "Board") as Board;
        var state = mod == null || owner?.Pointer != board.Pointer ? null : Read(mod, "State");
        return new
        {
            board = ApiBoard.Token,
            suppressCooldown = SuppressCooldown(board),
            available = glove != null,
            ready = state != null,
            selectedTool = state == null ? null : Read(state, "SelectedTool")?.ToString(),
            remaining = state == null ? null : Read(state, "Remaining"),
            chargeFraction = state == null ? null : Read(state, "ChargeFraction"),
            armed = state == null ? null : Read(state, "Armed"),
            customToolSelected = state == null ? null : Read(state, "CustomToolSelected")
        };
    }

    private static object? Read(object value, string name) => value.GetType().GetProperty(name, Fields)?.GetValue(value)
        ?? value.GetType().GetField(name, Fields)?.GetValue(value);
}
