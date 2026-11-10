"""Verify API controls without changing existing profiles or native saves."""
import json
from pathlib import Path
import sys
import time

from debug_api import GAME, call


def run():
    root = GAME / "UserData/DebugModApi"
    output = Path(__file__).resolve().parents[1] / "validation/controls-runtime.json"
    output.parent.mkdir(exist_ok=True)
    records, checks = [], []
    collector = None
    board = None
    collector_file = GAME / "UserData/SunCollector.cfg"
    collector_bytes = collector_file.read_bytes() if collector_file.exists() else None

    def api(command, expected=None, **args):
        response = call(root, command, args)
        records.append(dict(command=command, args=args, response=response))
        if expected:
            assert not response["ok"] and response["error"]["code"] == expected, response
            return response
        assert response["ok"], response
        return response["result"]

    def wait(command, predicate, timeout=40):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            response = call(root, command, {})
            records.append(dict(command=command, response=response))
            if response["ok"]:
                result = response["result"]
                if predicate(result):
                    return result
            elif response["error"]["code"] not in ("BOARD_NOT_READY", "CHOOSER_NOT_READY", "LEVEL_NOT_READY"):
                raise AssertionError(response)
            time.sleep(.1)
        raise AssertionError(f"Timed out: {command}")

    try:
        state = api("status")
        assert state["ready"] and not state["board"], "Main menu required."
        profiles = api("profiles")
        achievements = api("achievements")
        assert profiles["items"] and achievements
        api("profile.create", expected="INVALID_ARGUMENT", name="")
        api("profile.delete", expected="PROFILE_NOT_FOUND", guid="not-a-profile")
        api("profile.switch", expected="PROFILE_NOT_FOUND", guid="not-a-profile")
        assert api("profiles") == profiles
        checks.append("profile and achievement inspection; invalid mutations preserve profiles")
        for category in ("minigames", "puzzle", "survival"):
            modes = api("unlock.modes", mode=category, unlocked=False)["modes"]
            assert modes[category] is False
            modes = api("unlock.modes", mode=category, unlocked=True)["modes"]
            assert modes[category] is True
        unlocked = api("unlocks")
        assert unlocked["seeds"] and unlocked["levels"] and unlocked["almanac"]
        api("unlocks.reset")
        checks.append("all mode overrides and complete unlock status")
        backgrounds = api("debug.backgrounds")
        night = next(x for x in backgrounds if x["background"] == "Night")
        api("debug.start", index=night["index"])
        wait("chooser", lambda c: c["ready"] and not c["zombiePhase"])
        api("chooser.select", indices=[0], confirm=True)
        wait("chooser", lambda c: c["ready"] and c["zombiePhase"])
        api("chooser.select", indices=[0], confirm=True)
        state = wait("status", lambda s: s["debug"] and s["scene"] == "Playing")
        board = state["board"]
        for level in (1, 2, 3, 4, 5, 3):
            assert api("difficulty", board=board, value=level)["value"] == level
            assert api("difficulty")["value"] == level
        checks.append("Difficulty Mod displays and applies all five levels")
        collector = api("suncollector")["enabled"]
        assert api("suncollector", enabled=not collector)["enabled"] is not collector
        assert api("suncollector", enabled=collector)["enabled"] is collector
        checks.append("Sun Collector toggles actual mod state")
        catalog = api("catalog.plants")["registered"]
        silver = next(p for p in catalog if p["type"] == "Wallnut" and p["marker"] == "Marigold")
        plants = api("plant.place", registered=silver["index"], col=2, row=2)["plants"]
        plant = next(p for p in plants if p["mImitaterType"] == "Marigold")
        assert api("goldenbean.apply", board=board, id=plant["DataID"])["applied"]
        assert any(p["mSeedType"] == "Wallnut" and p["mImitaterType"] == "InstantCoffee" for p in api("plants")["items"])
        checks.append("golden coffee bean uses real Silver-nut upgrade")
        grave = api("gravestone.place", board=board, col=7, row=2)
        assert grave["mGridItemType"] == "Gravestone" and grave["mGridX"] == 7 and grave["mGridY"] == 2
        api("gravestone.place", expected="INVALID_PLACEMENT", board=board, col=7, row=2)
        api("gravestone.place", expected="STALE_BOARD", board="stale", col=6, row=2)
        checks.append("native gravestone placement rejects occupied and stale requests")
        api("mower.remove", board=board, all=True)
        assert not api("mowers")["items"]
        mower = api("mower.add", board=board, row=2, type="Roof")["items"]
        assert len(mower) == 1 and mower[0]["mMowerType"] == "Roof"
        mower = api("mower.set", board=board, id=mower[0]["DataID"], type="Pool")["items"]
        assert len(mower) == 1 and mower[0]["mMowerType"] == "Pool"
        all_mowers = api("mower.add", board=board, all=True)["items"]
        assert len(all_mowers) == 5
        assert len(api("mower.add", board=board, all=True)["items"]) == 5
        all_mowers = api("mower.set", board=board, all=True, type="SuperMower")["items"]
        assert len(all_mowers) == 5 and all(m["mMowerType"] == "SuperMower" for m in all_mowers)
        api("mower.remove", board=board, row=2)
        assert len(api("mowers")["items"]) == 4
        checks.append("individual and all mower operations; all four types; no duplicate rows")
        image = api("screenshot")
        records.append(dict(screenshot=image["path"]))
        api("controller", expected="INVALID_ARGUMENT", buttons=["invalid"])
        buttons = ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "Back", "Start", "LS", "RS", "DPadUp", "DPadDown", "DPadLeft", "DPadRight", "Guide"]
        api("controller", buttons=buttons, leftStick=dict(x=.75, y=-.5), rightStick=dict(x=-.5, y=.75), milliseconds=1000)
        value = wait("controller.status", lambda s: s.get("connected") and s["buttons"]["Guide"])
        assert all(value["buttons"].values()), value
        assert value["leftStick"]["x"] > .5 and value["rightStick"]["y"] > .5
        wait("controller.status", lambda s: not any(s.get("buttons", {}).values()))
        api("controller.reset")
        assert not api("controller.status")["connected"]
        checks.append("all Xbox 360 buttons, Guide, sticks and automatic release reach native device state")
        assert api("profiles") == profiles
        assert api("achievements") == achievements
        print("PASS:", "; ".join(checks), flush=True)
    finally:
        try:
            api("controller.reset")
            if collector is not None:
                api("suncollector", enabled=collector)
            state = api("status")
            if state["debug"]:
                if not state["board"]:
                    wait("status", lambda s: bool(s["board"]))
                api("pause", paused=False)
                api("level.leave")
            api("unlocks.reset")
        finally:
            # The real RS input can invoke the mod's persistent Toggle path.
            # Restore its pre-test file as well as its in-memory switch.
            if collector_bytes is not None:
                collector_file.write_bytes(collector_bytes)
            output.write_text(json.dumps(dict(checks=checks, records=records), indent=2) + "\n")


if __name__ == "__main__":
    run()
