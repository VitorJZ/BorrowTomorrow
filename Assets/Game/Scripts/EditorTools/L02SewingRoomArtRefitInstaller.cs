#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowTomorrow.EditorTools
{
    public static class L02SewingRoomArtRefitInstaller
    {
        const string ScenePath = "Assets/Game/Scenes/L02_SewingRoom.scene";
        const string Root = "RoomRoot_L02Indoor/";
        const string Narrative = Root + "NarrativeContent/";
        const string Decor = "Assets/Game/Art/Props/Decor/";
        const string Delivery = "Assets/Game/Art/Delivery/Props/";
        static readonly string[] Files = { Decor+"DECOR_SewingMachine.png", Delivery+"DECOR_Uniform_BlueWhite_2304x1728.png", Decor+"DECOR_Fabrics.png", Decor+"DECOR_ClothesOnFloor.png", Delivery+"DECOR_WorkBench_L02.png", Decor+"FX_ContactShadow_Soft.png", Delivery+"DECOR_Ledger_Closed_L02.png", Delivery+"DECOR_ClothesRack_Empty_L02.png", Delivery+"DECOR_ClothesRack_Full_L02.png" };
        static Transform Find(string path)
        {
            var scene=SceneManager.GetActiveScene();
            var parts=path.Split('/');
            var root=scene.GetRootGameObjects().SingleOrDefault(g=>g.name==parts[0]);
            var t=root==null?null:root.transform;
            foreach(var part in parts.Skip(1)) t=t==null?null:t.Find(part);
            if(t==null) throw new InvalidOperationException("L02 art refit missing exact path: "+path);
            return t;
        }
        static void Import(string file)
        {
            if(!File.Exists(file)) throw new FileNotFoundException(file);
            AssetDatabase.ImportAsset(file,ImportAssetOptions.ForceSynchronousImport);
            var ti=(TextureImporter)AssetImporter.GetAtPath(file);
            ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Single;
            ti.spritePixelsPerUnit=100;ti.alphaIsTransparency=true;ti.mipmapEnabled=false;
            ti.filterMode=FilterMode.Bilinear;ti.wrapMode=TextureWrapMode.Clamp;
            ti.textureCompression=TextureImporterCompression.Uncompressed;ti.maxTextureSize=4096;
            foreach(var platform in new[]{"Standalone","iPhone","Android","WebGL"}) ti.ClearPlatformTextureSettings(platform);
            var settings=new TextureImporterSettings();ti.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,0);
            settings.spriteMeshType=SpriteMeshType.FullRect;ti.SetTextureSettings(settings);ti.SaveAndReimport();
        }
        static SpriteRenderer Set(Transform t,int file,float width,float x,float y,int order)
        {
            var sr=t.GetComponent<SpriteRenderer>();
            if(sr==null) throw new InvalidOperationException("Missing SpriteRenderer: "+t.name);
            sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Files[file]);
            if(sr.sprite==null)throw new InvalidOperationException("Missing sprite: "+Files[file]);
            var parent=t.parent==null?Vector3.one:t.parent.lossyScale;
            if(Mathf.Abs(parent.x-parent.y)>.00001f||Mathf.Abs(parent.x-1)>.00001f)
                throw new InvalidOperationException("Unexpected parent scale: "+t.name);
            float scale=width/(sr.sprite.rect.width/100f);
            t.localScale=new Vector3(scale,scale,1);t.position=new Vector3(x,y,t.position.z);
            sr.sharedMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            sr.color=Color.white;sr.sortingLayerName="EraPlatform";sr.sortingOrder=order;
            return sr;
        }
        static void Shadow(SpriteRenderer subject,float width)
        {
            // Sibling follows the same era activation without inheriting the prop scale.
            string name=subject.name+"_ContactShadow";var parent=subject.transform.parent;
            var t=subject.transform.Find(name)??parent.Find(name);
            if(t!=null)t.SetParent(parent,true);
            if(t==null){t=new GameObject(name,typeof(SpriteRenderer)).transform;t.SetParent(parent,false);}
            var sr=t.GetComponent<SpriteRenderer>();if(sr==null)throw new InvalidOperationException(name);
            sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Files[5]);
            float worldWidth=width*.78f;float sx=worldWidth/(sr.sprite.rect.width/100f);
            float sy=Mathf.Min(.065f,width*.09f)/(sr.sprite.rect.height/100f);
            t.localScale=new Vector3(sx,sy,1);
            t.position=new Vector3(subject.transform.position.x,subject.transform.position.y-.02f,subject.transform.position.z);
            sr.sharedMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            sr.color=new Color(0,0,0,.20f);sr.sortingLayerName="EraPlatform";sr.sortingOrder=subject.sortingOrder-1;
        }
        static Dictionary<int,string> ProtectedState(Scene scene)
        {
            var data=new Dictionary<int,string>();
            foreach(var root in scene.GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Component>(true))
            {
                if(c is Transform || c is SpriteRenderer || c is BorrowTomorrow.Narrative.ClothTidyInteractable)continue;
                data.Add(c.GetInstanceID(),EditorJsonUtility.ToJson(c));
            }
            return data;
        }
        static void PreserveProtectedYaml(string original)
        {
            // Saving may materialize unrelated new MonoBehaviour default fields.
            // Keep every non-art YAML block byte-for-byte from before this installation.
            var pattern=@"^--- !u!(\d+) &(\d+)\r?\n.*?(?=^--- !u!|\z)";
            var blocks=new Dictionary<string,string>();
            foreach(Match m in Regex.Matches(original,pattern,RegexOptions.Multiline|RegexOptions.Singleline))
                if(m.Groups[1].Value!="1" && m.Groups[1].Value!="4" && m.Groups[1].Value!="212" && !m.Value.Contains("\n  chairPosition:"))blocks[m.Groups[2].Value]=m.Value;
            var saved=File.ReadAllText(ScenePath);
            saved=Regex.Replace(saved,pattern,m=>blocks.ContainsKey(m.Groups[2].Value)?blocks[m.Groups[2].Value]:m.Value,RegexOptions.Multiline|RegexOptions.Singleline);
            File.WriteAllText(ScenePath,saved,new System.Text.UTF8Encoding(false));
        }
        [MenuItem("BorrowTomorrow/美术/重装 L02 缝纫间道具")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogError("L02 art refit: exit Play mode first.");return;}
            var originalYaml=File.ReadAllText(ScenePath);
            var active=SceneManager.GetActiveScene();
            if(active.isDirty)throw new InvalidOperationException("Save existing scene edits before L02 art refit.");
            var scene=active.path==ScenePath?active:EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var machine=Find(Narrative+"Temporal_machine/Past/SewingMachine");
            var uniform=Find(Narrative+"Temporal_uniform/Past/Narrative_uniform/ArtSkin");
            var fabrics=Find(Narrative+"Temporal_fabrics/Past/Fabrics");
            var clothes=Find(Narrative+"Temporal_clothes/Past/Clothes");
            var ledger=Find(Narrative+"Temporal_ledger/Past/Narrative_ledger/ArtSkin");
            var tidy=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BorrowTomorrow.Narrative.ClothTidyInteractable>(true)).Single();
            var art=Find(Root+"ArtRoot");var bench=art.Find("CraftTableBench");
            var legacy=new[]{"CraftTableTop","CraftTableLegL","CraftTableLegR"};
            if(bench==null)foreach(var name in legacy)Find(Root+"ArtRoot/"+name);
            foreach(var name in legacy){var t=art.Find(name);if(t!=null && (t.childCount!=0||t.GetComponents<Component>().Any(c=>!(c is Transform)&&!(c is SpriteRenderer))))throw new InvalidOperationException("Legacy table has protected components: "+name);}
            var before=ProtectedState(scene);
            foreach(var file in Files)Import(file);
            if(bench==null){bench=new GameObject("CraftTableBench",typeof(SpriteRenderer)).transform;bench.SetParent(art,false);}
            foreach(var name in legacy){var t=art.Find(name);if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);}
            var renderers=new[]{Set(machine,0,1.85f,5.8f,.02f,0),Set(uniform,1,1.0f,8.9f,.70f,3),Set(fabrics,2,.60f,10.1f,.70f,2),Set(clothes,3,.80f,13f,.02f,0),Set(bench,4,3.40f,9.8f,0,1),Set(ledger,6,.85f,11.1f,.642f,3)};
            float[] widths={1.85f,1.0f,.60f,.80f,3.40f,.85f};
            for(int i=0;i<renderers.Length;i++)Shadow(renderers[i],widths[i]);
            // The bookmark hangs below the book body; support the page block at y=.70.
            ledger.parent.Find("ArtSkin_ContactShadow").position=new Vector3(11.1f,.68f,ledger.position.z);
            // Same Past root as the ground pile: era visibility stays synchronized.
            var rack=clothes.parent.Find("ClothesRack");
            if(rack==null){rack=new GameObject("ClothesRack",typeof(SpriteRenderer)).transform;rack.SetParent(clothes.parent,false);}
            var rackRenderer=Set(rack,7,1.65f,13f,.02f,-1);
            Shadow(rackRenderer,1.65f);
            tidy.clothesRack=rackRenderer;
            tidy.chairPosition=new Vector3(13f,.02f,0);
            tidy.completedSprite=AssetDatabase.LoadAssetAtPath<Sprite>(Files[8]);
            // Follow the cloth movement, era activation and save restoration.
            var clothShadow=clothes.Find("Clothes_ContactShadow")??clothes.parent.Find("Clothes_ContactShadow");
            clothShadow.SetParent(clothes,true);
            var after=ProtectedState(scene);
            if(before.Count!=after.Count||before.Any(kv=>!after.ContainsKey(kv.Key)||after[kv.Key]!=kv.Value))throw new InvalidOperationException("Protected component state changed; scene has not been saved.");
            var lines=new List<string>();
            for(int i=0;i<renderers.Length;i++){
                var sr=renderers[i];var scale=sr.transform.localScale;var b=sr.bounds;
                if(sr.sprite==null||Mathf.Abs(scale.x-scale.y)>.00001f||Mathf.Abs(b.size.x-widths[i])>widths[i]*.05f)throw new InvalidOperationException("Bounds validation failed: "+sr.name);
                lines.Add(sr.name+" scale="+scale.ToString("F6")+" boundsCenter="+b.center.ToString("F4")+" boundsSize="+b.size.ToString("F4")+" min="+b.min.ToString("F4")+" max="+b.max.ToString("F4"));
            }
            foreach(var sr in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>(true)).Where(s=>s.name.EndsWith("_ContactShadow")))lines.Add(sr.name+" scale="+sr.transform.localScale.ToString("F6")+" bounds="+sr.bounds);
            lines.Add("ClothesRack scale="+rack.localScale.ToString("F6")+" bounds="+rackRenderer.bounds);
            lines.Add("Protected components, camera, colliders, triggers and TemporalObject settings unchanged; only tidy completion target/art configuration updated.");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();PreserveProtectedYaml(originalYaml);
            Directory.CreateDirectory("ArtRegen_20261031");File.WriteAllLines("ArtRegen_20261031/ClothesRack/unity_refit.log",lines);
            Debug.Log("L02 art refit complete\n"+string.Join("\n",lines));
        }
    }
}
#endif
