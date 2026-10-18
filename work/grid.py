import sys
from PIL import Image,ImageDraw
src,out=sys.argv[1],sys.argv[2]
im=Image.open(src).convert('RGB');w=1280;im=im.resize((w,int(im.height*w/im.width)));d=ImageDraw.Draw(im)
for i in range(1,10):
    y=int(im.height*i/10);d.line([0,y,w,y],fill=(255,0,255),width=1);d.text((3,y+1),f".{i}0",fill=(255,255,0))
    x=int(w*i/10);d.line([x,0,x,im.height],fill=(0,200,255),width=1);d.text((x+2,3),f".{i}0",fill=(0,255,255))
im.save(out)
