using System;
using System.IO;
using System.Linq;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Level;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.TimeBoundary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    /// Non-destructive installer: touches L01B and appends its dialogue to Bootstrap only.
    public static class RopePuzzleInstaller
    {
        [InitializeOnLoadMethod]
        private static void PreviewLifecycle()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if(change==PlayModeStateChange.EnteredEditMode)
                SessionState.SetBool("BorrowTomorrow.L01BPreview",false);
        }
        [MenuItem("BorrowTomorrow/桂花树/试玩本关（独立存档）")]
        public static void Preview()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
            SessionState.SetBool("BorrowTomorrow.L01BPreview",true);
            EditorApplication.EnterPlaymode();
        }
        private const string Art="Assets/Game/Art/Props/RopePuzzle_v7/";
        private static Transform Node(Transform parent,string name,Vector2 position)
        {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.position=position;return g.transform;}
        private static void Remove(Transform parent,string name)
        {var n=parent.Find(name);if(n!=null)UnityEngine.Object.DestroyImmediate(n.gameObject);}
        private static SpriteRenderer Image(Transform parent,string name,string file,Vector2 position,float width,int order=12)
        {
            var n=Node(parent,name,position);var r=n.gameObject.AddComponent<SpriteRenderer>();
            r.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+file+".png");
            if(r.sprite==null)throw new InvalidOperationException("Missing rope art: "+file);
            n.localScale=Vector3.one*(width/r.sprite.bounds.size.x);r.sortingLayerName="EraPlatform";r.sortingOrder=order;return r;
        }
        private static void Import()
        {
            foreach(string p in Directory.GetFiles(Art,"*.png",SearchOption.AllDirectories))
            {
                var i=(TextureImporter)AssetImporter.GetAtPath(p);
                bool strip=p.Contains("TEX_Rope");
                i.textureType=strip?TextureImporterType.Default:TextureImporterType.Sprite;
                i.spriteImportMode=SpriteImportMode.Single;i.spritePixelsPerUnit=400;
                i.alphaIsTransparency=true;i.mipmapEnabled=false;i.maxTextureSize=4096;
                i.textureCompression=TextureImporterCompression.Uncompressed;i.filterMode=FilterMode.Bilinear;
                i.wrapMode=TextureWrapMode.Clamp;
                if(!strip){var settings=new TextureImporterSettings();i.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,.5f);i.SetTextureSettings(settings);}
                i.SaveAndReimport();
            }
        }
        private static void Trigger(Transform t,Vector2 size)
        {t.gameObject.layer=LayerMask.NameToLayer("Interactable");var c=t.gameObject.AddComponent<BoxCollider2D>();c.isTrigger=true;c.size=size;}
        public static void Install()
        {
            AssetDatabase.Refresh();Import();
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L01_Courtyard.scene");
            var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LevelRoot>(true)).Single(r=>r.RoomId=="L01B");
            Transform content=room.transform.Find("Content");
            // Restore the known rack geometry, not the full scene scaffolder.
            DryingRackInstaller.Configure(room);
            var old=room.GetComponentInChildren<FoldingDryingRack>(true);
            var data=new SerializedObject(old);
            T Ref<T>(string name) where T:UnityEngine.Object => data.FindProperty(name).objectReferenceValue as T;
            var rack=Ref<TemporalObject>("rackObject");var leaf=Ref<Transform>("leaf");var eye=Ref<Transform>("ropeEye");var deck=Ref<BoxCollider2D>("deck");
            var mechanism=old.gameObject.AddComponent<RopeDryingRack>();
            mechanism.rack=rack;mechanism.leaf=leaf;mechanism.eye=eye;mechanism.deck=deck;
            mechanism.pullSound=Ref<AudioClip>("pullSound");mechanism.creakSound=Ref<AudioClip>("creakSound");mechanism.lockSound=Ref<AudioClip>("lockSound");mechanism.brokenSound=Ref<AudioClip>("brokenSound");
            mechanism.labelFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Art/UI/Fonts/PlaceholderChinese.ttf");
            mechanism.tree=room.GetComponentsInChildren<TemporalObject>(true).Single(t=>t.ObjectId=="L01B_tree_branch");
            UnityEngine.Object.DestroyImmediate(old);
            Transform past=rack.transform.Find("Past"),present=rack.transform.Find("Present");
            foreach(string name in new[]{"SupportArm","Latch","LiftRope","PullRope","Guide","PullEnd"})Remove(past,name);
            var treePast=mechanism.tree.transform.Find("Past");var treePresent=mechanism.tree.transform.Find("Present");
            foreach(string name in new[]{"RopeKnot_v7","RopeFork_v7","TrunkOccluder_v7"}){Remove(treePast,name);Remove(treePresent,name);}
            var forkArt=Image(treePresent,"RopeFork_v7","PROP_BranchFork_Bare_2024",new Vector2(53.3f,4.7f),2.6f,8);
            forkArt.flipX=true;mechanism.fork=Node(forkArt.transform,"RopeContact",new Vector2(53.3f,4.7f));
            var k=Node(treePast,"RopeKnot_v7",new Vector2(54.4f,.9f));Trigger(k,new Vector2(.85f,.7f));
            mechanism.knot=k;
            mechanism.knotImage=Image(k,"TiedKnot","PROP_Knot_Tied_1994",k.position,.85f,10);
            mechanism.tiedKnot=mechanism.knotImage.sprite;
            mechanism.releasedKnot=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"PROP_Knot_Released_1994.png");
            k.gameObject.AddComponent<RopeKnotInteractable>().mechanism=mechanism;
            Image(treePresent,"TrunkOccluder_v7","PROP_TrunkFront_Occluder_2024",new Vector2(54.4f,1.05f),.7f,9);
            mechanism.hookImage=Image(past,"RopeCleat_v7","PROP_RopeCleat_Empty_1994",new Vector2(49.7f,3.1f),.65f,10);
            mechanism.hook=Node(mechanism.hookImage.transform,"RopeContact",new Vector2(49.7f,3.1f));
            Image(present,"BrokenCleat_v7","PROP_RopeCleat_Broken_2024",new Vector2(49.7f,3.1f),.7f,10);
            mechanism.rest=Node(past,"RestingRopeEnd",new Vector2(49.8f,3.5f));
            mechanism.tailImage=Image(past,"RopeEnd_v7","PROP_RopeEnd_Grab",mechanism.rest.position,.42f,18);
            var line=Node(past,"ContinuousRope_v7",Vector2.zero).gameObject.AddComponent<LineRenderer>();
            string materialPath=Art+"RopeRuntime.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(material,materialPath);}
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"TEX_Rope_Hemp_Strip.png");EditorUtility.SetDirty(material);
            line.sharedMaterial=material;line.useWorldSpace=true;line.startWidth=line.endWidth=.24f;
            line.startColor=line.endColor=Color.white;line.sortingLayerName="EraPlatform";line.sortingOrder=15;line.numCapVertices=4;line.numCornerVertices=4;
            mechanism.rope=line;leaf.localRotation=Quaternion.Euler(0,0,-22);deck.enabled=false;
            // The folded tip remains above the ground: 1.9 + 4.0*sin(-22°) > 0.
            foreach(var evt in room.GetComponentsInChildren<NarrativeAutoEvent>(true).ToArray())
                if(evt.entry!=null && (evt.entry.entryId.StartsWith("DLG_L01B_TREE") || evt.entry.entryId.StartsWith("V4_010")))UnityEngine.Object.DestroyImmediate(evt.gameObject);
            Remove(past,"ClothMemory_v7");
            var peg=Node(past,"ClothMemory_v7",new Vector2(47.3f,3.05f));Trigger(peg,new Vector2(.6f,.7f));
            Image(peg,"Peg","Reused/PROP_ClothesPeg",peg.position,.19f,12);
            var clothEntry=AssetDatabase.LoadAssetAtPath<DialogueEntry>("Assets/Game/Data/Dialogue/L01B_V6/V4_0101.asset");
            var invPeg=peg.gameObject.AddComponent<NarrativeInteractable>();invPeg.EditorConfigure(clothEntry,DialoguePriority.Story,true);
            LevelLayouts.SetField(invPeg,"replayWholeChain",true);
            LevelLayouts.SetField(invPeg,"prompt","看看布夹");peg.gameObject.AddComponent<SceneMemoryClick>();
            var treeEntry=AssetDatabase.LoadAssetAtPath<DialogueEntry>("Assets/Game/Data/Dialogue/L01B_V6/V4_0105.asset");
            var evtNode=Node(content,"TreeMemoryOnBalcony_v7",new Vector2(59,4));
            var auto=evtNode.gameObject.AddComponent<NarrativeAutoEvent>();auto.roomId="L01B";auto.entry=treeEntry;auto.minimumX=58.3f;auto.minimumY=3.1f;auto.requireGrounded=true;
            L01BWallMemoryInstaller.Configure(room);
            var exit=room.GetComponentsInChildren<ExitDoor>(true).Single();
            LevelLayouts.SetField(exit,"ignoreLevelConditions",false);
            LevelLayouts.SetField(exit,"requiredFlagsOverride",new[]{RopeDryingRack.SecuredFlag,"HeightMarksSeen"});
            LevelLayouts.SetField(exit,"lockedHintOverride","先沿晾架走过来，在阳台听完这段回忆。");
            RopePuzzleVisualInstaller.Configure(room);
            EditorUtility.SetDirty(mechanism);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            // Append all current L01B lines; retain every other level's voice/story data.
            var bootstrap=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
            var library=bootstrap.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DialogueLibrary>(true)).Single();
            var revised=AssetDatabase.FindAssets("t:DialogueEntry",new[]{"Assets/Game/Data/Dialogue/L01B_V6"}).Select(g=>AssetDatabase.LoadAssetAtPath<DialogueEntry>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            if(revised.Any(e=>e==null))throw new InvalidOperationException("L01B dialogue missing");
            var all=library.Entries.Where(e=>e!=null && !revised.Any(n=>n.entryId==e.entryId)).Concat(revised).ToArray();
            var so=new SerializedObject(library);var entries=so.FindProperty("entries");entries.arraySize=all.Length;
            for(int i=0;i<all.Length;i++)entries.GetArrayElementAtIndex(i).objectReferenceValue=all[i];so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(bootstrap);EditorSceneManager.SaveScene(bootstrap);
            AssetDatabase.SaveAssets();
            Debug.Log("[RopePuzzleV7] Installed direct knot/rope/fork/cleat, indirect support guard and 8 story lines. Other rooms preserved.");
        }
        public static void InstallAndBuild(){Install();JumpRecoveryInstaller.BuildCurrent();}
    }
}
