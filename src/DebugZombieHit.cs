using System.Text.Json;
using Il2CppReloaded.Gameplay;

namespace DebugMod;

internal static partial class DebugCommands
{
    private static object HitZombie(JsonElement args)
    {
        if (!args.TryGetProperty("board", out var token) || token.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(token.GetString()))
            throw new ApiError("INVALID_ARGUMENT", "board: expected the current board token.");
        var board = EntityBoard(args);
        if (!DebugMode.Owns(board))
            throw new ApiError("DEBUG_REQUIRED", "Zombie hits require a temporary debug session.");
        if (!args.TryGetProperty("damage", out var amount) || amount.ValueKind != JsonValueKind.Number)
            throw new ApiError("INVALID_ARGUMENT", "damage: expected integer 1..1000000.");
        if (!args.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetUInt32(out _))
            throw new ApiError("INVALID_ARGUMENT", "id: expected an unsigned 32-bit zombie ID.");
        int damage = Int(args, "damage", 1, 1000000);
        var zombie = FindZombie(board, Id(args));
        if (zombie.mBoard?.Pointer != board.Pointer || zombie.IsDeadOrDying())
            throw new ApiError("ENTITY_NOT_FOUND", "No living zombie with this ID on the current board.");
        // A normal native hit owns shield/helmet absorption, break callbacks,
        // body damage, colour effects and death. Never edit those fields here.
        zombie.TakeDamage(damage, (DamageFlags)0);
        return new { board = ApiBoard.Token, damage, zombie = ZombieInfo(zombie) };
    }
}
