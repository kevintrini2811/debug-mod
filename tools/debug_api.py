#!/usr/bin/env python3
"""Send one JSON command to Debug Mod running in PvZ Replanted (Linux/Proton)."""
import argparse
import json
import os
from pathlib import Path
import sys
import time
import uuid

GAME = Path('/mnt/FastGaming1/SteamLibrary/steamapps/common/PVZ Replanted')


class ApiFailure(Exception):
    def __init__(self, code, message, **details):
        self.payload = dict(ok=False, error=dict(code=code, message=message, **details))
        super().__init__(message)


def atomic_json(path, value):
    temporary = path.with_suffix('.tmp')
    try:
        with temporary.open('x') as stream:
            json.dump(value, stream)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def heartbeat(root):
    try:
        status = json.loads((root / 'status.json').read_text())
    except (OSError, ValueError):
        raise ApiFailure('GAME_NOT_READY', 'The game is not running or the Debug API has not initialized yet.')
    if status.get('stopped') or time.time() * 1000 - status.get('updatedAt', 0) > 5000:
        raise ApiFailure('GAME_NOT_READY', 'No recent heartbeat. The game is still starting, has exited, or is unresponsive.')
    return status


def call(root, command, args, timeout=15, *, response_poll_seconds=.04):
    if type(response_poll_seconds) not in (int, float) or not .001 <= response_poll_seconds <= .04:
        raise ValueError('Response polling must be between 1 and 40 milliseconds.')
    status = heartbeat(root)
    request_id = uuid.uuid4().hex
    path = root / 'requests' / (request_id + '.json')
    response = root / 'responses' / path.name
    expiry = int((time.time() + timeout) * 1000)
    atomic_json(path, dict(protocol=1, session=status['session'], expiresAt=expiry, command=command, args=args))
    deadline = time.monotonic() + timeout
    try:
        while time.monotonic() < deadline:
            if response.exists():
                result = json.loads(response.read_text())
                if result.get('id') != request_id or result.get('session') != status['session']:
                    raise ApiFailure('INVALID_RESPONSE', 'The response does not belong to this request.')
                response.unlink(missing_ok=True)
                if command == 'screenshot' and result.get('ok'):
                    image = root / 'screenshots' / result['result']['file']
                    while time.monotonic() < deadline:
                        # Unity writes asynchronously after the current frame.
                        if image.exists():
                            data = image.read_bytes()
                            if data.startswith(b'\x89PNG\r\n\x1a\n') and data.endswith(b'IEND\xaeB`\x82'):
                                result['result'].update(completed=True, path=str(image))
                                return result
                        time.sleep(.05)
                    raise ApiFailure('SCREENSHOT_TIMEOUT', 'The PNG has not been fully written yet.', file=str(image))
                return result
            time.sleep(response_poll_seconds)
        raise ApiFailure('TIMEOUT', 'No response. The outcome is unknown; check the state before retrying a mutation.', requestId=request_id)
    finally:
        path.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', nargs='?', default='status')
    parser.add_argument('args', nargs='?', default='{}', help='JSON object')
    parser.add_argument('--game-dir', type=Path, default=GAME)
    parser.add_argument('--timeout', type=float, default=15)
    options = parser.parse_args()
    try:
        args = json.loads(options.args)
        if not isinstance(args, dict) or not 1 <= options.timeout <= 120:
            raise ApiFailure('INVALID_ARGUMENT', 'A JSON object and a timeout of 1..120 seconds are required.')
        result = call(options.game_dir / 'UserData/DebugModApi', options.command, args, options.timeout)
    except ApiFailure as error:
        result = error.payload
    except (ValueError, OSError) as error:
        result = dict(ok=False, error=dict(code='CLIENT_ERROR', message=str(error)))
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result.get('ok') else 1


if __name__ == '__main__':
    sys.exit(main())
