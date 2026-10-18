import sys,glob
from PIL import Image,ImageDraw
out=sys.argv[1];cols=int(sys.argv[2]);files=sys.argv[3:]
w=960;ims=[]
for f in files:
    im=Image.open(f).convert('RGB');h=int(im.height*w/im.width);im=im.resize((w,h));d=ImageDraw.Draw(im);d.rectangle([0,0,300,22],fill=(0,0,0));d.text((4,4),f.split('/')[-1],fill=(255,255,0));ims.append(im)
rows=(len(ims)+cols-1)//cols;H=max(i.height for i in ims)
sheet=Image.new('RGB',(w*cols,H*rows))
for k,im in enumerate(ims):sheet.paste(im,((k%cols)*w,(k//cols)*H))
sheet.save(out,quality=85)
