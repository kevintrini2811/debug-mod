#!/usr/bin/env python3
"""Exercise Debug Mod's API in an already running game, starting from its main menu.

This intentionally starts a temporary debug level and changes session state.
It never requests permanent profile writes. See src/documentation.md.
"""
import argparse
import json
from pathlib import Path
import time
import debug_api


def run(root, output):
    results = []

    def call(command, args=None, expected_error=None):
        args = args or {}
        response = debug_api.call(root, command, args)
        results.append(dict(command=command, args=args, response=response))
        print(command, response.get('error') or 'OK', flush=True)
        if expected_error:
            assert response.get('error', {}).get('code') == expected_error, response
        else:
            assert response.get('ok'), response
        return response.get('result')

    def wait(predicate, command='status'):
        deadline = time.monotonic()+30
        while time.monotonic() < deadline:
            response = debug_api.call(root, command, {})
            if response.get('ok') and predicate(response['result']):
                return response['result']
            time.sleep(.2)
        raise AssertionError('State timeout: '+str(response))

    try:
        wait(lambda state: state['ready'] and not state['board'])
        call('help')
        call('debug.start', {'index': 0})
        wait(lambda state: state['visible'], 'chooser')
        call('plant.place', dict(type='Peashooter', col=0, row=0), 'LEVEL_NOT_READY')
        call('chooser.select', dict(indices=[0, 1, 3], confirm=True))
        wait(lambda state: state['zombiePhase'], 'chooser')
        call('chooser.select', dict(indices=[0, 1], confirm=True))
        state = wait(lambda state: state['scene'] == 'Playing')
        plant = call('plant.place', dict(type='Peashooter', col=1, row=1))['plants'][0]
        assert isinstance(plant['DataID'], int)
        call('plant.place', dict(type='Lilypad', col=4, row=0), 'INVALID_PLACEMENT')
        call('plant.place', dict(type='Peashooter', col=99, row=0), 'INVALID_CELL')
        zombie = call('zombie.place', dict(type='Normal', col=7, row=1))['native']
        moved = call('plant.move', dict(board=state['board'], id=plant['DataID'], col=2, row=2))
        assert moved['mPlantCol'] == 2 and moved['mRow'] == 2
        moved_zombie = call('zombie.move', dict(board=state['board'], id=zombie['DataID'], col=7, row=3))['native']
        assert moved_zombie['mRow'] == 3
        call('plant.move', dict(board='old', id=plant['DataID'], col=2, row=2), 'STALE_BOARD')
        call('sun.set', dict(amount=1200))
        call('coins.level.set', dict(amount=25))
        call('sun.set', dict(amount=25, player=1), 'INVALID_PLAYER')
        board = call('board')
        assert board['sun'][0] == 1200 and board['levelCoins'] == 25
        call('coins.profile.set', dict(amount=board['profileCoins']))
        call('coins.profile.set', dict(amount=board['profileCoins'], persist=True), 'DEBUG_SAVE_PROTECTED')
        call('bank.set', dict(plants=['Peashooter', 'Sunflower', 'Wallnut']))
        call('debug.roster', dict(indices=[0, 1]))
        for command in ['plants', 'zombies', 'projectiles', 'waves', 'catalog.plants', 'catalog.zombies', 'fps']:
            call(command)
        almanac = call('almanac')
        if almanac['plants'] and almanac['zombies']:
            plant_entry, zombie_entry = almanac['plants'][0], almanac['zombies'][0]
            call('unlock.almanac', dict(kind='plant', owner=plant_entry['owner'], id=plant_entry['id'], unlocked=False))
            call('unlock.almanac', dict(kind='zombie', owner=zombie_entry['owner'], id=zombie_entry['id'], duck=zombie_entry['duck'], unlocked=False))
            call('unlock.plant', dict(owner=plant_entry['owner'], id=plant_entry['id'], unlocked=False))
            locked = call('almanac')
            plant_result = next(item for item in locked['plants'] if item['owner'] == plant_entry['owner'] and item['id'] == plant_entry['id'])
            assert plant_result['unlocked'] is False and plant_result['plantUnlocked'] is False
            zombie_result = next(item for item in locked['zombies'] if item['owner'] == zombie_entry['owner'] and item['id'] == zombie_entry['id'] and item['duck'] == zombie_entry['duck'])
            assert zombie_result['unlocked'] is False
            visible = call('almanac', dict(unlockedOnly=True))
            assert not any(item['owner'] == plant_entry['owner'] and item['id'] == plant_entry['id'] for item in visible['plants'])
        call('unlock.plant', dict(type='Peashooter', unlocked=False))
        call('unlock.almanac', dict(kind='zombie', type='Yeti', unlocked=False))
        assert not next(item for item in call('almanac')['native'] if item['kind'] == 'Zombie' and item['zombie'] == 'Yeti')['unlocked']
        call('unlocks.reset')
        assert call('speed', dict(index=2))['effective'] == 2
        call('speed', dict(index=0))
        settings = call('settings')
        volume = float(next(item['value'] for item in settings if item['name'] == 'MusicVolume'))
        call('settings.set', dict(name='MusicVolume', value=volume))
        call('settings.set', dict(name='MusicVolume', value=2), 'INVALID_ARGUMENT')
        screenshot = call('screenshot')
        assert screenshot['completed'] and Path(screenshot['path']).is_file()
        call('pause', dict(paused=True))
        call('pause', dict(paused=False))
        call('plant.remove', dict(board=state['board'], id=plant['DataID']))
        call('zombie.remove', dict(board=state['board'], id=zombie['DataID']))
        call('level.leave')
        wait(lambda state: state['ready'] and not state['board'])
        call('levels')
        call('settings')
        call('almanac')
        call('unknown.test', expected_error='UNKNOWN_COMMAND')
        print('Debug API smoke check passed.', flush=True)
    finally:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(results, indent=2)+'\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-dir', type=Path, default=debug_api.GAME)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1]/'validation/debug-runtime.json')
    options = parser.parse_args()
    run(options.game_dir/'UserData/DebugModApi', options.output)
