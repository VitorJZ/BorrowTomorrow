import sys,subprocess
d,w,h,only=sys.argv[1],sys.argv[2],sys.argv[3],(sys.argv[4] if len(sys.argv)>4 else '')
s=open('work/shot_rooms.cs').read().replace('__DIR__',d).replace('__W__',w).replace('__H__',h).replace('__ONLY__',only)
open('/tmp/shot_run.cs','w').write(s)
print(subprocess.run(['python3','work/unity_bridge.py','code','/tmp/shot_run.cs'],capture_output=True,text=True).stdout[:2000])
