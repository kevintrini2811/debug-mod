using Il2CppReloaded.Gameplay;
using ReplantedModUi;

namespace DebugMod;

internal static partial class DebugCommands
{
    private static readonly Func<Board, object[]>? metalDrops = BindMetalDrops();

    private static Func<Board, object[]>? BindMetalDrops()
    {
        const string owner = "FusionMod.DebugFusionIntegration";
        var method = OptionalModApi.Type("FusionMod", owner)?.GetMethod("MetalDrops",
            OptionalModApi.Flags, null, new[] { typeof(Board) }, null);
        // Older Fusion versions have no drop inspection endpoint. Keep native
        // item inspection available without loading or requiring another mod.
        return method?.ReturnType == typeof(object[])
            ? OptionalModApi.Bind<Func<Board, object[]>>("FusionMod", owner, "MetalDrops") : null;
    }

    private static object Items(Board board)
    {
        if (!DebugMode.Owns(board))
            throw new ApiError("DEBUG_REQUIRED", "Item inspection requires a temporary debug session.");
        var items = new List<Dictionary<string, object?>>();
        for (int i = 0; i < board.m_gridItems.Count; ++i)
            if (board.m_gridItems[i] is { mDead: false } item) items.Add(Snapshot(item));
        return new { board = ApiBoard.Token, items,
            fusionDrops = metalDrops?.Invoke(board) ?? Array.Empty<object>() };
    }
}
