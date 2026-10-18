from pathlib import Path
import shutil
base=Path('work/l01b_polish_backup')
for s in ['Assets/Game/Scripts/UI/BoundaryHandle.cs']:
 p=Path(s);d=base/p;d.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,d)
