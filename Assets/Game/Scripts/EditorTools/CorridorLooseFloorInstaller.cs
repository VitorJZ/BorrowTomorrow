using System;
using System.IO;
using System.Linq;
using BorrowTomorrow.Level;
using BorrowTomorrow.TimeBoundary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class CorridorLooseFloorInstaller
    {
        const string Art="Assets/Game/Art/Props/CorridorFloor_v1/",Sprites="Assets/Game/Data/Art/Sprites/CorridorFloor_v12/";
        static Transform Child(Transform parent,string name,Vector3 position)
        {var old=parent.Find(name);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=position;return go.transform;}
        static Sprite Crop(string name,string file,Rect rect,Vector2 pivot)
        {
            string path=Sprites+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(existing!=null)return existing;
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+file);if(texture==null)throw new Exception("Missing existing floor art "+file);
            float sourceWidth=file.Contains("BrokenObvious")?1881:file.Contains("Joists")?1774:2172,sourceHeight=file.Contains("BrokenObvious")?836:file.Contains("Joists")?887:724;
            rect=new Rect(rect.x*texture.width/sourceWidth,rect.y*texture.height/sourceHeight,rect.width*texture.width/sourceWidth,rect.height*texture.height/sourceHeight);
            var sprite=Sprite.Create(texture,rect,pivot,100,0,SpriteMeshType.FullRect);sprite.name=name;AssetDatabase.CreateAsset(sprite,path);return sprite;
        }
        static SpriteRenderer Draw(Transform parent,string name,Sprite sprite,Vector3 position,Vector2 worldSize,int order=4,bool flip=false)
        {
            var t=Child(parent,name,position);var sr=t.gameObject.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingLayerName="EraPlatform";sr.sortingOrder=order;sr.flipX=flip;
            t.localScale=new Vector3(worldSize.x/sprite.bounds.size.x,worldSize.y/sprite.bounds.size.y,1);return sr;
        }
        static BoxCollider2D Deck(Transform parent,string name,Vector2 center,Vector2 size,int layer)
        {var t=Child(parent,name,center);t.gameObject.layer=layer;var c=t.gameObject.AddComponent<BoxCollider2D>();c.size=size;return c;}
        static Sprite WallPatch(SpriteRenderer backdrop,string id)
        {
            string path=Sprites+id+".asset";var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(existing!=null)return existing;
            // Reuse a nearby strip of the same wall as scene geometry, without editing
            // the background PNG or painting another large dark rectangle beneath it.
            var s=backdrop.sprite;var b=backdrop.bounds;
            float u=(8.4f-b.min.x)/b.size.x,v=(2.22f-b.min.y)/b.size.y;
            var rect=new Rect(s.rect.x+u*s.rect.width,s.rect.y+v*s.rect.height,1.6f/b.size.x*s.rect.width,.40f/b.size.y*s.rect.height);
            var sprite=Sprite.Create(s.texture,rect,new Vector2(0,1),100,0,SpriteMeshType.FullRect);sprite.name=id;AssetDatabase.CreateAsset(sprite,path);return sprite;
        }
        public static void Configure(LevelRoot room)
        {
            Directory.CreateDirectory(Sprites);AssetDatabase.Refresh();
            var floor=room.GetComponentsInChildren<TemporalObject>(true).Single(t=>t.ObjectId=="L03_floor");
            var past=floor.transform.Find("Past");var present=floor.transform.Find("Present");
            var oldPlank=past.Find("Plank");int layer=oldPlank.gameObject.layer;
            foreach(var c in oldPlank.GetComponentsInChildren<Collider2D>(true))c.enabled=false;
            foreach(var sr in oldPlank.GetComponentsInChildren<SpriteRenderer>(true))sr.enabled=false;
            var art=oldPlank.Find("ArtSkin");if(art!=null)art.gameObject.SetActive(false);
            var old=room.GetComponentInChildren<CorridorLooseFloor>(true);if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            var root=Child(past,"LooseFloor_v12",Vector3.zero);var now=Child(present,"LooseFloor_v12",Vector3.zero);
            var loose=Crop("NaturalCracked","PROP_FloorPanel_NaturalCracked_v14.png",new Rect(57,265,2059,185),new Vector2(0,159f/185));
            var solid=Crop("Supported","PROP_FloorPanel_Supported_1994.png",new Rect(58,300,2056,120),new Vector2(0,.95f));
            var beam=Crop("Joist","PROP_FloorJoists_Exposed_2024.png",new Rect(75,95,1625,616),new Vector2(.5f,.994f));
            var edge=Crop("BrokenTip","PROP_FloorEdge_Broken_2024.png",new Rect(1650,279,463,147),new Vector2(0,.92f));
            var stableDeck=Deck(root,"SupportedDeck",new Vector2(6.45f,2.86f),new Vector2(3.9f,.28f),layer);
            Draw(root,"SupportedArt",solid,new Vector3(4.5f,3,0),new Vector2(3.9f,.28f));
            var hinge=Child(root,"LooseHinge",new Vector3(8.4f,3,0));
            Draw(hinge,"CrackedPanelArt",loose,hinge.position,new Vector2(1.6f,.35f),5);
            var deck=Deck(root,"LooseDeck",new Vector2(9.2f,2.86f),new Vector2(1.6f,.28f),layer);
            Draw(now,"ExposedSupport",beam,new Vector3(8.05f,2.66f,0),new Vector2(1.15f,.78f),3);
            Deck(now,"SupportTop",new Vector2(8.05f,2.55f),new Vector2(1.15f,.22f),layer);
            var pastBackdrop=room.transform.Find("ArtRoot/PastBackdrop").GetComponent<SpriteRenderer>();
            var nowBackdrop=room.transform.Find("ArtRoot/PresentBackdrop").GetComponent<SpriteRenderer>();
            var pastGap=Draw(root,"GapReveal",WallPatch(pastBackdrop,"WallGap1994"),new Vector3(8.4f,3.06f,0),new Vector2(1.6f,.42f),-30).gameObject;
            Draw(now,"GapReveal",WallPatch(nowBackdrop,"WallGap2024"),new Vector3(8.4f,3.06f,0),new Vector2(1.6f,.42f),-30);
            GameObject Edges(Transform parent)
            {
                var caps=Child(parent,"BrokenEnds",Vector3.zero);
                Draw(caps,"LeftLip",edge,new Vector3(8.05f,2.97f,0),new Vector2(.35f,.23f),3);
                Draw(caps,"RightLip",edge,new Vector3(10.35f,2.97f,0),new Vector2(.35f,.23f),3,true);return caps.gameObject;
            }
            var pastEdges=Edges(root);Edges(now);pastGap.SetActive(false);pastEdges.SetActive(false);
            var mechanic=floor.gameObject.AddComponent<CorridorLooseFloor>();mechanic.floor=floor;mechanic.hinge=hinge;mechanic.looseDeck=deck;mechanic.supportedDeck=stableDeck;mechanic.pastGap=pastGap;mechanic.pastEdges=pastEdges;
            const string sound="Assets/Game/Audio/SFX/CorridorFloor/";
            mechanic.hollowSound=AssetDatabase.LoadAssetAtPath<AudioClip>(sound+"sfx_floor_probe_hollow.wav");mechanic.creakSound=AssetDatabase.LoadAssetAtPath<AudioClip>(sound+"sfx_floor_loose_creak.wav");mechanic.solidSound=AssetDatabase.LoadAssetAtPath<AudioClip>(sound+"sfx_floor_probe_solid.wav");
            mechanic.breakFallSound=AssetDatabase.LoadAssetAtPath<AudioClip>(sound+"sfx_floor_break_fall_v17.wav");
            if(mechanic.breakFallSound==null)throw new Exception("Generated floor collapse sound missing");
            if(mechanic.hollowSound==null||mechanic.creakSound==null||mechanic.solidSound==null)throw new Exception("Existing floor sounds missing");
            var probe=Child(floor.transform,"ProbeTrigger_v12",new Vector3(8.0f,3.5f,0));probe.gameObject.layer=LayerMask.NameToLayer("Interactable");probe.gameObject.AddComponent<BoxCollider2D>().size=new Vector2(.9f,1.1f);probe.GetComponent<BoxCollider2D>().isTrigger=true;
            // A retry begins on the supported part, never on the falling panel.
            var cp=Child(root,"Checkpoint_cp_L03_floor_approach",new Vector3(7.3f,3.75f,0));var trigger=cp.gameObject.AddComponent<BoxCollider2D>();trigger.size=new Vector2(.8f,1.5f);trigger.isTrigger=true;
            var checkpoint=cp.gameObject.AddComponent<CheckpointMarker>();var config=new SerializedObject(checkpoint);config.FindProperty("checkpointId").stringValue="cp_L03_floor_approach";config.ApplyModifiedPropertiesWithoutUndo();
            var content=room.transform.Find("Content");var spawn=Child(content,"Spawn_cp_L03_floor_approach",new Vector3(7.3f,3.05f,0)).gameObject.AddComponent<SpawnPoint>();
            var sp=new SerializedObject(spawn);sp.FindProperty("spawnId").stringValue="cp_L03_floor_approach";sp.ApplyModifiedPropertiesWithoutUndo();
            var so=new SerializedObject(room);var array=so.FindProperty("spawnPoints");var points=room.GetComponentsInChildren<SpawnPoint>(true);array.arraySize=points.Length;for(int i=0;i<points.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=points[i];so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mechanic);
        }
        [MenuItem("BorrowTomorrow/关卡/接入回廊松板掉落v12（保留照片剧情）")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L03_Corridor.scene");
            var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LevelRoot>(true)).Single();
            Configure(room);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("[CorridorLooseFloor] Old v1 art reused; short cracked terminal panel + safe probe/drop + exposed present support; photo story untouched.");
        }
    }
}
