from pathlib import Path
import shutil,json,hashlib,subprocess
r=Path.cwd();backup=r/'work/art_sample_backup';backup.mkdir(exist_ok=True)
for base in ['Assets/Game/Scripts','Assets/Game/Scenes','Assets/Game/Data/Narrative','Assets/Game/Data/Dialogue/StoryV13','Assets/Game/Prefabs']:
 for p in (r/base).rglob('*'):
  if p.is_file():q=backup/p.relative_to(r);q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q)
(backup/'git_status.txt').write_bytes(subprocess.check_output(['git','status','--porcelain=v1']))
# Import only sample assets, not all unused delivery files.
src=r/'ArtDelivery_2026-10-18/交付素材';dest=r/'Assets/Game/Art/SampleV1'
for p in src.rglob('*.png'):
 rel=p.relative_to(src);s=str(rel)
 if ('L01B' in s or s.startswith('03_') or s.startswith('02_') or '/Hero/' in s or 'UI_Button_Wood' in s or 'UI_Paper_9Slice' in s):
  q=dest/rel;q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q)
# hashes to verify every collider/transform irrelevant scope later
(backup/'source_hashes.json').write_text(json.dumps({str(p.relative_to(r)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (r/'Assets/Game').rglob('*') if p.is_file() and p.suffix in ['.scene','.cs','.prefab','.asset']},indent=2))
print('Imported',len(list(dest.rglob('*.png'))),'sample assets; backup',backup)
