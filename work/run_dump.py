import sys,subprocess
s=open('work/dump_rooms.cs').read().replace('__SCENES__',sys.argv[1]).replace('__OUT__',sys.argv[2])
open('/tmp/dump_run.cs','w').write(s)
print(subprocess.run(['python3','work/unity_bridge.py','code','/tmp/dump_run.cs'],capture_output=True,text=True).stdout[:600])
