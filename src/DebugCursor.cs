using System.Text.Json;
using Il2CppReloaded.Gameplay;
using Il2CppSource.Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DebugMod;

internal static partial class DebugCommands
{
    // Observation only. Do not update cursor/controller positions here: a stale
    // position is precisely what the native-input investigation must preserve.
    private static object Cursor(JsonElement args)
    {
        var board = EntityBoard(args);
        if (!DebugMode.Owns(board))
            throw new ApiError("DEBUG_REQUIRED", "Cursor inspection requires a temporary debug board.");
        int player = args.TryGetProperty("player", out _) ? Int(args, "player", 0, 1) : 0;
        var cursor = board.CursorObjects == null ? null : board.CursorObjects[player];
        var preview = board.CursorPreviews == null ? null : board.CursorPreviews[player];
        var gamepads = board.GamepadCursors;
        var gamepad = gamepads != null && player < gamepads.Length ? gamepads[player] : null;
        var widget = board.mApp?.WidgetManager;
        var widgetPoint = widget?.LastMouseX != null && widget.LastMouseY != null
            ? new { x = widget.LastMouseX[player], y = widget.LastMouseY[player] } : null;
        var boardLocal = widgetPoint == null ? null
            : new { x = widgetPoint.x - board.mX, y = widgetPoint.y - board.mY };
        var mouse = Mouse.current;
        var unityPosition = mouse?.position.ReadValue();
        return new {
            board = ApiBoard.Token, player, utc = DateTimeOffset.UtcNow.ToString("O"), frame = Time.frameCount,
            paused = Il2Cpp.TimeUtil.IsTimePaused(), boardPaused = board.mPaused,
            unityInput = new {
                source = "UnityEngine.InputSystem.Mouse.current.position",
                focused = Application.isFocused,
                screen = new { width = Screen.width, height = Screen.height },
                mouse = unityPosition.HasValue ? new { x = unityPosition.Value.x, y = unityPosition.Value.y } : null,
                units = "screen pixels, bottom-left origin"
            },
            nativeMouse = new {
                widget = widgetPoint,
                boardLocal,
                grid = boardLocal == null ? null : CursorMouseGrid(board, boardLocal.x, boardLocal.y),
                previous = board.mPrevMouseX != null && board.mPrevMouseY != null
                    ? new { x = board.mPrevMouseX[player], y = board.mPrevMouseY[player] } : null,
                boardOrigin = new { x = board.mX, y = board.mY },
                units = new { widget = "widget logical pixels before board offset",
                    boardLocal = "board-local logical pixels from selected-player widget minus board origin",
                    grid = "zero-based native Board.PixelToGridX/Y result; raw outside values retained",
                    previous = "board-local logical pixels", boardOrigin = "widget logical pixels" }
            },
            cursor = CursorState(board, cursor), gridPreview = GridPreviewState(board, preview),
            gamepad = CursorGamepadState(board, player, gamepad)
        };
    }

    private static object? CursorGamepadState(Board board, int player, GamepadCursor? cursor)
    {
        if (cursor == null) return null;
        if (cursor.mBoard?.Pointer != board.Pointer)
            return new { sameBoard = false, playerIndex = (int?)null, enabled = (bool?)null,
                gridX = (int?)null, gridY = (int?)null, x = (float?)null, y = (float?)null, units = (object?)null };
        int owner = cursor.PlayerIndex;
        if (owner != player)
            return new { sameBoard = true, playerIndex = (int?)owner, enabled = (bool?)null,
                gridX = (int?)null, gridY = (int?)null, x = (float?)null, y = (float?)null, units = (object?)null };
        return new { sameBoard = true, playerIndex = owner, enabled = cursor.Enabled,
            gridX = cursor.m_gridX, gridY = cursor.m_gridY, x = cursor.mX, y = cursor.mY,
            units = new { grid = "zero-based stored native DPad grid", position = "board-local logical pixels; raw initialization values retained" } };
    }

    private static object CursorMouseGrid(Board board, int x, int y)
    {
        int col = board.PixelToGridX(x, y), row = board.PixelToGridY(x, y);
        return new { col, row, inBounds = col >= 0 && col < 9 && row >= 0 && row < board.GetNumRows() };
    }

    private static object? CursorState(Board board, CursorObject? cursor)
    {
        if (cursor == null) return null;
        if (cursor.mBoard?.Pointer != board.Pointer)
            return new { sameBoard = false, playerIndex = (int?)null, raw = (object?)null, identity = (object?)null, preview = (object?)null };
        var identity = DebugPlantCards.CursorIdentity(cursor);
        return new { sameBoard = true, playerIndex = cursor.PlayerIndex,
            raw = new { type = cursor.mType.ToString(), imitaterType = cursor.mImitaterType.ToString(),
                seedBankIndex = cursor.mSeedBankIndex, cursorType = cursor.CursorType.ToString(), x = cursor.mX, y = cursor.mY },
            identity = new { found = identity.Found, type = identity.NativeType.ToString(), marker = identity.Marker.ToString() },
            preview = CursorController(cursor.mPreviewController) };
    }

    private static object? GridPreviewState(Board board, CursorPreview? preview)
    {
        if (preview == null) return null;
        if (preview.mBoard?.Pointer != board.Pointer)
            return new { sameBoard = false, playerIndex = (int?)null, gridX = (int?)null, gridY = (int?)null,
                x = (float?)null, y = (float?)null, visible = (bool?)null, controller = (object?)null };
        return new { sameBoard = true, playerIndex = preview.PlayerIndex,
            gridX = preview.mGridX, gridY = preview.mGridY, x = preview.mX, y = preview.mY,
            visible = preview.mVisible, controller = CursorController(preview.mController) };
    }

    private static object? CursorController(PreviewController? controller)
    {
        if (controller == null || !controller) return null;
        var native = controller.m_previewDrawer?.m_previewSprite;
        return new { active = controller.gameObject.activeInHierarchy, position = CursorVector(controller.transform.position),
            units = "Unity world units",
            renderers = controller.GetComponentsInChildren<Renderer>(true).Select(renderer => new {
                name = renderer.name, renderer = renderer.GetIl2CppType().Name,
                native = native != null && renderer.Pointer == native.Pointer,
                enabled = renderer.enabled, active = renderer.gameObject.activeInHierarchy,
                position = CursorVector(renderer.transform.position),
                bounds = new { center = CursorVector(renderer.bounds.center), size = CursorVector(renderer.bounds.size) },
                sprite = renderer.TryCast<SpriteRenderer>()?.sprite?.name
            }).ToArray() };
    }

    private static object CursorVector(Vector3 point) => new { x = point.x, y = point.y, z = point.z };
}
