using Il2CppReloaded.Gameplay;
using Il2CppReloaded.TreeStateActivities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DebugMod;

internal static partial class DebugMode
{
    internal static bool ApiSunOverride;
    internal static object ApiBackgrounds()
    {
        if (Active) throw new ApiError("DEBUG_ACTIVE", "Leave the current debug level first.");
        Open();
        return backgrounds.Select((value, index) => new { index, value.Name, background = value.Type.ToString(), value.NoFog }).ToArray();
    }
    internal static void ApiLaunch(int index)
    {
        if (Active) throw new ApiError("DEBUG_ACTIVE", "Leave the current debug level first.");
        Open();
        if (index < 0 || index >= backgrounds.Count) throw new ApiError("INVALID_BACKGROUND", "Use debug.backgrounds to obtain an index.");
        backgroundIndex = index;
        Launch();
    }
    internal static void ApiRoster(int[] indices)
    {
        if (!Active) throw new ApiError("DEBUG_REQUIRED", "No active debug session.");
        if (indices.Length > 10 || indices.Distinct().Count() != indices.Length || indices.Any(i => i < 0 || i >= ZombieCount))
            throw new ApiError("INVALID_ROSTER", "Expected up to ten unique debug zombie catalog indices.");
        SetZombies(indices); refreshUi = true;
    }
    internal static object ApiZombieCatalog() => Enumerable.Range(0, ZombieCount)
        .Select(index => new { index, name = ZombieName(index), type = (index < zombieTypes.Length ? zombieTypes[index] : registeredZombies[index - zombieTypes.Length].NativeType).ToString() }).ToArray();
}

internal static class ApiBoard
{
    internal static Board? Current;
    private static IntPtr pointer;
    private static IntPtr pauseOwner;
    internal static bool PauseOwned => pauseOwner != IntPtr.Zero;
    internal static void ReleasePause()
    {
        if (!PauseOwned) return;
        pauseOwner = IntPtr.Zero;
        Il2Cpp.TimeUtil.UnpauseTime();
    }
    internal static void SetPaused(Board board, bool paused)
    {
        if (!paused) { ReleasePause(); return; }
        if (pauseOwner == board.Pointer) return;
        ReleasePause();
        Il2Cpp.TimeUtil.PauseTime();
        pauseOwner = board.Pointer;
    }
    internal static string Token { get; private set; } = "";
    internal static void Observe(Board board)
    {
        if (pointer != board.Pointer)
        {
            ReleasePause();
            pointer = board.Pointer; Token = Guid.NewGuid().ToString("N");
        }
        Current = board;
    }
    internal static void Clear(Board board)
    {
        if (pointer != board.Pointer) return;
        ReleasePause();
        Current = null; pointer = IntPtr.Zero; Token = "";
    }
    internal static Board Require(bool playing = true)
    {
        if (Current == null || Current.mApp == null) throw new ApiError("BOARD_NOT_READY", "No active level board.");
        if (playing && Current.mApp.GameScene != GameScenes.Playing)
            throw new ApiError("LEVEL_NOT_READY", "Level is loading, choosing seeds or showing its intro.");
        return Current;
    }
    internal static void Cell(Board board, int col, int row)
    {
        if (col < 0 || col >= 9 || row < 0 || row >= board.GetNumRows())
            throw new ApiError("INVALID_CELL", $"Expected col 0..8, row 0..{board.GetNumRows() - 1}.");
    }
}

[HarmonyLib.HarmonyPatch(typeof(GameplayActivity), nameof(GameplayActivity.NewGame))]
internal static class ApiNewBoardPatch
{
    private static void Postfix(GameplayActivity __instance) { if (__instance.Board != null) ApiBoard.Observe(__instance.Board); }
}
[HarmonyLib.HarmonyPatch(typeof(Board), nameof(Board.Update))]
internal static class ApiObserveBoardPatch
{
    private static void Postfix(Board __instance) => ApiBoard.Observe(__instance);
}
[HarmonyLib.HarmonyPatch(typeof(Board), nameof(Board.DisposeBoard))]
internal static class ApiDisposeBoardPatch
{
    private static void Prefix(Board __instance) => ApiBoard.Clear(__instance);
}
