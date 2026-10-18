const fs=require('fs'),path=require('path');
const sharp=require('/Users/edwin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root='/Users/edwin/Documents/Codex/2026-10-18/users-edwin-documents-projects-unity-borrowtomorrow/outputs/BorrowTomorrow_ArtPack';
const art=path.resolve('Assets/Game/Art');
const dest=path.join(root,'交付素材');
const rows=[],placements=[];
const mkdir=p=>fs.mkdirSync(p,{recursive:true});
const output=(folder,name)=>{mkdir(path.join(dest,folder));return path.join(dest,folder,name);};
const clear=(w,h)=>sharp({create:{width:w,height:h,channels:4,background:{r:0,g:0,b:0,alpha:0}}});
async function clean(input){const {data,info}=await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject:true});for(let i=0;i<data.length;i+=4)if(data[i+3]<=5){data.fill(0,i,i+4);}return {data,info};}
async function buf(input){const {data,info}=await clean(input);return sharp(data,{raw:info}).png().toBuffer();}
function bbox(d,w,h,th=32){let x0=w,y0=h,x1=0,y1=0;for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(d[(y*w+x)*4+3]>th){x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}return {left:x0,top:y0,width:x1-x0+1,height:y1-y0+1};}
async function record(file,source,extra={}){const m=await sharp(file).metadata();let amin=255,amax=255,frac=0;if(m.hasAlpha){const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});let z=0;amin=255;amax=0;for(let i=3;i<data.length;i+=4){amin=Math.min(amin,data[i]);amax=Math.max(amax,data[i]);if(!data[i])z++;}frac=z/(info.width*info.height);}let native=null;try{const n=await sharp(source).metadata();native=[n.width,n.height];}catch{}rows.push({file:path.relative(dest,file),source:path.relative(root,source),native,delivery:[m.width,m.height],alpha_min:amin,alpha_max:amax,transparent_fraction:+frac.toFixed(5),...extra});}
async function save(id,folder,name,w,h,extra={}){const input=path.join(root,id+'.png'),f=output(folder,name+'_'+w+'x'+h+'.png');const b=await buf(input);await sharp(b).resize(w,h,{fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).png().toFile(f);await record(f,input,extra);return f;}
async function layer(id,folder,name,W,H,box){const input=path.join(root,id+'.png');const {data,info}=await clean(input),bb=bbox(data,info.width,info.height);let ww,hh,left,top;if(box){ww=box[2];hh=box[3];left=box[0];top=box[1];}else{ww=Math.round(bb.width*W/info.width);hh=Math.round(bb.height*H/info.height);left=Math.round(bb.left*W/info.width);top=Math.round(bb.top*H/info.height);}const content=await sharp(data,{raw:info}).extract(bb).resize(ww,hh,{fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).png().toBuffer();const f=output(folder,name+'_'+W+'x'+H+'.png');await clear(W,H).composite([{input:content,left,top}]).png().toFile(f);await record(f,input,{placement_top_left:[left,top,ww,hh]});placements.push({file:path.relative(dest,f),original_bbox:bb,canvas:[W,H],placed:[left,top,ww,hh]});return f;}
async function composite(folder,name,w,h,layers){const f=output(folder,name+'_'+w+'x'+h+'.png');await clear(w,h).composite(layers.map(input=>({input}))).png().toFile(f);await record(f,layers[0],{type:'assembled_preview_or_state'});return f;}
(async()=>{
 const bg='01_背景_Backgrounds';for(const room of ['L00_Gate','L01A_Canal','L01B_Tree','L02_Yard','L02_Room','L03_Corridor','L04_Attic'])for(const era of ['1994','2024'])await save('M06_BG_'+room+'_'+era,bg,'BG_'+room+'_'+era,2560,1440,{review:'需游戏碰撞与建筑配准复核'});
 const a='03_动作分层_Actions/L02_Cuff',W=2304,H=1728;
 const table=await save('M01_Table_Base',a,'ACT_L02_Cuff_00_Table',W,H);
 const red=await layer('M01_Fabric_Red',a,'ACT_L02_Cuff_01_FabricRed',W,H,[40,820,650,650]);
 const blue=await layer('M01_Fabric_Blue',a,'ACT_L02_Cuff_02_FabricBlue',W,H,[70,1230,740,450]);
 const before=await layer('M01_Sleeve_Before',a,'ACT_L02_Cuff_03_SleeveBefore',W,H,[600,100,1250,1300]);
 const after=await layer('M01_Sleeve_After_v2',a,'ACT_L02_Cuff_04_SleeveAfter',W,H,[600,100,1250,1300]);
 const hands=[];for(let i=0;i<4;i++)hands.push(await layer('M01_Hand_0'+i+(i?'_v2':''),a,'ACT_L02_Cuff_Hand_'+String(i).padStart(2,'0'),W,H,[1320,820-i*12,984,730]));
 await composite('02_剧情插画_Illustrations','ILL_L02_Cuff_Before',W,H,[table,red,blue,before]);
 await composite('02_剧情插画_Illustrations','ILL_L02_Cuff_After',W,H,[table,red,blue,after,hands[3]]);
 const b='03_动作分层_Actions/L04_Niche';
 const niche=await save('M02_Niche_Base',b,'ACT_L04_Niche_00_WallWindow',W,H);
 const box=await layer('M02_Box_Only',b,'ACT_L04_Niche_01_BoxFixed',W,H,[1620,690,540,275]);
 const board=await layer('M02_Board_Closed',b,'ACT_L04_Niche_02_BoardClosed',W,H,[1450,210,780,750]);
 // Same board, orthographic vertical hinge projection. No newly generated moving background.
 const boardOpen=output(b,'ACT_L04_Niche_03_BoardOpen_2304x1728.png');
 const bd=await clean(board),bb=bbox(bd.data,W,H);const plank=await sharp(bd.data,{raw:bd.info}).extract(bb).resize(bb.width,165,{fit:'fill'}).png().toBuffer();await clear(W,H).composite([{input:plank,left:bb.left,top:bb.top-120}]).png().toFile(boardOpen);await record(boardOpen,board,{hinge_pixel:[bb.left+bb.width/2,bb.top],note:'开板为同一板的投影状态；接入可改为绕上沿旋转'});
 await composite('02_剧情插画_Illustrations','ILL_L04_Niche_Closed',W,H,[niche,box,board]);await composite('02_剧情插画_Illustrations','ILL_L04_Niche_Open',W,H,[niche,box,boardOpen]);
 const c='03_动作分层_Actions/EPI_Smooth';
 const quilt=await save('M03_Quilt_Base',c,'ACT_EPI_Smooth_00_QuiltWrinkled',W,H);const flat=await save('M03_Quilt_Flat',c,'ACT_EPI_Smooth_01_QuiltFlat',W,H);
 const old=[];for(let i=0;i<6;i++)old.push(await layer('M03_OldHand_0'+i,c,'ACT_EPI_Smooth_OldHand_'+String(i).padStart(2,'0'),W,H,[0,1050-i*8,1500+40*i,650]));
 await layer('M03_YoungHand_Resting',c,'ACT_EPI_Smooth_YoungHand_Resting',W,H,[1500,650,804,600]);
 await composite('02_剧情插画_Illustrations','ILL_EPI_Smooth_Before',W,H,[quilt,old[0]]);await composite('02_剧情插画_Illustrations','ILL_EPI_Smooth_After',W,H,[flat,old[5]]);
 const ill='02_剧情插画_Illustrations';await save('M04_Photo_Master',ill,'ILL_L03_Photo_Canonical',W,H);
 await save('M04_Hands_Frame_Overlay','04_场景物件_Props/Photo','PROP_Photo_HandsFrameOverlay',W,H);
 await save('M04_Scene_Frame_Overlay','04_场景物件_Props/Photo','PROP_Photo_DesktopFrameOverlay',2048,2048);
 for(const [id,name] of [['M05_Ward_Master','ILL_Ward_Report'],['M05_Ward_Delivery','ILL_Ward_BoxDelivery']])await save(id,ill,name,W,H);
 await save('M05_Ward_Background','03_动作分层_Actions/Ward','WARD_00_Background',W,H);await save('M05_Ward_People','03_动作分层_Actions/Ward','WARD_01_People',W,H);
 for(const [n,folder,size] of [['M07','L01B_Tree',[2048,1024]],['M08','L04_Tree',[4096,2048]]])for(const [kind,suffix]of [['Trunk_Branch','00_TrunkBranch'],['Back_Leaves','01_BackLeaves'],['Front_Leaves','02_FrontLeaves']])await save(n+'_'+kind,'04_场景物件_Props/'+folder,'PROP_'+folder+'_'+suffix,...size,{review:'分层已提供；尚需校准层间位置和碰撞顶面'});
 await save('M07_ChildDrawing','04_场景物件_Props/Decor','DECOR_ChildDrawing_TreeToWindow',2048,2048);
 await save('M01_Uniform_Matching','04_场景物件_Props/Decor','DECOR_Uniform_BlueWhite',W,H);
 await save('M09_Fence_Open_2024','04_场景物件_Props/L00_Fence','PROP_L00_Fence_AdultOpening_2024',2048,2048,{review:'条件备选；先修原场景栅栏位置，不自动替换'});
 await save('M11_Menu_Background','06_界面_UI','UI_Menu_Background_NoControls',2560,1440);
 await layer('M11_Button_Blank','06_界面_UI','UI_Button_Wood_Blank',1024,256,[8,8,1008,240]);
 // Existing UI with genuine alpha: preserved unmodified, controls remain separate.
 for(const file of ['UI_TabRecordsPanel.png','UI_SubtitleBar.png','UI_ObjectiveCard.png','UI_HintBubble.png','UI_BoundaryHandle.png','UI_InteractIcon.png']){const f=output('06_界面_UI/复用原稿_Reuse',file);fs.copyFileSync(path.join(art,'UI',file),f);await record(f,path.join(art,'UI',file),{type:'existing_reused',note:'既有底图；布局和文字仍由程序处理'});}
 for(const file of ['ILL_PHOTO_BACK.jpg','ILL_EPILOGUE_LETTER.jpg']){const f=output(ill,file.replace('.jpg','_Reuse.png'));await sharp(path.join(art,'Illustrations',file)).png().toFile(f);await record(f,path.join(art,'Illustrations',file),{type:'existing_reused_blank_text_backing'});}
 fs.writeFileSync(path.join(root,'delivery_manifest.json'),JSON.stringify(rows,null,2));fs.writeFileSync(path.join(root,'layer_placements.json'),JSON.stringify(placements,null,2));console.log('Packaged',rows.length,'files');
})().catch(e=>{console.error(e);process.exit(1)});
