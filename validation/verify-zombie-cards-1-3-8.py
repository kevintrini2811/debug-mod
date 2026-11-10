import sys,time,json,shutil
from pathlib import Path
sys.path.insert(0,'/home/kevin/modding/pvzreplanted/debug-mod/tools')
from debug_api import call,GAME
root=GAME/'UserData/DebugModApi';label=sys.argv[1]
out=Path('/home/kevin/modding/pvzreplanted/.test-runs/zombie-card-review-20261106');events=[]
def api(cmd,**args):
 r=call(root,cmd,args);events.append(dict(command=cmd,args=args,response=r));assert r['ok'],r;return r['result']
def wait(cmd,pred,seconds=45):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  try:r=api(cmd)
  except AssertionError as e:
   if cmd=='chooser' and any(code in str(e) for code in ('CHOOSER_NOT_READY','BOARD_NOT_READY')):
    time.sleep(.2);continue
   raise
  if pred(r):return r
  time.sleep(.2)
 raise AssertionError((cmd,r))
def snap(name):
 time.sleep(.5)
 ui=api('debug.ui');(out/(label+'-'+name+'-ui.json')).write_text(json.dumps(ui,indent=2)+'\n')
 if label!='before':
  packets=[v for v in ui if (v.get('sprite') or '').startswith('DebugZombiePacket.Dynamic')]
  assert len(packets)==(8 if name=='selected' else 4),packets
  assert all(v['enabled'] and v['active'] and v['spriteRect']['width']==128 and v['spriteRect']['height']==176 for v in packets),packets
 p=api('screenshot')['path'];shutil.copy2(p,out/(label+'-'+name+'.png'))
try:
 chooser=wait('chooser',lambda x:x['ready'])
 if not chooser['zombiePhase']:
  api('chooser.select',indices=[0]);wait('chooser',lambda x:x['ready'] and x['zombiePhase'])
 catalog=api('catalog.zombies');choices=catalog['debug']
 eyes=[x for x in choices if 'Augenzombie' in x['name'] or 'Augenzombie' in x['name'].replace('-','') or 'Eyes Zombie' in x['name']]
 print('EYES',eyes,flush=True);assert len(eyes)==3,eyes
 selected=[0]+[x['index'] for x in eyes]
 if label!='before':selected += [next(x['index'] for x in choices if x['type']=='PeaHead' and 'Hypno' not in x['name']),next(x['index'] for x in choices if 'Hypno' in x['name'])]
 snap('chooser')
 api('chooser.select',indices=selected,confirm=False);snap('selected')
 api('chooser.select',indices=selected,confirm=True)
 wait('status',lambda x:x['scene']=='Playing');snap('bar')
 print('PASS chosen native Normal and all three Eyes variants; screenshots captured.',flush=True)
finally:(out/(label+'-events.json')).write_text(json.dumps(events,indent=2)+'\n')
