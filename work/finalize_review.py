from pathlib import Path
from PIL import Image,ImageDraw
import json,re,hashlib
root=Path.cwd();out=root/'Docs/ArtSample_2026-10-18';shots=out/'screenshots'
# Compare tree correction without editing either game asset.
a=Image.open(out/'evidence/rejected_mesh_version/L01B_mixed.png').convert('RGB');b=Image.open(shots/'L01B_final_1920x1080.png').convert('RGB')
for im in [a,b]:im.thumbnail((960,540))
sheet=Image.new('RGB',(1920,580),(32,32,32));sheet.paste(a,(0,30));sheet.paste(b,(960,30));d=ImageDraw.Draw(sheet);d.text((12,8),'REJECTED: deformed mesh and detached canopy',fill='white');d.text((972,8),'CORRECTED: intact tree with uniform scale',fill='white');sheet.save(out/'tree_before_after.jpg')
# Actual event-controller motion samples, not manual progress previews.
for name in ['DLG_L02_UNIFORM_01','DLG_L04_BOX_01','DLG_EPI_07']:
 ps=sorted(shots.glob(name+'_frame_*.png'));frames=[]
 for p in ps:
  im=Image.open(p).convert('RGB');im.thumbnail((960,540));frames.append(im)
 if frames:frames[0].save(out/(name+'_motion.gif'),save_all=True,append_images=frames[1:],duration=160,loop=0)
# Contact sheet for three output aspect ratios.
sheet=Image.new('RGB',(1800,1440),(32,32,32));d=ImageDraw.Draw(sheet)
for j,name in enumerate(['L01B','ACT_L02_CUFF','ACT_L04_BOARD','ACT_EPI_SMOOTH']):
 for i,size in enumerate(['1920x1080','1280x720','1440x1080']):
  p=shots/(name+'_final_'+size+'.png')
  if not p.exists():continue
  im=Image.open(p).convert('RGB');im.thumbnail((600,338));sheet.paste(im,(i*600+(600-im.width)//2,j*360));d.text((i*600+5,j*360+340),name+' '+size,fill='white')
sheet.save(out/'sample_contact_sheet.jpg')
(out/'evidence/final_png_sizes.json').write_text(json.dumps({p.name:Image.open(p).size for p in shots.glob('*_final_*.png')},indent=2))
