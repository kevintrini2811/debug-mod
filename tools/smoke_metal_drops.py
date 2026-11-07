"""Verify read-only Fusion drop inspection on an existing temporary Night board.

Creates real drops through native helmet damage, checks paused snapshots, board
isolation and expiry, then restores the session's speed and difficulty settings.
Never starts a game or opens an ordinary saved level.
"""
import json
import math
from pathlib import Path
import shutil
import sys
import time

from debug_api import GAME, call

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "validation/metal-drops"
API = GAME / "UserData/DebugModApi"
events = []


def api(command, **args):
    response = call(API, command, args)
    events.append(dict(command=command, args=args, response=response, wall=time.monotonic()))
    assert response["ok"], response
    return response["result"]


def wait(command, predicate, seconds=30):
    deadline = time.monotonic() + seconds
    last = None
    while time.monotonic() < deadline:
        response = call(API, command, {})
        events.append(dict(command=command, args={}, response=response, wall=time.monotonic()))
        if response["ok"]:
            last = response["result"]
            if result := predicate(last):
                return result
        time.sleep(.04)
    raise AssertionError(("Timed out", command, last))


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    state = api("status")
    assert state["debug"] and state["scene"] == "Playing", "A temporary playing debug board is required."
    board = state["board"]
    original_speed = api("speed")["selected"]
    original_pause = state["paused"]
    original_difficulty = api("difficulty")["value"]
    original_settings = api("settings")

    def drops():
        value = api("debug.items")
        assert value["board"] == board and isinstance(value["items"], list), value
        assert isinstance(value["fusionDrops"], list), value
        return value["fusionDrops"]

    def create_drop():
        assert not drops(), "Start each drop case without existing drops."
        for _ in range(20):
            api("pause", paused=False)
            zombie = api("zombie.place", type="Pail", col=5, row=1)["native"]
            api("debug.zombie.set", board=board, id=zombie["DataID"], health=10000, freezeTicks=1000000)
            api("debug.zombie.hit", board=board, id=zombie["DataID"], damage=zombie["mHelmHealth"])
            api("pause", paused=True)
            found = drops()
            api("zombie.remove", board=board, id=zombie["DataID"])
            if found:
                assert len(found) == 1, found
                drop = found[0]
                assert drop["type"] == "Potatomine" and drop["marker"] == "Magnetshroom", drop
                assert drop["row"] == 1 and 0 <= drop["age"] <= 800, drop
                assert all(math.isfinite(drop[key]) for key in ("x", "y")), drop
                return found
        raise AssertionError("No real bucket drop after twenty ordinary helmet losses.")

    try:
        api("difficulty", board=board, value=3)
        assert not api("plants")["items"] and not api("zombies")["items"], "Start with an empty temporary board."
        baseline = create_drop()
        for _ in range(3):
            time.sleep(.15)
            assert drops() == baseline, "Paused reads must not advance age, physics or selection."
        screenshot = api("screenshot")
        shutil.copy2(screenshot["path"], OUTPUT / "bucket-drop.png")
        print("PASS real bucket identity and repeated unchanged paused snapshots", flush=True)

        # Leave while the drop is still alive, rather than after it expires.
        previous_board = board
        api("pause", paused=False)
        api("level.leave")
        wait("status", lambda value: value["ready"] and not value["board"])
        api("debug.start", index=2)
        for phase in (False, True):
            wait("chooser", lambda value: value["ready"] and value["zombiePhase"] == phase)
            api("chooser.select", indices=[0] if not phase else [], confirm=True)
        state = wait("status", lambda value: value["debug"] and value["scene"] == "Playing" and value)
        board = state["board"]
        assert board != previous_board and not drops(), "New board must not expose the previous board's live drop."
        stale = call(API, "debug.zombie.hit", dict(board=previous_board, id=1, damage=1))
        events.append(dict(command="debug.zombie.hit", args=dict(board=previous_board, id=1, damage=1),
                           response=stale, wall=time.monotonic()))
        assert not stale["ok"] and stale["error"]["code"] == "STALE_BOARD", stale
        print("PASS board transition drops previous owner state", flush=True)

        api("difficulty", board=board, value=3)
        baseline = create_drop()
        first = api("fps")["simulationTick"]
        api("speed", index=6)
        api("pause", paused=False)
        wait("debug.items", lambda value: not value["fusionDrops"] and value)
        api("pause", paused=True)
        elapsed = api("fps")["simulationTick"] - first
        assert elapsed >= 801 - baseline[0]["age"], (elapsed, baseline)
        assert not drops(), "Expired drops must disappear from later snapshots."
        print("PASS ordinary drop expiry removes snapshot entry", flush=True)
    finally:
        try:
            api("pause", paused=False)
            api("speed", index=original_speed)
            api("difficulty", board=board, value=original_difficulty)
            api("pause", paused=original_pause)
            assert api("settings") == original_settings, "Native settings must remain unchanged."
        finally:
            (OUTPUT / "runtime.json").write_text(json.dumps(events, indent=2))


if __name__ == "__main__":
    main()
