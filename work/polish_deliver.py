from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import hashlib,json,re,shutil
r=Path.cwd();b=r/'work/l01b_polish_backup';o=r/'Docs/ArtSample_2026-10-18/Polish';e=o/'evidence'
# Display evidence only; do not alter source art.
a=Image.open(r/'Docs/ArtSample_2026-10-18/screenshots/L01B_final_1920x1080.png').convert('RGB')
z=Image.open(o/'screenshots/L01B_mixed_1920x1080.png').convert('RGB')
box=(95,570,705,782);canvas=Image.new('RGB',(1830,365),(29,30,30));d=ImageDraw.Draw(canvas)
for n,(im,label) in enumerate([(a,'BEFORE: 100%'),(z,'AFTER: 130%')]):
 im=im.crop(box).resize((915,318));canvas.paste(im,(n*915,40));d.text((n*915+12,12),label,fill='white')
canvas.save(o/'人物比例_前后对比.jpg',quality=95)
# Preserve/recover the save files owned by this review.
live=Path('/Users/edwin/Library/Application Support/DefaultCompany/BorrowTomorrow');snap=b/'save'
expected={str(p.relative_to(snap)):hashlib.sha256(p.read_bytes()).hexdigest() for p in snap.rglob('*') if p.is_file()}
for p in list(live.rglob('*')):
 if p.is_file() and str(p.relative_to(live)) not in expected:
  archive=e/'review_generated_saves'/p.relative_to(live);archive.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,archive);p.unlink()
shutil.copytree(snap,live,dirs_exist_ok=True)
actual={str(p.relative_to(live)):hashlib.sha256(p.read_bytes()).hexdigest() for p in live.rglob('*') if p.is_file()}
(e/'save_restoration.json').write_text(json.dumps({'exactly_restored':expected==actual,'files':len(expected)},indent=2))
# Compare collider bodies and the local transforms of physical objects to this turn's baseline.
def physical(p):
 s=p.read_text();blocks=re.split(r'(?m)^--- !u!',s)[1:];out={};gos=set();transforms={}
 for block in blocks:
  m=re.match(r'(\d+) &([\d-]+)',block);kind=int(m[1]);key=m[2]
  g=re.search(r'm_GameObject: \{fileID: ([\d-]+)\}',block)
  if kind in (50,58,60,61,62,68,70):out[(kind,key)]=block;gos.add(g[1])
  if kind==4 and g:transforms[g[1]]=block
 for go in gos:
  block=transforms.get(go,'');out[('transform',go)]='\n'.join(re.findall(r'(?m)^  m_Local(?:Position|Rotation|Scale):.*$',block))
 return out
checks={}
for s in ['Assets/Game/Scenes/L01_Courtyard.scene','Assets/Game/Prefabs/Player_Graybox.prefab']:
 old=physical(b/s);new=physical(r/s);checks[s]={'physical_blocks_unchanged':old==new,'blocks':len(old)}
for s in ['Assets/Game/Scripts/Level/CameraRig2D.cs','Assets/Game/Scripts/UI/BoundaryHandle.cs']:
 checks[s]={'unchanged':(b/s).read_bytes()==(r/s).read_bytes()}
