"""Install the debug extraction and optional integrations without starting the game."""
from contextlib import ExitStack
from datetime import datetime
import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
GAME = Path("/mnt/FastGaming1/SteamLibrary/steamapps/common/PVZ Replanted")
PROJECTS = {
    "DebugMod": ("debug-mod", "DebugMod.csproj"),
    "FusionMod": ("fusion-mod", "FusionMod.csproj"),
    "PlantLibrary": ("plant-library", "PlantLibrary.csproj"),
    "ZombieLibrary": ("zombie-library", "ZombieLibrary.csproj"),
    "LawnGlove": ("glove-mod", "LawnGlove.csproj"),
    "SpeedMod": ("speed-mod", "SpeedMod.csproj"),
    "DifficultyMod": ("difficulty-mod", "DifficultyMod.csproj"),
    "SunCollector": ("sun-collector-mod", "SunCollector.csproj"),
}


ALL_PROJECTS = PROJECTS.copy()


def digest(data):
    return hashlib.sha256(data).hexdigest() if data is not None else None


def read(path):
    return path.read_bytes() if path.exists() else None


def atomic(path, data):
    stat = path.stat() if path.exists() else path.parent.stat()
    descriptor, name = tempfile.mkstemp(dir=path.parent, prefix=".DebugMod-")
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(name, 0o644)
        os.chown(name, stat.st_uid, stat.st_gid)
        os.replace(name, path)
        directory = os.open(path.parent, os.O_DIRECTORY)
        try:
            os.fsync(directory)
        finally:
            os.close(directory)
    finally:
        if os.path.exists(name):
            os.unlink(name)


def install():
    writes, versions, payloads = {}, {}, {}
    for assembly, (directory, project) in PROJECTS.items():
        base = ROOT.parent / directory
        versions[assembly] = ET.parse(base / project).findtext(".//Version")
        payload = (base / "bin/Release/net6.0" / f"{assembly}.dll").read_bytes()
        if payload[:2] != b"MZ":
            raise ValueError(f"Not a PE assembly: {assembly}")
        payloads[f"{assembly}.dll"] = payload
        writes[GAME / "Mods" / f"{assembly}.dll"] = payload

    sums = {name: digest(data) for name, data in payloads.items()}
    for assembly, (directory, _) in ALL_PROJECTS.items():
        path = ROOT.parent / directory / "SHA256SUMS"
        lines = []
        if path.exists():
            for line in path.read_text().splitlines():
                parts = line.split()
                if len(parts) == 2:
                    name = parts[1]
                    lines.append(f"{sums.get(name, parts[0])}  {name}")
        if assembly in PROJECTS and not any(line.split()[-1] == f"{assembly}.dll" for line in lines):
            lines.append(f"{sums[assembly + '.dll']}  {assembly}.dll")
        writes[path] = ("\n".join(lines) + "\n").encode()

    originals = {path: read(path) for path in writes}
    backup = ROOT / "backups" / datetime.now().strftime("install-%Y%m%d-%H%M%S-%f")
    backup.mkdir(parents=True)
    manifest = {"game_started": False, "tests_run": False, "versions": versions, "files": []}
    for index, (path, data) in enumerate(writes.items()):
        saved = f"{index}-{path.name}"
        if originals[path] is not None:
            (backup / saved).write_bytes(originals[path])
        manifest["files"].append({"path": str(path), "backup": saved if originals[path] is not None else None,
                                  "before": digest(originals[path]), "after": digest(data)})
    (backup / "installation.json").write_text(json.dumps(manifest, indent=2) + "\n")

    changed = []
    try:
        for path, data in writes.items():
            if read(path) != originals[path]:
                raise ValueError(f"File changed concurrently: {path}")
            changed.append(path)
            atomic(path, data)
        for path, data in writes.items():
            if read(path) != data:
                raise ValueError(f"Installed hash mismatch: {path}")
    except BaseException:
        for path in reversed(changed):
            if originals[path] is None:
                path.unlink(missing_ok=True)
            else:
                atomic(path, originals[path])
        raise
    print(f"Installed {len(payloads)} assemblies. Backup: {backup}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", choices=PROJECTS, nargs="+", help="Install only these assemblies.")
    options = parser.parse_args()
    if options.only:
        PROJECTS = {name: PROJECTS[name] for name in options.only}
    with ExitStack() as stack:
        for assembly in sorted(ALL_PROJECTS):
            lock = stack.enter_context((GAME / "Mods" / f".{assembly}.release.lock").open("a"))
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        install()
