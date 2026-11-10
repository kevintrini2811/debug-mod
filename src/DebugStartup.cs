using System.Text.Json;
using UnityEngine;

namespace DebugMod;

internal static partial class DebugMode
{
    private static string? startupBackground = StartupArgument();
    private static bool startupLaunched;
    private static float nextStartupAttempt;
    private static readonly JsonElement EmptyArgs = JsonSerializer.SerializeToElement(new { });
    private static string? StartupArgument()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; ++i)
        {
            if (args[i].StartsWith("--debug-level=", StringComparison.Ordinal)) return args[i][14..];
            if (args[i] == "--debug-level") return i + 1 < args.Length ? args[i + 1] : "";
        }
        return null;
    }
    internal static void UpdateStartup()
    {
        if (startupBackground == null || Time.unscaledTime < nextStartupAttempt) return;
        nextStartupAttempt = Time.unscaledTime + .2f;
        try
        {
            if (!startupLaunched)
            {
                if (ApiBoard.Current != null) throw new InvalidOperationException("--debug-level cannot replace an existing board.");
                if (!ApiMenuReady) { DebugCommands.Execute("startup.continue", EmptyArgs); return; }
                Open();
                int index = int.TryParse(startupBackground, out int number) ? number : backgrounds.FindIndex(b =>
                    b.Name.Equals(startupBackground, StringComparison.OrdinalIgnoreCase) || b.Type.ToString().Equals(startupBackground, StringComparison.OrdinalIgnoreCase));
                if (index < 0 || index >= backgrounds.Count) throw new InvalidOperationException("Unknown --debug-level background. Use debug.backgrounds names or indices.");
                startupLaunched = true;
                backgroundIndex = index; Launch();
                return;
            }
            if (!Active) { startupBackground = null; return; }
            if (SessionBoard?.mApp.GameScene == Il2CppReloaded.Gameplay.GameScenes.Playing)
            { startupBackground = null; Mod.Instance.LoggerInstance.Msg("Debug startup complete: placeholder Peashooter and Normal zombie roster."); return; }
            var status = JsonSerializer.SerializeToElement(DebugCommands.Execute("chooser", EmptyArgs));
            if (status.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True)
                DebugCommands.Execute("chooser.select", JsonSerializer.SerializeToElement(new { indices = new[] { 0 }, confirm = true }));
        }
        catch (ApiError error) when (error.Code is "STARTUP_NOT_READY" or "GAME_NOT_READY" or "BOARD_NOT_READY" or "CHOOSER_NOT_READY" or "LEVEL_NOT_READY") { }
        catch (Exception error)
        { startupBackground = null; Mod.Instance.LoggerInstance.Error($"Debug startup stopped: {error.Message}"); }
    }
}
