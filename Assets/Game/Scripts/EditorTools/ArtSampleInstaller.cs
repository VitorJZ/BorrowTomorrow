using System;
using System.IO;
using System.Linq;
using BorrowTomorrow.Level;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.Player;
using BorrowTomorrow.TimeBoundary;
using BorrowTomorrow.UI;
using BorrowTomorrow.Core.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowTomorrow.EditorTools
{
    // Narrow installer: existing scene objects/colliders/story markers are retained.
    public static class ArtSampleInstaller
    {
        public const string Root = "Assets/Game/Art/SampleV1/";
        private static string[] paths;
        private static string PathFor(string prefix) => paths.Single(p=>System.IO.Path.GetFileName(p).StartsWith(prefix, StringComparison.Ordinal));
        private static Texture2D T(string prefix) => AssetDatabase.LoadAssetAtPath<Texture2D>(PathFor(prefix));
        private static Sprite S(string prefix) => AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(prefix));
        private static NarrativeAction.Layer L(string name,params string[] frames) => new NarrativeAction.Layer{name=name,frames=frames.Select(T).ToArray()};
        private static NarrativeAction A(string id)=>AssetDatabase.LoadAssetAtPath<NarrativeAction>("Assets/Game/Data/Narrative/Actions/"+id+".asset");
        [MenuItem("BorrowTomorrow/完善 L01B 背景与人物比例")]
        public static void PolishL01B()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode before installing sample art.");
            for(int n=0;n<SceneManager.sceneCount;n++)if(SceneManager.GetSceneAt(n).isDirty)throw new InvalidOperationException("Open scene has unsaved edits.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            paths=Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).ToArray();
            foreach(var prefix in new[]{"BG_L01B_CommonArchitecture_v2","L01B_Sapling_v2"})
            {
                string path=PathFor(prefix);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Center;importer.SetTextureSettings(settings);
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
            }
            try
            {
                ConfigureHeroScale();
                var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L01_Courtyard.scene");
                ApplyTreeComposition(scene);EditorSceneManager.SaveScene(scene);
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtSample] L01B common architecture + fitted sapling + exit support; hero visual scale 1.30. Camera and physics retained.");
        }
        [MenuItem("BorrowTomorrow/接入 L01B 与三个动作美术样板")]
        public static void Install()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode before installing sample art.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Open scene has unsaved edits. Save them before installing sample art.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            paths=Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).ToArray();
            foreach(string path in paths)
            {
                var i=(TextureImporter)AssetImporter.GetAtPath(path);
                i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;
                i.spritePixelsPerUnit=path.Contains("/Hero/")?300:100;
                var settings=new TextureImporterSettings();i.ReadTextureSettings(settings);
                settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=path.Contains("/Hero/")?new Vector2(.5f,.125f):new Vector2(.5f,.5f);i.SetTextureSettings(settings);
                i.alphaIsTransparency=true;i.mipmapEnabled=false;i.wrapMode=TextureWrapMode.Clamp;i.filterMode=FilterMode.Bilinear;
                i.maxTextureSize=4096;i.textureCompression=TextureImporterCompression.Uncompressed;i.isReadable=path.Contains("L01B_Tree_00");
                i.SaveAndReimport();
            }
            ConfigureActions(); ConfigureStory(); ConfigureHero();
            try
            {
                var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L01_Courtyard.scene");
                BuildTreeSample(scene);
                ConfigurePresentation(scene);
                EditorSceneManager.SaveScene(scene);
                foreach(string sceneName in new[]{"L00_Gate","L02_Sewing","L03_Corridor","L04_Attic","Epilogue"})
                {
                    scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/"+sceneName+".scene");
                    ConfigurePresentation(scene);EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtSample] Installed L01B and three layered story actions; original three branch slopes fitted to intact tree; story text and event gates retained.");
        }
        private static void ConfigurePresentation(Scene scene)
        {
            foreach(var p in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<NarrativePresentation>(true)))
            {
                p.stagedArt=new[]{A("ACT_L02_CUFF"),A("ACT_L04_BOARD"),A("ACT_EPI_SMOOTH")};
                p.buttonSkin=S("UI_Button_Wood_Blank");
                const string patchPath="Assets/Game/Data/Art/SampleClothPatch.mat";
                var patch=AssetDatabase.LoadAssetAtPath<Material>(patchPath);
                if(patch==null){patch=new Material(AssetDatabase.LoadAssetAtPath<Shader>(Root+"SoftClothPatch.shader"));AssetDatabase.CreateAsset(patch,patchPath);}
                p.clothPatchMaterial=patch;EditorUtility.SetDirty(p);
            }
        }
        private static void ConfigureActions()
        {
            var cuff=A("ACT_L02_CUFF");cuff.beforeImage=T("ILL_L02_Cuff_Before");cuff.afterImage=T("ILL_L02_Cuff_After");
            cuff.stageBackground=T("ACT_L02_Cuff_00_Table");cuff.seconds=2.2f;cuff.zoom=1;cuff.coverRect=Rect.zero;
            var sleeve=L("SleeveBefore","ACT_L02_Cuff_03_SleeveBefore");sleeve.start=.48f;sleeve.end=.52f;sleeve.alphaAfter=0;
            var turned=L("SleeveTurned","ACT_L02_Cuff_04_SleeveAfter");turned.start=.48f;turned.end=.52f;turned.alphaBefore=0;
            var hand=L("TurnCuffHand","ACT_L02_Cuff_Hand_00","ACT_L02_Cuff_Hand_01","ACT_L02_Cuff_Hand_02","ACT_L02_Cuff_Hand_03");hand.start=.08f;hand.end=.85f;hand.offsetBefore=new Vector2(.07f,-.06f);hand.offsetAfter=new Vector2(-.02f,.03f);
            cuff.layers=new[]{L("RedFabric","ACT_L02_Cuff_01_FabricRed"),L("BlueFabric","ACT_L02_Cuff_02_FabricBlue"),sleeve,turned,hand};
            var board=A("ACT_L04_BOARD");board.beforeImage=T("ILL_L04_Niche_Closed");board.afterImage=T("ILL_L04_Niche_Open");board.stageBackground=T("ACT_L04_Niche_00_WallWindow");board.seconds=1.8f;board.zoom=1;board.coverRect=Rect.zero;
            var plank=L("HingedWoodenBoard","ACT_L04_Niche_02_BoardClosed");plank.pivot=new Vector2(1840f/2304f,1518f/1728f);plank.rotationAfter=new Vector3(-104,0,0);plank.start=.1f;plank.end=.85f;
            board.layers=new[]{L("FixedBlueBox","ACT_L04_Niche_01_BoxFixed"),plank};
            var smooth=A("ACT_EPI_SMOOTH");smooth.beforeImage=T("ILL_EPI_Smooth_Before");smooth.afterImage=T("ILL_EPI_Smooth_After");smooth.stageBackground=T("ACT_EPI_Smooth_00_QuiltWrinkled");smooth.seconds=2f;smooth.zoom=1;smooth.coverRect=Rect.zero;
            // Only reveal a local cloth patch, never crossfade the entire generated quilt grid.
            var patch=L("SmoothedCorner","ACT_EPI_Smooth_01_QuiltFlat");patch.region=new Rect(.48f,.11f,.23f,.21f);patch.start=.46f;patch.end=.82f;patch.alphaBefore=0;
            var old=L("GrandmasActiveHand",Enumerable.Range(0,6).Select(n=>"ACT_EPI_Smooth_OldHand_"+n.ToString("00")).ToArray());old.start=.12f;old.end=.86f;old.pivot=new Vector2(0,.5f);old.offsetBefore=new Vector2(-.10f,.025f);old.offsetAfter=new Vector2(-.10f,0);old.scaleBefore=new Vector2(1.1f,1);old.scaleAfter=new Vector2(1.22f,1);
            smooth.layers=new[]{patch,L("XiaoxiaosRestingHand","ACT_EPI_Smooth_YoungHand_Resting"),old};
            foreach(var a in new[]{cuff,board,smooth}){a.focus=new Vector2(.5f,.5f);EditorUtility.SetDirty(a);}
        }
        private static void ConfigureStory()
        {
            foreach(var p in Directory.GetFiles("Assets/Game/Data/Dialogue/StoryV13","*.asset"))
            {
                var e=AssetDatabase.LoadAssetAtPath<DialogueEntry>(p);if(e==null)continue;
                string key=null;
                if(e.entryId.StartsWith("DLG_L02_UNIFORM"))key="ILL_L02_Cuff_After";
                if(e.entryId=="DLG_L03_REVEAL_02")key="ILL_L03_Photo_Canonical";
                if(e.entryId=="DLG_L03_REVEAL_03")key="ILL_L03_Photo_InOldHands";
                if(e.entryId.StartsWith("DLG_L03_REVEAL")&&int.Parse(e.entryId.Substring(e.entryId.Length-2))>=4||e.entryId.StartsWith("DLG_L04_BOX"))key="ILL_Ward_Report";
                if(e.entryId=="DLG_EPI_02")key="ILL_Ward_BoxDelivery";
                if(e.entryId=="DLG_EPI_05"||e.entryId=="DLG_EPI_06")key="ILL_EPI_Smooth_Before";
                if(e.entryId=="DLG_EPI_07"||e.entryId=="DLG_EPI_08")key="ILL_EPI_Smooth_After";
                if(key!=null){e.illustration=T(key);EditorUtility.SetDirty(e);}
            }
        }
        private static void ConfigureHero()
        {
            string path="Assets/Game/Prefabs/Player_Graybox.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try{
                var a=root.GetComponent<CharacterArtAnimator>();
                // Keep the approved Walk/Climb and phased Jump clips from the main branch.
                // The sample pack only replaces idle and story gestures.
                foreach(var clip in a.clips.Where(c=>c.name=="Idle"||c.name=="Interact"||c.name=="PickUp"||c.name=="Mantle"))
                {
                    var frames=paths.Where(p=>p.Contains("/Hero/"+clip.name+"/")).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p)).ToArray();
                    if(frames.Length==0||frames.Any(f=>f==null))throw new InvalidOperationException("Missing sample hero frames: "+clip.name);
                    clip.frames=frames;
                }
                a.visual.sprite=a.clips.First(c=>c.name=="Idle").frames[0];
                a.visual.transform.localScale=new Vector3(1.3f,1.3f,1);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void ConfigureHeroScale()
        {
            string path="Assets/Game/Prefabs/Player_Graybox.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                // The sprite foot pivot stays at the original ground point. Do not scale the motor root.
                root.GetComponent<CharacterArtAnimator>().visual.transform.localScale=new Vector3(1.3f,1.3f,1);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static SpriteRenderer Place(Transform parent,string name,Sprite sprite,Vector2 pos,Vector2 size,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=pos;
            var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sortingLayerName="EraPlatform";r.sortingOrder=order;
            go.transform.localScale=new Vector3(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1);return r;
        }
        private static void FitSupport(Transform branch,Vector2 start,Vector2 end)
        {
            if(branch==null)throw new InvalidOperationException("Missing original branch support");
            var box=branch.GetComponent<BoxCollider2D>();
            Vector2 delta=end-start,normal=new Vector2(-delta.y,delta.x).normalized;
            branch.position=(start+end)*.5f-normal*(box.size.y*.5f);
            branch.rotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            box.size=new Vector2(delta.magnitude,box.size.y);
        }
        private static void BuildTreeSample(Scene scene)
        {
            var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LevelRoot>(true)).Single(r=>r.RoomId=="L01B");
            var old=room.transform.Find("FinalArtSample");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var sample=new GameObject("FinalArtSample");sample.transform.SetParent(room.transform,false);
            var settings=new SerializedObject(room);var bounds=settings.FindProperty("cameraBounds");var rect=bounds.rectValue;rect.center=new Vector2(55,3.5f);bounds.rectValue=rect;settings.ApplyModifiedPropertiesWithoutUndo();
            var backdrop=room.GetComponentInChildren<EraArtBackdrop>();
            // Matching camera framing, shared image rectangle; both eras retain one spatial coordinate system.
            backdrop.past.sprite=S("BG_L01B_Tree_1994");backdrop.present.sprite=S("BG_L01B_Tree_2024");
            foreach(var r in new[]{backdrop.past,backdrop.present})
            {r.transform.position=new Vector3(55,3,0);r.transform.localScale=new Vector3(34/r.sprite.bounds.size.x,19.125f/r.sprite.bounds.size.y,1);}
            var tree=room.GetComponentsInChildren<TemporalObject>(true).Single(t=>t.ObjectId=="L01B_tree_branch");
            var present=tree.transform.Find("Present");
            foreach(Transform child in present.Cast<Transform>().Where(t=>t.name=="SampleWholeTree"||t.name=="FittedLivingBranch"||t.name=="SampleBackLeaves"||t.name=="SampleFrontLeaves").ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach(var r in present.GetComponentsInChildren<SpriteRenderer>(true))r.enabled=false;
            var whole = S("L01B_WholeTree_v3");
            float width=15.90f, height=width*whole.texture.height/whole.texture.width;
            Place(present,"SampleWholeTree",whole,new Vector2(58.3f,3.5f),new Vector2(width,height),-10);
            // Keep the living tree intact; adapt only the three existing support slopes to its natural top.
            FitSupport(present.Find("BranchA"),new Vector2(50.9f,2.34f),new Vector2(56,2.32f));
            FitSupport(present.Find("BranchB"),new Vector2(56,2.32f),new Vector2(61.5f,2.92f));
            FitSupport(present.Find("BranchC"),new Vector2(61.5f,2.92f),new Vector2(66,3.52f));
            // Platforms remain unchanged. Give the permanent ledge a visible architectural support.
            var support=Place(sample.transform,"LedgeSupport",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Data/Art/Sprites/NearWall.asset"),new Vector2(47.75f,.85f),new Vector2(5.5f,1.7f),-5);
            support.transform.localScale=Vector3.one;support.drawMode=SpriteDrawMode.Tiled;support.size=new Vector2(5.5f,1.7f);
            var marks=room.transform.Find("NarrativeContent/HeightMarks");if(marks!=null){var rr=marks.GetComponent<SpriteRenderer>();rr.enabled=false;}
            Place(sample.transform,"TreeWindowDrawing",S("DECOR_ChildDrawing_TreeToWindow"),new Vector2(43.5f,1.6f),new Vector2(.95f,.95f),3);
            Font font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Art/UI/Fonts/PlaceholderChinese.ttf");
            for(int n=0;n<2;n++)
            {
                var label=new GameObject("HeightMark_"+n,typeof(TextMesh));label.transform.SetParent(sample.transform,false);label.transform.position=new Vector3(43.1f,.78f+n*.25f,0);
                var tm=label.GetComponent<TextMesh>();tm.font=font;tm.fontSize=52;tm.characterSize=.032f;tm.anchor=TextAnchor.MiddleLeft;tm.text="— 晓晓，"+(n==0?"六岁":"七岁");tm.color=new Color(.25f,.15f,.08f);
                var renderer=label.GetComponent<MeshRenderer>();renderer.sharedMaterial=font.material;renderer.sortingLayerName="EraPlatform";renderer.sortingOrder=4;
            }
            var ground=room.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.name=="Ground");
            var skin=ground.transform.Find("ArtSkin");if(skin!=null){var sr=skin.GetComponent<SpriteRenderer>();sr.drawMode=SpriteDrawMode.Simple;sr.transform.localScale=new Vector3(34/sr.sprite.bounds.size.x,8.5f/sr.sprite.bounds.size.y,1);sr.transform.localPosition=new Vector3(0,-3.75f,0);}
            if(paths.Any(p=>System.IO.Path.GetFileName(p).StartsWith("BG_L01B_CommonArchitecture_v2",StringComparison.Ordinal)))ApplyTreeComposition(scene);
        }
        private static void ApplyTreeComposition(Scene scene)
        {
            var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LevelRoot>(true)).Single(r=>r.RoomId=="L01B");
            var backdrop=room.GetComponentInChildren<EraArtBackdrop>(true);var common=S("BG_L01B_CommonArchitecture_v2");
            foreach(var r in new[]{backdrop.past,backdrop.present})
            {
                r.sprite=common;r.transform.position=new Vector3(55,3,0);
                float scale=34/common.bounds.size.x;r.transform.localScale=new Vector3(scale,scale,1);
            }
            // One building skeleton in both eras: only the color treatment changes at the seam.
            backdrop.past.color=Color.white;backdrop.present.color=new Color(.86f,.94f,.91f,1);
            var tree=room.GetComponentsInChildren<TemporalObject>(true).Single(t=>t.ObjectId=="L01B_tree_branch");
            var past=tree.transform.Find("Past");
            var previous=past.Find("SampleYoungTree");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            foreach(var r in past.GetComponentsInChildren<SpriteRenderer>(true))r.enabled=false;
            var young=S("L01B_Sapling_v2");float height=2.6f;
            Place(past,"SampleYoungTree",young,new Vector2(58.41f,1.17f),new Vector2(height*young.texture.width/young.texture.height,height),-10);
            var sample=room.transform.Find("FinalArtSample");
            if(sample==null)throw new InvalidOperationException("Install the existing L01B sample before applying composition polish.");
            var old=sample.Find("ExitSupport");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var support=Place(sample,"ExitSupport",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Data/Art/Sprites/NearWall.asset"),new Vector2(67.4f,1.375f),new Vector2(3.2f,2.75f),-5);
            support.transform.localScale=Vector3.one;support.drawMode=SpriteDrawMode.Tiled;support.size=new Vector2(3.2f,2.75f);
            var drawing=sample.Find("TreeWindowDrawing");if(drawing!=null)drawing.position=new Vector3(44.5f,1.6f,0);
            for(int n=0;n<2;n++)
            {
                var mark=sample.Find("HeightMark_"+n);if(mark!=null)mark.position=new Vector3(43.1f,1.08f+n*.25f,0);
            }
        }
    }
}
