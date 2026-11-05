"""Offline tests for the host client's transport invariants. No game is started."""
import json
from pathlib import Path
import tempfile
import threading
import time
import unittest
from unittest.mock import patch
import debug_api


class TransportTests(unittest.TestCase):
    def test_missing_and_stale_game_report_not_ready(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for status in (None, dict(session='old', updatedAt=0), dict(stopped=True, updatedAt=int(time.time()*1000))):
                if status is not None:
                    (root/'status.json').write_text(json.dumps(status))
                with self.assertRaises(debug_api.ApiFailure) as raised:
                    debug_api.heartbeat(root)
                self.assertEqual(raised.exception.payload['error']['code'], 'GAME_NOT_READY')

    def test_request_roundtrip_and_response_cleanup(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root/'requests').mkdir()
            (root/'responses').mkdir()
            (root/'status.json').write_text(json.dumps(dict(session='current', updatedAt=int(time.time()*1000))))
            failures = []
            def responder():
                try:
                    deadline = time.monotonic()+2
                    while time.monotonic() < deadline:
                        paths = list((root/'requests').glob('*.json'))
                        if paths:
                            path = paths[0]
                            request = json.loads(path.read_text())
                            self.assertEqual(request['session'], 'current')
                            self.assertEqual(request['args'], {'amount': 50})
                            self.assertGreater(request['expiresAt'], int(time.time()*1000))
                            debug_api.atomic_json(root/'responses'/path.name, dict(id=path.stem, session='current', ok=True, result={'amount':50}))
                            return
                        time.sleep(.005)
                    raise AssertionError('No request')
                except Exception as error:
                    failures.append(error)
            thread = threading.Thread(target=responder)
            thread.start()
            result = debug_api.call(root, 'sun.set', {'amount':50}, timeout=2)
            thread.join()
            self.assertFalse(failures)
            self.assertEqual(result['result']['amount'], 50)
            self.assertFalse(list((root/'requests').iterdir()))
            self.assertFalse(list((root/'responses').iterdir()))

    def test_timeout_removes_unexecuted_request_and_reports_uncertainty(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            (root/'requests').mkdir(); (root/'responses').mkdir()
            (root/'status.json').write_text(json.dumps(dict(session='current', updatedAt=int(time.time()*1000))))
            with self.assertRaises(debug_api.ApiFailure) as raised:
                debug_api.call(root, 'sun.set', {'amount':50}, timeout=.01)
            self.assertEqual(raised.exception.payload['error']['code'], 'TIMEOUT')
            self.assertIn('requestId', raised.exception.payload['error'])
            self.assertFalse(list((root/'requests').iterdir()))


if __name__ == '__main__':
    unittest.main()
