using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Level;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.Player;
using BorrowTomorrow.TimeBoundary;
using BorrowTomorrow.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BorrowTomorrow.EditorTools
{
    public static class ArtInstaller
    {
        [Serializable] private class Manifest { public ArtSprite[] sprites; public ArtClip[] clips; }
        [Serializable] private class ArtSprite { public string name;public string path;public float[] rect; }
        [Serializable] private class ArtClip { public string name;public string path;public float fps;public float ppu;public bool loop;public ArtFrame[] frames; }
        [Serializable] private class ArtFrame { public float[] rect;public float[] pivot; }
        private const string Generated="Assets/Game/Data/Art/Sprites";
        private static Dictionary<string,Sprite> sprites;
        private static CharacterArtAnimator.Clip[] hero;
        private static string BuildRoot=>Environment.GetEnvironmentVariable("BORROWTOMORROW_ART_OUTPUT")??Path.GetFullPath("Builds/Art/BorrowTomorrow.exe");
        private static IEnumerable<T> All<T>(Scene scene)where T:Component=>scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<T>(true));
        private static Sprite S(string name)=>sprites.TryGetValue(name,out var value)?value:null;
        private static Sprite MakeSprite(string name,string texture,float[] rect,Vector2 pivot,float ppu)
        {
            string path=Generated+"/"+name+".asset";
            Sprite existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(existing!=null)return existing;
            Texture2D tex=AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
            if(tex==null)throw new InvalidOperationException("Missing art: "+texture);
            var importer=AssetImporter.GetAtPath(texture) as TextureImporter;
            int sourceWidth=tex.width,sourceHeight=tex.height;
            if(importer!=null)importer.GetSourceTextureWidthAndHeight(out sourceWidth,out sourceHeight);
            float sx=(float)tex.width/sourceWidth,sy=(float)tex.height/sourceHeight;
            var area=new Rect(rect[0]*sx,rect[1]*sy,rect[2]*sx,rect[3]*sy);
            var s=Sprite.Create(tex,area,pivot,ppu*sy,0,SpriteMeshType.FullRect);s.name=name;
            AssetDatabase.CreateAsset(s,path);return s;
        }
        private static void Import()
        {
            Directory.CreateDirectory(Generated);AssetDatabase.Refresh();
            var m=JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Game/Data/Art/art_manifest.json"));sprites=new Dictionary<string,Sprite>();
            foreach(var entry in m.sprites)sprites[entry.name]=MakeSprite(entry.name,entry.path,entry.rect,new Vector2(.5f,.5f),100);
            sprites["NearWall"]=MakeSprite("NearWall","Assets/Game/Art/Props/Constant/PROP_Wall_Old.png",new float[]{0,0,1024,4096},new Vector2(.5f,.5f),700);
            sprites["NearFloor"]=MakeSprite("NearFloor","Assets/Game/Art/Props/Temporal/PROP_WoodFloor_1994.png",new float[]{600,850,800,180},new Vector2(.5f,.5f),100);
            sprites["TreeBranchBark"]=MakeSprite("TreeBranchBark","Assets/Game/Art/Props/Temporal/PROP_TreeAttic_Grown_2024.png",new float[]{1800,1000,350,120},new Vector2(.5f,.5f),100);
            hero=m.clips.Select(c=>new CharacterArtAnimator.Clip{name=c.name,fps=c.fps,loop=c.loop,frames=c.frames.Select((f,i)=>MakeSprite("Hero_"+c.name+"_"+i.ToString("00"),c.path,f.rect,new Vector2(f.pivot[0],f.pivot[1]),c.ppu)).ToArray()}).ToArray();
            // User-approved v3 walk: reuse the exact reviewed Sprite/AnimationClip
            // assets rather than slicing or re-timing the old twelve-pose sheet.
            WalkCandidateInstaller.Create();
            const string walkRoot="Assets/Game/Art/Characters/WalkCandidate_v3";
            var approvedWalk=hero.First(c=>c.name=="Walk");
            approvedWalk.frames=Enumerable.Range(0,8).Select(i=>AssetDatabase.LoadAssetAtPath<Sprite>(walkRoot+"/Frames/Walk_"+i.ToString("00")+".asset")).ToArray();
            approvedWalk.animation=AssetDatabase.LoadAssetAtPath<AnimationClip>(walkRoot+"/Hero_Walk.anim");
            approvedWalk.fps=10;approvedWalk.loop=true;
            if(approvedWalk.animation==null||approvedWalk.frames.Any(f=>f==null))throw new InvalidOperationException("Approved walk assets are missing.");
            int climbIndex=Array.FindIndex(hero,c=>c.name=="Climb");
            if(climbIndex<0)throw new InvalidOperationException("Player art manifest is missing Climb.");
            hero[climbIndex]=ClimbFrameV5Installer.CreateGameplayClip(hero.First(c=>c.name=="Idle").frames[0]);
            var jump=JumpReferenceV6Installer.CreateGameplayClips(hero.First(c=>c.name=="Idle").frames[0]);
            hero=hero.Where(c=>!c.name.StartsWith("Jump",StringComparison.Ordinal)).Concat(jump).ToArray();
            AssetDatabase.SaveAssets();
        }
        private static void SkinPlayer()
        {
            string path="Assets/Game/Prefabs/Player_Graybox.prefab";GameObject root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var renderer=root.transform.Find("Visual").GetComponent<SpriteRenderer>();
                renderer.drawMode=SpriteDrawMode.Simple;renderer.color=Color.white;renderer.sprite=hero.First(c=>c.name=="Idle").frames[0];
                renderer.transform.localPosition=Vector3.zero;renderer.transform.localScale=Vector3.one;
                var animator=root.GetComponent<CharacterArtAnimator>()??root.AddComponent<CharacterArtAnimator>();animator.visual=renderer;animator.clips=hero;
                var motor=new SerializedObject(root.GetComponent<PlayerMotor2D>());
                motor.FindProperty("jumpAnticipation").floatValue=JumpReferenceV6Installer.PreparationSeconds;
                motor.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static SpriteRenderer Place(Transform parent,string name,Sprite sprite,Vector3 position,Vector2 size,string layer="EraPlatform",int order=5,bool local=false)
        {
            if(sprite==null)return null;
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            if(local)go.transform.localPosition=position;else go.transform.position=position;
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingLayerName=layer;renderer.sortingOrder=order;
            go.transform.localScale=new Vector3(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1);return renderer;
        }
        private static void Skin(SpriteRenderer original,string key,Vector2? size=null)
        {
            Sprite sprite=S(original.name=="IndoorFloor"?"NearFloor":key);if(sprite==null)return;
            Vector2 worldSize=size??original.size;original.enabled=false;
            Vector3 offset=Vector3.zero;
            if(original.name=="Ground"||original.name=="IndoorFloor"||original.name=="BankL"||original.name=="BankR")
            {
                float depth=Mathf.Max(worldSize.y,worldSize.x*sprite.bounds.size.y/sprite.bounds.size.x);
                offset.y=(worldSize.y-depth)*.5f;worldSize.y=depth;
            }
            if(original.name=="Narrative_uniform")offset.y=.2f;
            var skin=Place(original.transform,"ArtSkin",sprite,offset,worldSize,"EraPlatform",2,true);
            if(original.name=="IndoorFloor")
            {
                skin.transform.localScale=Vector3.one;skin.drawMode=SpriteDrawMode.Tiled;skin.size=worldSize;
            }
        }
        private static void Backdrop(LevelRoot room,Rect bounds,string key,float initial)
        {
            var go=new GameObject("ArtRoot");go.transform.SetParent(room.transform,false);
            float width=bounds.width+2;float height=Mathf.Max(bounds.height+2,width*9f/16f)+1;
            string pastKey=key=="Epilogue_Ward"?"BG_Epilogue_Ward":"BG_"+key+"_1994";
            string presentKey=key=="Epilogue_Ward"?pastKey:"BG_"+key+"_2024";
            var past=Place(go.transform,"PastBackdrop",S(pastKey),bounds.center,new Vector2(width,height),"FarBuildings",-100);
            var present=Place(go.transform,"PresentBackdrop",S(presentKey),bounds.center,new Vector2(width,height),"FarBuildings",-99);
            string dir="Assets/Game/Data/Art/Materials";Directory.CreateDirectory(dir);AssetDatabase.Refresh();
            Shader shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Art/EraArtBackground.shader");
            for(int i=0;i<2;i++)
            {
                string path=dir+"/"+room.RoomId+(i==0?"_Past":"_Present")+".mat";
                Material mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
                mat.SetFloat("_Past",i==0?1:0);
                var settings=new SerializedObject(room);float min=settings.FindProperty("boundaryMin").floatValue,max=settings.FindProperty("boundaryMax").floatValue;
                mat.SetFloat("_Boundary",initial>=max-.01f?100000f:initial<=min+.01f?-100000f:initial);
                (i==0?past:present).sharedMaterial=mat;EditorUtility.SetDirty(mat);
            }
            var split=go.AddComponent<EraArtBackdrop>();split.past=past;split.present=present;
            if(room.RoomId=="L02Indoor")
            {
                var wall=Place(go.transform,"NearRoomWall",S("NearWall"),new Vector3(49,2.5f,0),new Vector2(20,5),"EraBackground",0);
                wall.transform.localScale=Vector3.one;wall.drawMode=SpriteDrawMode.Tiled;wall.size=new Vector2(20,5);wall.color=new Color(.85f,.80f,.72f,1);
                Place(go.transform,"WallTopBeam",S("PROP_WoodFloor_1994"),new Vector3(49,5.05f,0),new Vector2(20,.2f),"EraPlatform",-8);
                Place(go.transform,"CraftTableTop",S("PROP_WoodFloor_1994"),new Vector3(49.8f,.48f,0),new Vector2(3.4f,.14f),"EraPlatform",1);
                Place(go.transform,"CraftTableLegL",S("PROP_WoodFloor_1994"),new Vector3(48.3f,.24f,0),new Vector2(.12f,.48f),"EraPlatform",1);
                Place(go.transform,"CraftTableLegR",S("PROP_WoodFloor_1994"),new Vector3(51.3f,.24f,0),new Vector2(.12f,.48f),"EraPlatform",1);
            }
        }
        private static void SkinRoom(LevelRoot room)
        {
            foreach(var t in room.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="ArtRoot"||t.name=="ArtSkin"||t.name=="ArtDoor").ToArray())if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);
            var so=new SerializedObject(room);Rect bounds=so.FindProperty("cameraBounds").rectValue;float initial=so.FindProperty("initialBoundary").floatValue;
            string key=room.RoomId=="L00"?"L00_Gate":room.RoomId=="L01A"?"L01A_Canal":room.RoomId=="L01B"?"L01B_Tree":room.RoomId=="L02"?"L02_Yard":room.RoomId=="L02Indoor"?"L02_Room":room.RoomId=="L03"?"L03_Corridor":room.RoomId=="L04"?"L04_Attic":"Epilogue_Ward";
            var originals=room.GetComponentsInChildren<SpriteRenderer>(true).ToArray();
            foreach(var original in originals)
            {
                string name=original.name;
                if(original.GetComponentInParent<CharacterArtAnimator>()!=null)continue;
                if(name.StartsWith("Wall")){original.enabled=false;continue;}
                var temporal=original.GetComponentInParent<TemporalObject>();
                string asset=null;Vector2? size=null;
                if(temporal!=null)
                {
                    string id=temporal.ObjectId;bool past=IsPast(original.transform,temporal.transform);
                    if(name=="Bar"||name.StartsWith("Rung_")||name=="BrokenStub"){original.enabled=false;continue;}
                    // L00 的挡路物已改为 2024 年木箱，由 L00ObstacleInstaller 负责贴图，不能再套栅栏皮。
                    if(id=="L00_fence_gate")continue;
                    else if(id=="L01A_plank_bridge")asset=past?"PROP_BridgePlank_1994":"PROP_BridgePlank_2024";
                    else if(id=="L02_maintenance")asset="PROP_MetalWalkway_2024";
                    else if(id=="L03_floor")asset="PROP_WoodFloor_1994";
                    else if(id=="L03_walkway")asset="PROP_MetalWalkway_2024";
                    else if(id=="L03_cabinet"){original.enabled=false;continue;}
                    else if(name.StartsWith("Branch"))asset="TreeBranchBark";
                    else if(name=="Sapling"){asset="PROP_TreeYoung_1994";size=new Vector2(1.4f,2.1f);}
                    else if(name=="Trunk"||name=="YardGround"){original.enabled=false;continue;}
                }
                else if(name=="Ground"||name=="IndoorFloor")asset=room.RoomId=="Epilogue"||room.RoomId=="L02Indoor"?"PROP_Ground_Ward":"PROP_Ground_Courtyard";
                else if(name=="BankL"||name=="BankR")asset="PROP_Bank_Canal";
                else if(name.Contains("Threshold"))asset="PROP_Threshold_Stone";
                else if(name.Contains("Platform")||name.Contains("Ledge"))asset="PROP_Platform_Stone";
                else if(name=="FloorToPhoto")asset="PROP_WoodFloor_1994";
                else if(name=="CompartmentVisual")asset="PROP_BoardNiche";
                else if(name=="Narrative_uniform"){asset="DECOR_Uniform";size=new Vector2(1.1f,.65f);}
                else if(name=="Narrative_ledger"){asset="DECOR_Ledger";size=new Vector2(1,.7f);}
                else if(name=="Narrative_photo"){asset="DECOR_PhotoFrame";size=new Vector2(.8f,1);}
                else if(name=="Narrative_notice"){asset="DECOR_VacateNotice";size=new Vector2(.7f,1);}
                else if(name=="Narrative_height_marks"){original.enabled=false;continue;}
                else if(name=="Narrative_niche"){original.enabled=false;continue;}
                else if(name=="Bed"||name=="Table"){original.enabled=false;continue;}
                if(asset!=null)Skin(original,asset,size);
            }
            foreach(var temporal in room.GetComponentsInChildren<TemporalObject>(true))
            {
                if(temporal.ObjectId.Contains("ladder"))
                {
                    var past=temporal.transform.Find("Past");var zone=past?.GetComponentInChildren<BoxCollider2D>(true);
                    if(zone!=null)Place(past,"ArtSkin",S("PROP_LadderTall_1994"),zone.transform.TransformPoint(zone.offset),new Vector2(.85f,zone.size.y),"EraPlatform",3);
                    var present=temporal.transform.Find("Present");
                    if(present!=null)Place(present,"ArtSkin",S("PROP_LadderTall_2024"),temporal.transform.position+Vector3.up*.9f,new Vector2(.75f,1.8f));
                }
                if(temporal.ObjectId.Contains("tree"))
                {
                    var present=temporal.transform.Find("Present");
                    if(present!=null)
                    {
                        bool attic=room.RoomId=="L04";
                        var treeVisual=Place(present,"ArtSkin",S(attic?"PROP_TreeAttic_Grown_2024":"PROP_TreeGrown_2024"),attic?new Vector3(14,.3f,0):new Vector3(58,3.2f,0),attic?new Vector2(28,13):new Vector2(8,7),"EraPlatform",-10);
                        if(attic)
                        {
                            string materialPath="Assets/Game/Data/Art/TreeCutout.mat";
                            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                            if(material==null){material=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Art/Characters/DogWhiteCutout.shader"));AssetDatabase.CreateAsset(material,materialPath);}
                            treeVisual.sharedMaterial=material;
                        }
                    }
                }
            }
            foreach(var door in room.GetComponentsInChildren<ExitDoor>(true))
            {
                string doorKey=room.RoomId=="L00"?"PROP_YardDoor":room.RoomId=="L03"?"PROP_AtticDoor":room.RoomId=="L02Indoor"?"PROP_Door_StairHall":room.RoomId=="L02"||room.RoomId=="L01B"?"PROP_Door_SewingRoom":"PROP_Door_YardSide";
                Place(door.transform,"ArtDoor",S(doorKey),Vector3.zero,new Vector2(1.2f,2.3f),"EraPlatform",8,true);
            }
            var sewing=room.transform.Find("NarrativeContent/SewingMachine");
            if(sewing!=null)
            {
                var renderer=sewing.GetComponent<SpriteRenderer>();
                string matPath="Assets/Game/Data/Art/SewingCutout.mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(mat==null){mat=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Art/Characters/DogWhiteCutout.shader"));AssetDatabase.CreateAsset(mat,matPath);}
                renderer.sharedMaterial=mat;
            }
            foreach(var tidy in room.GetComponentsInChildren<ClothTidyInteractable>(true))
            {
                tidy.completedSprite=S("DECOR_ClothesOnChair");tidy.chairPosition=new Vector3(54,.72f,0);
                if(tidy.cloth!=null)
                {
                    var renderer=tidy.cloth.GetComponent<SpriteRenderer>();var sprite=S("DECOR_ClothesOnFloor");
                    renderer.sprite=sprite;tidy.cloth.localScale=new Vector3(1/sprite.bounds.size.x,.6f/sprite.bounds.size.y,1);
                }
            }
            if(room.RoomId=="L03")CorridorRubbleInstaller.Configure(room);
            Backdrop(room,bounds,key,initial);
        }
        private static bool IsPast(Transform t,Transform stop){while(t!=null&&t!=stop){if(t.name=="Past")return true;t=t.parent;}return false;}
        private static Image PanelImage(Transform parent,string name,Sprite sprite,Color color)
        {
            Transform existing=parent.Find(name);if(existing!=null)UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);go.transform.SetAsFirstSibling();
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            var image=go.GetComponent<Image>();image.sprite=sprite;image.color=color;image.raycastTarget=false;return image;
        }
        private static void SkinHud(Scene scene)
        {
            foreach(var presenter in All<NarrativePresentation>(scene))
            {
                presenter.creditsBackground=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/UI/UI_CreditsBackground.jpg");
            }
            foreach(var journal in All<NarrativeJournal>(scene))journal.journalSkin=S("UI_TabRecordsPanel");
            SkinPause(scene);
            SkinDialogue(scene);
        }
        private static RectTransform UiRect(Transform parent,string name,Vector2 pos,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=size;return rt;
        }
        private static Text UiText(Transform parent,string value,Vector2 pos,Vector2 size,Font font,int fontSize=24)
        {
            var rt=UiRect(parent,value,pos,size);var text=rt.gameObject.AddComponent<Text>();text.text=value;text.font=font;text.fontSize=fontSize;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.23f,.14f,.08f);text.raycastTarget=false;return text;
        }
        private static readonly Color Ink=new Color(.29f,.17f,.08f);
        private static Sprite UiSprite(string file)
        {
            string path="Assets/Game/Art/UI/"+file+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(importer.textureType!=TextureImporterType.Sprite||importer.mipmapEnabled)
            {
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        // Dialogue art: wood plank (9-sliced, ends kept intact), paper name tag and ticket-style buttons.
        private static Sprite DialogPlank(string file="UI_DialogPlank")
        {
            string path="Assets/Game/Art/UI/"+file+".png";var sprite=UiSprite(file);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            var border=new Vector4(120,0,120,0);
            if(importer.spriteBorder!=border){importer.spriteBorder=border;importer.SaveAndReimport();sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);}
            return sprite;
        }
        private static void DialogSkins(NarrativePresentation presenter)
        {
            presenter.subtitleSkin=DialogPlank();presenter.buttonSkin=UiSprite("UI_DialogButton");presenter.nameTagSkin=UiSprite("UI_DialogNameTag");
        }
        // Single-line panels use the seamless one-board variant so the seam never cuts through text.
        private static void PlankPanel(Transform root,string name)
        {
            var sprite=DialogPlank("UI_DialogPlankSingle");var image=PanelImage(root,name,sprite,Color.white);image.type=Image.Type.Sliced;
            float height=((RectTransform)root).rect.height;if(height<1)height=((RectTransform)root.parent).rect.height;
            image.pixelsPerUnitMultiplier=sprite.rect.height*(100f/sprite.pixelsPerUnit)/Mathf.Max(1,height);
        }
        private static RectTransform UiStretch(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var rt=UiRect(parent,name,Vector2.zero,Vector2.zero);rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;return rt;
        }
        private static Image UiImage(Transform parent,string name,Sprite sprite,Vector2 pos,Vector2 size,Color? color=null)
        {
            var image=UiRect(parent,name,pos,size).gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color??Color.white;image.raycastTarget=false;return image;
        }
        private static Slider UiSlider(Transform parent,string label,float y,Font font,Sprite track,Sprite knob)
        {
            var rt=UiRect(parent,label,new Vector2(45,y),new Vector2(350,30));
            var bg=rt.gameObject.AddComponent<Image>();bg.sprite=track;bg.raycastTarget=true;
            var slider=rt.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.transition=Selectable.Transition.ColorTint;
            var fillArea=UiStretch(rt,"FillArea",new Vector2(.08f,.3f),new Vector2(.92f,.78f));
            var fill=UiStretch(fillArea,"Fill",Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();fill.color=new Color(.9f,.66f,.34f,.55f);fill.raycastTarget=false;slider.fillRect=fill.rectTransform;
            var handleArea=UiStretch(rt,"HandleArea",new Vector2(.08f,0),new Vector2(.92f,1));
            var handle=UiImage(handleArea,"Handle",knob,Vector2.zero,new Vector2(36,6));handle.raycastTarget=true;
            slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
            var colors=slider.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.96f,.88f);colors.pressedColor=new Color(.85f,.78f,.68f);colors.selectedColor=Color.white;slider.colors=colors;
            UiText(parent,label,new Vector2(-205,y),new Vector2(100,36),font,24).color=Ink;return slider;
        }
        // Top strip: a ticket-style handle with era captions underneath, both legible on sky and on dark rooms.
        private static void SkinTopBar(Scene scene)
        {
            foreach(var handle in All<BoundaryHandle>(scene))
            {
                var image=handle.GetComponent<Image>();
                if(image!=null){image.sprite=UiSprite("UI_DialogButton");image.color=Color.white;image.type=Image.Type.Simple;image.preserveAspect=true;}
                foreach(var label in handle.GetComponentsInChildren<Text>(true)){label.text="拖动";label.color=Ink;Halos(label);}
            }
            foreach(var name in new[]{"EraLabelPast","EraLabelPresent"})
            {
                var label=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<RectTransform>(true)).FirstOrDefault(x=>x.name==name);
                if(label==null)continue;
                // Corner captions on the top edge; BoundaryHandle fades them out as the handle reaches them.
                var p=label.anchoredPosition;p.y=-24f;label.anchoredPosition=p;
                foreach(var text in label.GetComponents<Text>()){text.color=Ink;text.alignment=TextAnchor.MiddleCenter;var halo=text.GetComponent<Shadow>();if(halo!=null)UnityEngine.Object.DestroyImmediate(halo);}
                // A small ticket plate keeps the caption readable on pale skies and dark rooms alike.
                string plateName=label.name+"Plate";
                var plate=label.parent.Find(plateName) as RectTransform;
                if(plate==null)
                {
                    var go=new GameObject(plateName,typeof(RectTransform),typeof(Image));plate=(RectTransform)go.transform;
                    plate.SetParent(label.parent,false);plate.SetSiblingIndex(label.GetSiblingIndex());
                }
                plate.anchorMin=label.anchorMin;plate.anchorMax=label.anchorMax;plate.pivot=label.pivot;
                Vector2 plateSize=new Vector2(label.sizeDelta.x+34,label.sizeDelta.y+12);
                plate.sizeDelta=plateSize;
                // Same pivot, larger rect: offset so the caption stays centred on the plate.
                plate.anchoredPosition=label.anchoredPosition+Vector2.Scale(new Vector2(.5f,.5f)-label.pivot,label.sizeDelta-plateSize);
                var image=plate.GetComponent<Image>();image.sprite=UiSprite("UI_DialogButton");image.color=new Color(1,1,1,.92f);image.raycastTarget=false;
                EditorUtility.SetDirty(plate);EditorUtility.SetDirty(label);
            }
        }
        private static void Halos(Text text)
        {
            var halo=text.GetComponent<Shadow>()??text.gameObject.AddComponent<Shadow>();
            halo.effectColor=new Color(1f,.98f,.92f,.55f);halo.effectDistance=new Vector2(1.5f,-1.5f);
        }
        private static void SkinPause(Scene scene)
        {
            Font font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Art/UI/Fonts/PlaceholderChinese.ttf");
            Sprite paper=UiSprite("UI_PausePaper"),plank=UiSprite("UI_PausePlank"),track=UiSprite("UI_PauseSliderTrack"),knob=UiSprite("UI_PauseSliderKnob"),ornament=UiSprite("UI_PauseOrnament");
            foreach(var pause in All<PauseMenu>(scene))
            {
                var parent=(RectTransform)pause.transform;parent.sizeDelta=new Vector2(620,860);
                var root=pause.transform.Find("Root");
                foreach(Transform child in root.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
                // 全屏压暗 + 拦截点击，菜单之外不可操作。
                var dim=UiImage(root,"Dim",null,Vector2.zero,new Vector2(6000,6000),new Color(.06f,.04f,.03f,.55f));dim.raycastTarget=true;
                PanelImage(root,"ArtPause",paper,Color.white).transform.SetSiblingIndex(1);
                const float o=-10;
                UiText(root,"暂  停",new Vector2(0,370+o),new Vector2(300,64),font,44).color=Ink;
                UiImage(root,"TitleOrnament",ornament,new Vector2(0,326+o),new Vector2(420,23));
                Button[] buttons=new Button[3];string[] labels={"继续游戏","返回主菜单","退出游戏"};
                for(int i=0;i<3;i++)
                {
                    var image=UiImage(root,labels[i],plank,new Vector2(0,250+o-i*106),new Vector2(440,94));image.raycastTarget=true;
                    buttons[i]=image.gameObject.AddComponent<Button>();buttons[i].targetGraphic=image;
                    var colors=buttons[i].colors;colors.normalColor=new Color(.9f,.9f,.9f);colors.highlightedColor=Color.white;colors.selectedColor=Color.white;colors.pressedColor=new Color(.72f,.66f,.6f);colors.fadeDuration=.08f;buttons[i].colors=colors;
                    var text=UiText(image.transform,labels[i],new Vector2(0,2),new Vector2(400,80),font,32);text.color=new Color(1,.92f,.76f);
                    var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.12f,.06f,.02f,.75f);shadow.effectDistance=new Vector2(1.5f,-2);
                }
                LevelLayouts.SetObject(pause,"resumeButton",buttons[0]);LevelLayouts.SetObject(pause,"mainMenuButton",buttons[1]);
                var controls=pause.GetComponent<PauseArtControls>()??pause.gameObject.AddComponent<PauseArtControls>();controls.quit=buttons[2];
                UiImage(root,"SettingsOrnament",ornament,new Vector2(0,-30+o),new Vector2(340,18),new Color(1,1,1,.8f));
                LevelLayouts.SetObject(pause,"masterVolume",UiSlider(root,"总音量",-86+o,font,track,knob));
                LevelLayouts.SetObject(pause,"musicVolume",UiSlider(root,"音乐",-148+o,font,track,knob));
                LevelLayouts.SetObject(pause,"voiceVolume",UiSlider(root,"配音",-210+o,font,track,knob));
                var toggleRt=UiRect(root,"Subtitles",new Vector2(0,-274+o),new Vector2(260,40));
                var hit=toggleRt.gameObject.AddComponent<Image>();hit.color=Color.clear;
                var toggle=toggleRt.gameObject.AddComponent<Toggle>();
                var box=UiImage(toggleRt,"Box",knob,new Vector2(-100,0),new Vector2(32,32));
                var tick=UiImage(box.transform,"Tick",null,Vector2.zero,new Vector2(12,12),Ink);tick.rectTransform.localEulerAngles=new Vector3(0,0,45);
                toggle.targetGraphic=box;toggle.graphic=tick;
                var colors2=toggle.colors;colors2.highlightedColor=new Color(1,.96f,.88f);colors2.pressedColor=new Color(.85f,.78f,.68f);colors2.selectedColor=Color.white;toggle.colors=colors2;
                var label=UiText(toggleRt,"显示对白字幕",new Vector2(20,0),new Vector2(200,36),font,24);label.color=Ink;
                LevelLayouts.SetObject(pause,"subtitlesToggle",toggle);
                var hint=UiText(root,"Esc  继续游戏",new Vector2(0,-336+o),new Vector2(300,30),font,18);hint.color=new Color(Ink.r,Ink.g,Ink.b,.55f);
            }
        }
        private static void SkinMenu(Scene scene)
        {
            // 主菜单已改为真正的木牌按钮；复用独立安装器，避免此处再次铺一张“按钮底图”。
            MainMenuUiInstaller.Apply(scene,S("UI_TitleScreen"));
        }
        [MenuItem("BorrowTomorrow/本地接入生成美术（保留碰撞与剧情）")]
        public static void Install()
        {
            Import();SkinPlayer();
            foreach(var name in new[]{"Bootstrap","MainMenu","L00_Gate","L01_Courtyard","L01_TreeCourtyard","L02_Sewing","L03_Corridor","L04_Attic","Epilogue"})
            {
                if (name == "L01_TreeCourtyard" && !System.IO.File.Exists(L01TreeSceneInstaller.ScenePath)) continue;
                var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/"+name+".scene");
                if(name=="Bootstrap")SkinHud(scene);
                else if(name=="MainMenu")SkinMenu(scene);
                else foreach(var room in All<LevelRoot>(scene).ToArray())SkinRoom(room);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
            if(!File.Exists("Assets/Game/Data/Art/enabled.json"))File.WriteAllText("Assets/Game/Data/Art/enabled.json","{\"enabled\":true}");
            Debug.Log("[ArtInstaller] Applied "+hero.Sum(c=>c.frames.Length)+" hero frames, room backgrounds, prop skins and UI without changing puzzle colliders.");
        }
        [MenuItem("BorrowTomorrow/仅重建暂停菜单")]
        public static void RebuildPauseMenu()
        {
            Import();var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");SkinPause(scene);EditorSceneManager.SaveScene(scene);
        }
        [MenuItem("BorrowTomorrow/仅重建 HUD 与对白皮肤")]
        public static void RebuildHud()
        {
            // Only reskins dialogue-related panels; no Import(), no pause-menu rebuild.
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");SkinDialogue(scene);EditorSceneManager.SaveScene(scene);
        }
        private static void SkinDialogue(Scene scene)
        {
            SkinTopBar(scene);
            foreach(var presenter in All<NarrativePresentation>(scene)){DialogSkins(presenter);EditorUtility.SetDirty(presenter);}
            foreach(var prompt in All<InteractionPrompt>(scene))
            {
                var root=prompt.transform.Find("Root");if(root!=null)PlankPanel(root,"ArtPaper");
                foreach(var text in prompt.GetComponentsInChildren<Text>(true))text.color=new Color(.98f,.94f,.86f);
            }
            foreach(var confirm in All<ResumeConfirmDialog>(scene))
            {
                var root=confirm.transform.Find("Root");if(root!=null){PlankPanel(root,"ArtConfirm");foreach(var text in root.GetComponentsInChildren<Text>(true))text.color=new Color(.98f,.94f,.86f);}
            }
            foreach(var panel in All<ObjectivePanel>(scene))
            {
                // The square paper card distorted into a smear at 640x90; use the strip plank instead.
                var root=panel.transform.Find("Root");if(root==null)continue;
                var shade=root.Find("ArtShade");if(shade!=null)UnityEngine.Object.DestroyImmediate(shade.gameObject);
                PlankPanel(root,"ArtPaper");
                foreach(var text in panel.GetComponentsInChildren<Text>(true))
                {
                    text.color=new Color(.98f,.94f,.86f);text.rectTransform.sizeDelta=new Vector2(((RectTransform)root).rect.width-90,text.rectTransform.sizeDelta.y);
                    var shadow=text.GetComponent<Shadow>()??text.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.08f,.04f,.01f,.8f);shadow.effectDistance=new Vector2(1.5f,-1.5f);
                }
            }
        }
        private static void ExportPreviews()
        {
            string output=Path.Combine(Path.GetDirectoryName(BuildRoot),"Previews");Directory.CreateDirectory(output);
            foreach(var level in new[]{"L00_Gate","L01_Courtyard","L01_TreeCourtyard","L02_Sewing","L03_Corridor","L04_Attic","Epilogue"})
            {
                if (level == "L01_TreeCourtyard" && !System.IO.File.Exists(L01TreeSceneInstaller.ScenePath)) continue;
                var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/"+level+".scene");
                var rooms=All<LevelRoot>(scene).ToArray();
                foreach(var room in rooms)
                {
                    foreach(var other in rooms)other.gameObject.SetActive(other==room);
                    var so=new SerializedObject(room);Rect bounds=so.FindProperty("cameraBounds").rectValue;float b=so.FindProperty("initialBoundary").floatValue;
                    foreach(var obj in room.GetComponentsInChildren<TemporalObject>(true))
                    {
                        Transform past=obj.transform.Find("Past"),present=obj.transform.Find("Present");
                        // Static composition preview uses the useful combined state; it never changes saved colliders/scenes.
                        bool usePast=obj.ObjectId.Contains("ladder")||obj.ObjectId=="L00_fence_gate"||obj.ObjectId=="L03_floor"||obj.ObjectId=="L01A_plank_bridge";
                        if(obj.ObjectId=="L03_cabinet")usePast=true;
                        if(past!=null)past.gameObject.SetActive(usePast);if(present!=null)present.gameObject.SetActive(!usePast);
                    }
                    Camera camera=All<Camera>(scene).First();camera.orthographic=true;camera.aspect=16f/9f;
                    camera.orthographicSize=Mathf.Max(bounds.height*.5f,bounds.width/(2*camera.aspect))+.35f;
                    camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,-10);camera.backgroundColor=new Color(.12f,.11f,.095f);
                    var spawn=room.GetComponentsInChildren<SpawnPoint>(true).FirstOrDefault();
                    GameObject player=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Player_Graybox.prefab")) as GameObject;
                    if(spawn!=null)player.transform.position=spawn.transform.position;
                    var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;camera.Render();
                    var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
                    File.WriteAllBytes(Path.Combine(output,room.RoomId+".png"),image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;
                    UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(player);
                }
            }
            ExportUiPreviews(output);
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
        }
        private static void CaptureUi(Camera camera,string path)
        {
            var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;
            foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
            Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
        private static void ExportUiPreviews(string output)
        {
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/MainMenu.scene");
            var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=5;
            foreach(var button in All<Button>(scene))if(button.targetGraphic!=null)button.targetGraphic.color=Color.clear;
            CaptureUi(camera,Path.Combine(output,"MainMenu.png"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=5;
            var root=new GameObject("NarrativeArtPreview");var presenter=root.AddComponent<NarrativePresentation>();
            presenter.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Art/UI/Fonts/PlaceholderChinese.ttf");DialogSkins(presenter);
            typeof(NarrativePresentation).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(presenter,null);
            var entry=AssetDatabase.LoadAssetAtPath<DialogueEntry>("Assets/Game/Data/Dialogue/StoryV13/DLG_L02_UNIFORM_01.asset");
            presenter.Present(entry,entry.subtitle,true,1,1);CaptureUi(camera,Path.Combine(output,"Dialogue.png"));
        }
        public static void InstallAndBuild()
        {
            Install();ExportPreviews();Directory.CreateDirectory(Path.GetDirectoryName(BuildRoot));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(x=>x.enabled).Select(x=>x.path).ToArray(),target=BuildTarget.StandaloneWindows64,locationPathName=BuildRoot,options=BuildOptions.Development});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Art build failed: "+report.summary.result);
            Debug.Log("[ArtInstaller] BUILD SUCCEEDED: "+BuildRoot);
        }
    }
}
