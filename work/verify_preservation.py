from pathlib import Path
import re,json
root=Path.cwd();backup=root/'work/art_sample_backup';out=root/'Docs/ArtSample_2026-10-18/evidence'
results={}
for f in (backup/'Assets/Game/Scenes').glob('*.scene'):
 def colliders(path):
  text=path.read_text();blocks=re.split(r'(?=^--- !u!)',text,flags=re.M)
  return {re.search(r'^--- !u!\d+ &([^\n]+)',b).group(1):b for b in blocks if re.match(r'^--- !u!(61|60|58|70|64|68|50|49) ',b)}
 a=colliders(f);b=colliders(root/f.relative_to(backup));results[f.name]={'original_physics_blocks':len(a),'preserved':a==b}
# Compare all dialogue keys other than illustration references to pre-worktree baseline.
dialogue=[]
for f in (backup/'Assets/Game/Data/Dialogue/StoryV13').glob('*.asset'):
 strip=lambda p:re.sub(r'^  illustration:.*\n','',p.read_text(),flags=re.M)
 if strip(f)!=strip(root/f.relative_to(backup)):dialogue.append(f.name)
results['dialogue_changes_other_than_illustration']=dialogue
(out/'preservation.json').write_text(json.dumps(results,indent=2));print(json.dumps(results,indent=2))
