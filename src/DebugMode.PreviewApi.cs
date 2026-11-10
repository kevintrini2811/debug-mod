using System.Text.Json;
using Il2CppReloaded.Gameplay;

namespace DebugMod;

internal static partial class DebugMode
{
    private static (int X, int Y)? apiPreviewPoint;

    private static void SelectZombie(int index)
    {
        board!.ClearCursor(true, 0);
        selectedZombie = index;
        apiPreviewPoint = null;
        refreshUi = true;
    }

    internal static object ApiPreview(JsonElement args)
    {
        var current = ApiBoard.Require();
        if (!Owns(current)) throw new ApiError("DEBUG_REQUIRED", "Preview inspection requires a temporary debug board.");
        if (DebugCommands.Bool(args, "cancel"))
        {
            CancelZombiePlacement(); apiPreviewPoint = null;
            preview?.Dispose(); preview = null;
        }
        if (args.TryGetProperty("index", out _))
        {
            int index = DebugCommands.Int(args, "index", 0, ZombieCount - 1);
            if (!zombies.Contains(index)) throw new ApiError("INVALID_ARGUMENT", "Select a zombie from the current debug roster.");
            if (OptionalGameplay.CarriedPlant(current) != null)
                throw new ApiError("INVALID_STATE", "Finish the carried plant before selecting a zombie.");
            SelectZombie(index);
        }
        if (args.TryGetProperty("col", out _) || args.TryGetProperty("row", out _))
        {
            int col = DebugCommands.Int(args, "col"), row = DebugCommands.Int(args, "row");
            ApiBoard.Cell(current, col, row);
            if (selectedZombie < 0) throw new ApiError("INVALID_STATE", "Select a roster zombie first.");
            apiPreviewPoint = (current.GridToPixelX(col, row) + 40, current.GridToPixelY(col, row) + 40);
        }
        UpdatePreview();
        object? beforePlacement = preview?.Snapshot();
        if (DebugCommands.Bool(args, "place"))
        {
            if (selectedZombie < 0 || apiPreviewPoint == null)
                throw new ApiError("INVALID_STATE", "Select a zombie and target cell before placement.");
            var point = apiPreviewPoint.Value;
            MouseDown(current, point.X, point.Y, 1, 0);
            apiPreviewPoint = null;
            UpdatePreview();
        }
        return new { board = ApiBoard.Token, index = selectedZombie, target = apiPreviewPoint is { } target
            ? new { x = target.X, y = target.Y } : null, rendering = beforePlacement };
    }
}
