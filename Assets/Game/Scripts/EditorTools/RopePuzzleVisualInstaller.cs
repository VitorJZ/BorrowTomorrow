using System;
using System.Linq;
using BorrowTomorrow.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class RopePuzzleVisualInstaller
    {
        private const string Art="Assets/Game/Art/Props/RopePuzzle_v8/";
        private const string TreeArt="Assets/Game/Art/Props/RopePuzzle_v9/";
        private static Material Material(string name,Color color)
        {
            string path=Art+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.mainTexture=null;EditorUtility.SetDirty(m);return m;
        }
        private static LineRenderer Line(Transform parent,string name,Material material,float width,int order)
        {
            var prior=parent.Find(name);if(prior!=null)UnityEngine.Object.DestroyImmediate(prior.gameObject);
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            var l=go.AddComponent<LineRenderer>();l.useWorldSpace=true;l.sharedMaterial=material;
            l.startColor=l.endColor=Color.white;l.startWidth=l.endWidth=width;
            l.numCapVertices=4;l.numCornerVertices=4;l.sortingLayerName="EraPlatform";l.sortingOrder=order;
            return l;
        }
        private static void Arc(LineRenderer line,Vector3 center,float from,float to,float radiusX,float radiusY)
        {
            line.positionCount=25;
            for(int i=0;i<25;i++){float a=Mathf.Lerp(from,to,i/24f);line.SetPosition(i,center+new Vector3(Mathf.Cos(a)*radiusX,Mathf.Sin(a)*radiusY,0));}
        }
        private static void Width(SpriteRenderer r,float width) => r.transform.localScale=Vector3.one*(width/r.sprite.bounds.size.x);
        private static Sprite TreeSprite(string name,Vector2 pivot)
        {
            string path=TreeArt+name+".png";
            var i=(TextureImporter)AssetImporter.GetAtPath(path);
            if(i==null)throw new InvalidOperationException("Missing tree state: "+path);
            i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;i.spritePixelsPerUnit=100;
            i.alphaIsTransparency=true;i.mipmapEnabled=false;i.maxTextureSize=2048;i.textureCompression=TextureImporterCompression.Uncompressed;
            var settings=new TextureImporterSettings();i.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=pivot;
            i.SetTextureSettings(settings);i.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static Vector3 TreePoint(SpriteRenderer tree,Vector2 uvFromTop)
        {
            var s=tree.sprite;
            return tree.transform.TransformPoint(new Vector3((s.rect.width*uvFromTop.x-s.pivot.x)/s.pixelsPerUnit,
                (s.rect.height*(1-uvFromTop.y)-s.pivot.y)/s.pixelsPerUnit,0));
        }
        public static void Configure(LevelRoot room)
        {
            var r=room.GetComponentInChildren<RopeDryingRack>(true);
            var past=r.rack.transform.Find("Past");
            var treePast=r.tree.transform.Find("Past");
            // RopeContact remains alive; only the conspicuous separate branch overlay is retired.
            r.forkImage=r.tree.transform.Find("Present/RopeFork_v7").GetComponent<SpriteRenderer>();
            r.forkImage.enabled=false;
            var mature=r.tree.transform.Find("Present/NaturalOsmanthus").GetComponent<SpriteRenderer>();
            mature.sprite=TreeSprite("PROP_Osmanthus_ClothesPole_2024",new Vector2(.375f,.015f));
            mature.color=Color.white;
            mature.transform.position=new Vector3(54.4f,0,0);
            mature.transform.localScale=Vector3.one*(6.8f/mature.sprite.bounds.size.y);
            r.fork.position=TreePoint(mature,new Vector2(.15f,.36f));
            var start=mature.transform.Find("PoleStart");
            if(start==null){var go=new GameObject("PoleStart");go.transform.SetParent(mature.transform,false);start=go.transform;}
            var end=mature.transform.Find("PoleEnd");
            if(end==null){var go=new GameObject("PoleEnd");go.transform.SetParent(mature.transform,false);end=go.transform;}
            start.position=TreePoint(mature,new Vector2(.12f,.36f));
            end.position=TreePoint(mature,new Vector2(.89f,.43f));
            r.poleStart=start; r.poleEnd=end;

            // Native sprite subasset: reuse the existing knot texture unchanged;
            // expose the knot detail, and draw its two trunk wraps separately.
            string knotPath=Art+"KnotDetail.asset";
            var detail=AssetDatabase.LoadAssetAtPath<Sprite>(knotPath);
            if(detail==null)
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Props/RopePuzzle_v7/PROP_Knot_Tied_1994.png");
                detail=Sprite.Create(texture,new Rect(texture.width*.565f,texture.height*.235f,texture.width*.197f,texture.height*.474f),new Vector2(.5f,.5f),400,0,SpriteMeshType.FullRect);
                AssetDatabase.CreateAsset(detail,knotPath);
            }
            r.knot.position=new Vector3(54.4f,1.14f,0);
            r.knotImage.sprite=r.tiedKnot=detail;
            r.knotImage.transform.position=r.knot.position+Vector3.right*.23f;Width(r.knotImage,.44f);
            r.knotImage.sortingOrder=18;
            r.rest.position=new Vector3(54.25f,.56f,0);
            r.tailImage.transform.position=r.rest.position;Width(r.tailImage,.6f);
            Width(r.hookImage,.95f);

            var hemp=Material("ReadableHemp",new Color(.86f,.70f,.44f));
            var dark=Material("HempEdge",new Color(.20f,.13f,.065f));
            r.rope.sharedMaterial=hemp;r.rope.startWidth=r.rope.endWidth=.048f;
            r.rope.startColor=r.rope.endColor=Color.white;
            r.ropeOutline=Line(past,"RopeOutline_v8",dark,.078f,6);
            r.treeWrapBack=Line(treePast,"TreeWrapBack_v8",hemp,.048f,-11);
            r.treeWrapFront=Line(treePast,"TreeWrapFront_v8",hemp,.048f,15);
            Arc(r.treeWrapBack,r.knot.position,0,Mathf.PI,.26f,.10f);
            Arc(r.treeWrapFront,r.knot.position,Mathf.PI,Mathf.PI*2,.26f,.10f);
            r.tiedTail=Line(treePast,"TiedTail_v8",hemp,.046f,17);
            r.cleatWrap=Line(past,"CleatWrap_v8",hemp,.045f,16);
            Arc(r.cleatWrap,r.hook.position,0,Mathf.PI*4,.17f,.085f);r.cleatWrap.enabled=false;
            var prior=past.Find("SlackCoil_v8");if(prior!=null)UnityEngine.Object.DestroyImmediate(prior.gameObject);
            var coil=new GameObject("SlackCoil_v8");coil.transform.SetParent(past,false);coil.transform.position=r.rest.position+new Vector3(-.12f,-.12f,0);
            r.slackCoil=coil.AddComponent<SpriteRenderer>();r.slackCoil.sprite=r.releasedKnot;
            r.slackCoil.sortingLayerName="EraPlatform";r.slackCoil.sortingOrder=16;Width(r.slackCoil,1.15f);coil.SetActive(false);
            r.youngTreeTied=TreeSprite("PROP_Sapling_RopeTied_1994",new Vector2(.565f,.03f));
            r.youngTreeFree=TreeSprite("PROP_Sapling_RopeFree_1994",new Vector2(.585f,.03f));
            r.youngTreeImage=treePast.Find("SampleYoungTree").GetComponent<SpriteRenderer>();
            r.youngTreeImage.sprite=r.youngTreeTied;r.youngTreeImage.sortingOrder=10;
            r.youngTreeImage.transform.position=new Vector3(54.4f,0,0);
            r.youngTreeImage.transform.localScale=new Vector3(.23f,.23f,1);
            r.knot.position=TreePoint(r.youngTreeImage,new Vector2(.612f,.712f));
            var lead=treePast.Find("TiedLead_v8");
            if(lead==null){var g=new GameObject("TiedLead_v8");g.transform.SetParent(treePast,false);lead=g.transform;}
            lead.position=TreePoint(r.youngTreeImage,new Vector2(.32f,.778f));r.tiedLead=lead;
            r.rest.position=new Vector3(r.knot.position.x+.65f,.55f,0);
            r.tailImage.transform.position=r.rest.position;
            coil.transform.position=new Vector3(r.rest.position.x-.12f,.20f,0);
            var scale=coil.transform.localScale;scale.y*=.5f;coil.transform.localScale=scale;
            r.knotImage.gameObject.SetActive(false);r.treeWrapBack.enabled=false;r.treeWrapFront.enabled=false;r.tiedTail.enabled=false;
            var exit=room.GetComponentsInChildren<ExitDoor>(true).Single();
            LevelLayouts.SetField(exit,"lockedHintOverride","门还打不开。");
            EditorUtility.SetDirty(r);
        }
        [MenuItem("BorrowTomorrow/桂花树/接入场景线索与鼠标操作 v9")]
        public static void Install()
        {
            AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene(L01TreeSceneInstaller.AuthoredTreeScenePath);
            var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LevelRoot>(true)).Single(r=>r.RoomId=="L01B");
            Configure(room);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
        }
    }
}
