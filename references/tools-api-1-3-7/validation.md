# Tool diagnostics API 1.3.7

Status: DONE. Verified on 2026-11-06.

Release build passes with zero errors and zero warnings. Offline tests cover
optional Lawn Glove presence and absence, non-mutating reads, native-board
rejection, ownership, argument validation, and reset behavior.

The live Debug Mod API test uses two temporary Day debug sessions. It verifies:

- The installed Lawn Glove state is available and belongs to the current board.
- Repeated reads preserve selected tool and armed state.
- Missing and stale board tokens reject cooldown-setting changes.
- A non-boolean suppression value returns `INVALID_ARGUMENT` without changing
  the existing setting.
- Setting `suppressCooldown:false` persists across subsequent reads.
- Leaving the board removes the override; a newly created debug board defaults
  to `suppressCooldown:true` again.
- A token from the previous board cannot change the new board's setting.
- Outside a board, inspection returns `BOARD_NOT_READY`.
- The profile list and active profile remain unchanged.

The adjacent `runtime.json` retains request and response evidence. The second
debug session remains available for the separate Speed Mod cooldown review.

The game log records a separate Versus Bot exception during the board switch:
`VersusBotMod.VigilarSalidaDeVersus` calls `IGameplayService.get_IsVSMode` after
the native board is cleared and receives `NullReferenceException`. This is
recorded in workspace `ISSUES.md`; the diagnostic API does not patch that
external mod or the game's global mode query. Full log:
`/home/kevin/modding/pvzreplanted/.test-runs/debug-tools-api-20261106/game.log`.