start=json.loads((b/'asset_hashes.json').read_text());changed=[s for s,h in start.items() if not (r/s).exists() or hashlib.sha256((r/s).read_bytes()).hexdigest()!=h]
checks['changed_existing_assets']=changed
(e/'preservation.json').write_text(json.dumps(checks,indent=2,ensure_ascii=False))
print(json.dumps(checks,indent=2,ensure_ascii=False))
(o/'本轮调整记录.txt').write_text('''借一点明天 L01B 背景与人物比例调整\n2026-10-18\n\n本轮采用用户确认的简单方案：保持原镜头，人物等比放大30%。已撤销试作的近景跟随相机与边界把手偏移改动。没有改变人物根节点、移动速度、跳跃参数、物理胶囊及场景碰撞。\n\n人物比例\nPlayer_Graybox.prefab 的 Visual 局部缩放为 (1.3,1.3,1)，所有动作共用这一个比例，脚底 pivot 不动。Idle00 有效画高从493/300=1.643米变为2.136米；当前1080p镜头中约101像素变为131像素。这里只调整视觉尺寸，碰撞胶囊仍为0.6×1.6米；其他关卡窄顶和低通道的画面贴合需要后续随关卡复查。\n人物比例_前后对比.jpg 为同一相机、同一屏幕区域的实际运行截图裁切，左右显示倍率一致。原始图保存在 screenshots。\n\nL01B 背景与空间\n1. 两年代共用 BG_L01B_CommonArchitecture_v2.png 的同一建筑骨架、相同位置和等比缩放；过去原色，现在轻微青绿调色。屋檐、墙线和高窗在切换时不再发生几何漂移。当前不是两套独立重画的风化差分。\n2. 清除了背景里没有对应玩法的地面敞门；缩小并关闭原先过大的高窗，保留阁楼位置线索。高窗与实际出口位于同一右侧竖向建筑区域。\n3. 复用现有 NearWall 砖墙图，为实际出口台补可见支撑；未新增碰撞。\n4. 1994幼树改为真正透明、无白色夹底的配套幼树，整图等比显示。幼树与成年树根部中心均约在世界X=58.41。年代机关判断锚点仍是原X=58，未移动谜题阈值。\n5. 身高刻线与儿童画调整位置以免互相覆盖。刚纠正的完整成年树保持原图、原比例及原有三段支撑。\n\n资源与制作方式\n内置 ImageGen 定向修改现有背景、重绘配套透明幼树；未调用API脚本。\n背景：Assets/Game/Art/SampleV1/01_背景_Backgrounds/BG_L01B_CommonArchitecture_v2.png\n幼树：Assets/Game/Art/SampleV1/Tree/L01B_Sapling_v2.png\n完整提示词：evidence/background_prompt.txt、evidence/sapling_prompt.txt。\n旧背景、旧幼树及原交付素材均保留。\n\n实际运行验证\n- 放大后的角色完成地面行走、木梯攀爬、恒定台站稳、正常安全拖动至标准解、跳上枝条、沿枝走到出口；15项检查通过。\n- 全过去、全现在、混合状态分别保存1920×1080、1280×720、1440×1080截图；另保存梯顶、枝条起点/中点/末端和出口截图。\n- 本轮为L01B路线隔离复查，关闭了房间自动对白；未宣称全游戏重新通关或所有动画均最终验收。\n- 控制台仅复现原有两条Metal memoryless depth错误，无C#编译错误。\n- 相机、边界把手代码与本轮开始时逐字一致；人物与L01场景物理块及其变换一致，见evidence/preservation.json。\n- 测试存档已恢复；编辑器退出运行并回到保存的Bootstrap，原工作区修改保留。\n\n仍待后续\n4:3画面顶部仍有背景覆盖不足（约118像素），本轮按用户要求不扩展镜头方案，不能列为通过。人物其他关卡的低顶通道和近景贴合、三个动作的连续观感、阁楼高窗的正式剧情镜头移动仍需后续检查。2024背景可在共同建筑锚点上补局部风化差分，避免重新生成两张独立构图。\n\n再次应用\n编辑器菜单：BorrowTomorrow → 完善 L01B 背景与人物比例。\n原样板接入入口也会保留这次人物比例，并应用已有的新背景与幼树。\n''',encoding='utf-8')
p=r/'Docs/ArtSample_2026-10-18/样板接入记录.txt';s=p.read_text();s+='\n后续更新：已完成人物等比放大30%、共用建筑骨架、移除误导地面门、缩小高窗、配套幼树与出口支撑。本轮保持原相机，详见 Polish/本轮调整记录.txt 和 Polish/人物比例_前后对比.jpg。原文中背景年代配准与树根对应的待办已由此次更新替代；4:3顶部覆盖问题仍待后续。\n';p.write_text(s)
