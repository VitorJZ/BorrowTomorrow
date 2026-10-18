from pathlib import Path
import json, shutil, zipfile
import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt, resample_poly
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parents[1]
ROOT = OUT.parents[1]
SR = 48000
rng = np.random.default_rng(20261018)

def read(rel):
    x, sr = sf.read(ROOT / rel, always_2d=True)
    x = resample_poly(x.mean(axis=1), SR, sr)
    return x / max(abs(x).max(), 1e-8)

def band(x, lo, hi):
    return sosfilt(butter(3, [lo, hi], btype='bandpass', fs=SR, output='sos'), x)

def fade(x, attack=.012, release=.06):
    x = x.copy()
    a, b = min(len(x)//2, int(attack*SR)), min(len(x)//2, int(release*SR))
    if a: x[:a] *= np.sin(np.linspace(0,np.pi/2,a))**2
    if b: x[-b:] *= np.cos(np.linspace(0,np.pi/2,b))**2
    return x

def add(dst, src, at, gain=1):
    start=int(at*SR); n=min(len(src),len(dst)-start)
    if n>0: dst[start:start+n] += gain*src[:n]

metal=read('Assets/AudioLib/ed963ccc1ec9bdfa/bc7ea2b908450297/ACTION_Open_Metal_Hatch_01_mono.wav')
wood=read('Assets/AudioLib/504becfb9d470769/6454c4fb1ecce48f/Footsteps_Ladder_Wood_Old_Squeak_Creak-003.wav')
cloth=read('Assets/AudioLib/ed963ccc1ec9bdfa/e87b03a7443b8efa/FABRIC_Flap_01_mono.wav')
clips=[]; waves=[]
def save(name, x, peak, description):
    x=fade(x,.005,.08)
    x *= peak/max(abs(x).max(),1e-8)
    stereo=np.column_stack([x,x*.98])
    sf.write(OUT/'Audio'/f'{name}.wav',stereo,SR,subtype='PCM_24')
    clips.append(dict(file=f'Audio/{name}.wav',seconds=round(len(x)/SR,3),loop=False,description=description,peak=float(abs(stereo).max())))
    waves.append(stereo)

# Friction from cloth plus filtered granular noise, distinct from a crank loop.
x=np.zeros(int(1.5*SR)); t=np.arange(len(x))/SR
friction=band(rng.normal(0,.018,len(x)),450,3800)*(np.sin(np.pi*t/1.5)**2)
x+=friction
for at,g in [(0,.20),(.45,.16),(.9,.10)]: add(x,fade(band(cloth,300,4200)),at,g)
save('sfx_rack_rope_pull',x,.30,'拉绳摩擦：一次下拉，持续1.5秒')
x=np.zeros(int(1.8*SR))
for at,g in [(0,.32),(.48,.22),(.94,.12)]: add(x,fade(band(wood,120,2300)),at,g)
add(x,fade(band(metal,180,1400)),.25,.08)
save('sfx_rack_raise_creak',x,.36,'木架升起：木料与铰链受力')
x=np.zeros(int(.65*SR)); t=np.arange(int(.4*SR))/SR
add(x,fade(band(metal,500,5500)[:int(.08*SR)],.001,.025),.015,.35)
add(x,.14*np.sin(2*np.pi*720*t)*np.exp(-t/.038),.025)
add(x,.10*np.sin(2*np.pi*145*t)*np.exp(-t/.07),.025)
save('sfx_rack_latch_lock',x,.48,'锁扣入位：短促清楚的咔嗒')
x=np.zeros(int(.95*SR))
for at,g in [(0,.2),(.12,.11),(.29,.07)]:add(x,fade(band(metal,230,2600)[:int(.13*SR)],.002,.04),at,g)
add(x,fade(band(wood,150,1800)),.08,.13)
add(x,fade(band(cloth,500,3800)),.2,.10)
save('sfx_rack_broken_rattle',x,.32,'现在态失败：松脱铁件、断绳与木架轻响')
silence=np.zeros((int(.45*SR),2))
demo=np.concatenate([silence]+[item for w in waves for item in (w,silence)])
sf.write(OUT/'Audio'/'DryingRack_demo.wav',demo,SR,subtype='PCM_24')

# The delivered PNGs are copied unchanged. Thumbnails below are review artifacts.
assets=[]
for p in sorted((OUT/'Art').glob('*.png')):
    im=Image.open(p).convert('RGBA'); a=np.asarray(im.getchannel('A'))
    bbox=im.getchannel('A').point(lambda v:255 if v>20 else 0).getbbox()
    assets.append(dict(file='Art/'+p.name,size=list(im.size),alphaRange=[int(a.min()),int(a.max())],visibleBounds=list(bbox),status='generated; not integrated'))
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',23)
canvas=Image.new('RGB',(1440,1320),'#e8e1d3');draw=ImageDraw.Draw(canvas)
label_map={'PROP_RackBrace_1994.png':'支撑臂','PROP_RackLatch_Closed.png':'锁扣：锁定','PROP_RackLatch_Open.png':'锁扣：打开','PROP_RackLeaf_1994.png':'过去：完整木架','PROP_RackLeaf_2024.png':'现在：损坏木架','PROP_RackMount_1994.png':'过去：固定支座','PROP_RackMount_2024.png':'现在：损坏支座','PROP_Osmanthus_Natural_2024.png':'自然桂花树（布局待适配）'}
labels=[label_map[Path(a['file']).name] for a in assets]
for i,(a,label) in enumerate(zip(assets,labels)):
    col,row=i%4,i//4; x,y=col*360,row*600
    draw.rounded_rectangle((x+8,y+8,x+352,y+555),12,fill='#f8f3e9')
    im=Image.open(OUT/a['file']).convert('RGBA').crop(tuple(a['visibleBounds']))
    im.thumbnail((320,460),Image.Resampling.LANCZOS)
    canvas.paste(im,(x+(360-im.width)//2,y+35+(460-im.height)//2),im)
    draw.text((x+18,y+510),label,font=font,fill='#403c33')
draw.text((24,1220),'L01B 晾晒架资产 | 7张机关分层 + 1张自然树 | 不含人物动画 | 尚未接入游戏',font=font,fill='#403c33')
canvas.save(OUT/'contact_sheet.jpg',quality=92)
manifest={'version':1,'imageMethod':'built-in imagegen; generated alpha preserved; originals copied unchanged','characterAnimation':'cancelled by user; none delivered','images':assets,'audioMethod':'local sound design from existing repository audio and synthesis; no external service','audioSources':['ACTION_Open_Metal_Hatch_01_mono.wav','Footsteps_Ladder_Wood_Old_Squeak_Creak-003.wav','FABRIC_Flap_01_mono.wav'],'audio':clips,'integration':'Not installed. New natural tree requires new level placement and collision fitting; do not reuse old tree collision.','pivotsTopLeftNormalized':{'PROP_RackMount_1994.png':{'hinge':[.554,.762],'pulley':[.563,.185]},'PROP_RackMount_2024.png':{'hinge':[.565,.770]},'PROP_RackLeaf_1994.png':{'hinge':[.086,.511],'ropeEye':[.926,.434]},'PROP_RackLeaf_2024.png':{'hinge':[.075,.488]},'PROP_RackBrace_1994.png':{'lowerEye':[.103,.828],'upperEye':[.890,.180]},'PROP_RackLatch_Open.png':{'pivot':[.467,.615]},'PROP_RackLatch_Closed.png':{'pivot':[.443,.615]}}}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
cards=''.join(f'<figure><img src="{a["file"]}"><figcaption>{label}<small>{Path(a["file"]).name}</small></figcaption></figure>' for a,label in zip(assets,labels))
players=''.join(f'<label>{c["description"]}<audio controls preload="metadata" src="{c["file"]}"></audio></label>' for c in clips)
html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>L01B 晾晒架素材预览</title><style>body{background:#e9e2d5;color:#342f29;font:16px/1.65 system-ui;margin:32px auto;max-width:1180px;padding:0 20px}h1{font-size:28px}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(250px,1fr));gap:18px}figure{background:#faf7ef;margin:0;padding:15px;border-radius:12px}figure img{width:100%;height:300px;object-fit:contain;background:repeating-conic-gradient(#ddd5c8 0 25%,#f3eee4 0 50%) 0/20px 20px}small{display:block;font-size:11px;word-break:break-all}audio{display:block;width:100%;margin:10px 0}label{display:block;margin:20px 0}p{max-width:900px}.note{background:#fff8df;padding:14px;border-radius:10px}a{color:#735336}</style><h1>桂花树关 · 折叠晾晒架资产</h1><p>7 张透明机关分层、1 张自然桂花树候选、4 条机关音效。人物拉绳动画已按要求取消。</p><p class="note">素材尚未接入游戏。自然树已改用向上分叉的真实树形，旧树碰撞不可直接复用。建议路线：晾晒架 → 主干分叉 → 短粗枝 → 固定窗台；具体位置、跳距需接入时实测。</p><section>'''+cards+'''</section><h2>机关音效</h2>'''+players+'''<h2>连续试听</h2><audio controls src="Audio/DryingRack_demo.wav"></audio><p>顺序：拉绳 → 木架升起 → 锁扣扣住 → 现在态失败。48kHz 双声道 PCM24 WAV；为现有素材加工与本地声音设计。</p><p><a href="manifest.json">尺寸、轴心与音频参数</a> · <a href="Source/prompts.json">生成提示词</a> · <a href="README.md">接入说明</a></p></html>'''
(OUT/'preview.html').write_text(html,encoding='utf-8')
assert len(assets)==8 and len(clips)==4
for a in assets: assert a['alphaRange'][0]==0 and a['alphaRange'][1]>240
for c in clips:
    info=sf.info(OUT/c['file']); assert info.samplerate==SR and info.channels==2 and info.subtype=='PCM_24'
print(json.dumps({'images':len(assets),'sounds':len(clips),'demoSeconds':len(demo)/SR,'output':str(OUT)},ensure_ascii=False))

