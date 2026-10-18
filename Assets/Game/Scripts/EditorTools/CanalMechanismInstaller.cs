using System;
using System.Linq;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Level;
using BorrowTomorrow.TimeBoundary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class CanalMechanismInstaller
    {
        private const string Art="Assets/Game/Art/Props/CanalMechanism_v1/";
        private const string Audio="Assets/Game/Audio/SFX/CanalMechanism/";

        private static void Import()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Art.TrimEnd('/')}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                bool leaf=path.Contains("Bridge_Leaf");bool crank=path.Contains("Crank");
                importer.spritePixelsPerUnit=leaf?440f:640f;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;
                settings.spritePivot=leaf?new Vector2(.068f,.475f):crank?new Vector2(.355f,.495f):new Vector2(.5f,.544f);
                importer.SetTextureSettings(settings);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
                importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            foreach(var guid in AssetDatabase.FindAssets("t:AudioClip",new[]{Audio.TrimEnd('/')}))
            {
                var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var sample=importer.defaultSampleSettings;sample.loadType=AudioClipLoadType.DecompressOnLoad;
                sample.compressionFormat=AudioCompressionFormat.PCM;sample.preloadAudioData=true;
                sample.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings=sample;importer.forceToMono=false;importer.SaveAndReimport();
            }
        }

        private static Transform Node(Transform parent,string name,Vector2 position)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=position;return go.transform;
        }
        private static SpriteRenderer Sprite(Transform parent,string name,string file,Vector2 position,int order)
        {
            var node=Node(parent,name,position);var sr=node.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+file+".png");
            if(sr.sprite==null)throw new InvalidOperationException("Missing canal sprite: "+file);
            sr.sharedMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            sr.sortingLayerName="EraPlatform";sr.sortingOrder=order;return sr;
        }
        private static LineRenderer Rope(Transform parent,string name)
        {
            var node=Node(parent,name,Vector2.zero);var line=node.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            line.useWorldSpace=true;line.startWidth=line.endWidth=.025f;
            line.startColor=line.endColor=new Color(.36f,.25f,.13f);
            line.numCapVertices=3;line.sortingLayerName="EraPlatform";line.sortingOrder=5;return line;
        }

        // Reusable by the existing art installer, without rebuilding level geometry.
        public static void Configure(LevelRoot room)
        {
            var bridge=room.GetComponentsInChildren<TemporalObject>(true).Single(o=>o.ObjectId=="L01A_plank_bridge");
            var past=bridge.transform.Find("Past");var present=bridge.transform.Find("Present");
            foreach(var parent in new[]{past,present,room.transform.Find("Content")})
                foreach(var name in new[]{"CanalWinchArt","CanalBridgePivot","CanalWinchMechanism"})
                {var old=parent.Find(name);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);}
            var original=past.Find("PlankPast");
            if(original!=null)
            {
                foreach(var col in original.GetComponentsInChildren<Collider2D>(true))col.enabled=false;
                foreach(var sr in original.GetComponentsInChildren<SpriteRenderer>(true))sr.enabled=false;
            }
            var pastArt=Node(past,"CanalWinchArt",Vector2.zero);
            var presentArt=Node(present,"CanalWinchArt",Vector2.zero);
            Vector2 axle=new Vector2(8.65f,.934f);
            Sprite(pastArt,"FixedWinch1994","PROP_CanalWinch_Base_1994",axle,7);
            Sprite(presentArt,"BrokenWinch2024","PROP_CanalWinch_Base_2024",axle,7);
            var crankPast=Sprite(pastArt,"Crank1994","PROP_CanalWinch_Crank_1994",axle,8).transform;
            var crankPresent=Sprite(presentArt,"BrokenCrank2024","PROP_CanalWinch_Crank_2024",axle,8).transform;
            crankPast.localScale=crankPresent.localScale=Vector3.one*.48f;
            var guide=Node(pastArt,"RopeGuide",new Vector2(9.218f,1.528f));
            var pivot=Node(past,"CanalBridgePivot",new Vector2(9.8f,-.23f));
            Sprite(pivot,"DrawbridgeLeaf","PROP_CanalBridge_Leaf_1994",pivot.position,6);
            var deck=pivot.gameObject.AddComponent<BoxCollider2D>();
            deck.size=new Vector2(4.4f,.25f);deck.offset=new Vector2(2.2f,.105f);deck.enabled=false;
            pivot.gameObject.layer=LayerMask.NameToLayer("Ground");
            var eye=Node(pivot,"RopeEye",pivot.position);eye.localPosition=new Vector3(4.171f,.197f,0);
            pivot.localRotation=Quaternion.Euler(0,0,88);
            var pastRope=Rope(pastArt,"Rope1994");var presentRope=Rope(presentArt,"BrokenRope2024");
            // Serialize the rope pose too, avoiding a blank rope before runtime initializes.
            pastRope.positionCount=2;pastRope.SetPosition(0,guide.position);pastRope.SetPosition(1,eye.position);
            presentRope.positionCount=2;presentRope.SetPosition(0,new Vector3(9.12f,1.55f,0));presentRope.SetPosition(1,new Vector3(9.10f,1.10f,0));
            var mechanism=Node(room.transform.Find("Content"),"CanalWinchMechanism",new Vector2(8.6f,.65f));
            mechanism.gameObject.layer=LayerMask.NameToLayer("Interactable");
            var trigger=mechanism.gameObject.AddComponent<BoxCollider2D>();trigger.isTrigger=true;trigger.size=new Vector2(.8f,1.1f);
            var controller=mechanism.gameObject.AddComponent<CanalDrawbridge>();
            var so=new SerializedObject(controller);
            void Ref(string name,UnityEngine.Object value)=>so.FindProperty(name).objectReferenceValue=value;
            Ref("bridgeObject",bridge);Ref("bridgePivot",pivot);Ref("deckCollider",deck);
            Ref("pastCrank",crankPast);Ref("presentCrank",crankPresent);Ref("pastRope",pastRope);Ref("presentRope",presentRope);
            Ref("ropeGuide",guide);Ref("ropeEye",eye);
            foreach(var pair in new[]{("turnLoop","sfx_canal_winch_turn_loop"),("ropeTension","sfx_canal_rope_tension"),
                ("bridgeLower","sfx_canal_bridge_lower"),("bridgeSettle","sfx_canal_bridge_settle"),("jammed","sfx_canal_winch_jammed")})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Audio+pair.Item2+".wav");
                if(clip==null)throw new InvalidOperationException("Missing canal audio: "+pair.Item2);
                Ref(pair.Item1,clip);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            // Update the actual hint assets as well as the JSON source.
            var definition=room.Definition;
            foreach(var hint in definition.hintGroups??Array.Empty<HintGroup>())
                if(hint.stageKey=="HintPassed_L01A_Canal")
                {hint.hintLevel1="左岸有一套放桥的摇柄装置。";hint.hintLevel2="现在的装置已经损坏，过去还能使用。";
                 hint.hintLevel3="把边界移到木桥右侧，回到左岸摇柄旁按 E，放下木桥再过渠。";}
            EditorUtility.SetDirty(definition);
        }

        [MenuItem("BorrowTomorrow/关卡/接入水渠摇柄放桥")]
        public static void Install()
        {
            AssetDatabase.Refresh();Import();
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L01_Courtyard.scene");
            var room=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<LevelRoot>(true)).Single(r=>r.RoomId=="L01A");
            Configure(room);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[CanalMechanism] Installed five sprite layers, five sounds, raised bridge, interaction and persistent flag.");
        }
        public static void InstallAndBuild(){Install();JumpRecoveryInstaller.BuildCurrent();}
    }
}
