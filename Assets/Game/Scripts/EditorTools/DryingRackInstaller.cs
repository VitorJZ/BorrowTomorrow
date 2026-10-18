using System;
using System.IO;
using System.Linq;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Level;
using BorrowTomorrow.TimeBoundary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class DryingRackInstaller
    {
        public const string Art = "Assets/Game/Art/Props/DryingRack_v1/";
        private const string Audio = "Assets/Game/Audio/SFX/DryingRack/";
        private static Transform Node(Transform parent, string name, Vector2 at)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = at; return go.transform; }
        private static void Remove(Transform parent, string name)
        { var t = parent.Find(name); if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject); }
        private static SpriteRenderer Image(Transform parent, string name, string file, Vector2 at, int order = 3)
        {
            var n = Node(parent, name, at); var r = n.gameObject.AddComponent<SpriteRenderer>();
            r.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + file + ".png");
            if (r.sprite == null) throw new InvalidOperationException("Missing rack sprite " + file);
            r.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            r.sortingLayerName = "EraPlatform"; r.sortingOrder = order; return r;
        }
        private static void Import()
        {
            foreach (var path in Directory.GetFiles(Art, "*.png"))
            {
                var i = (TextureImporter)AssetImporter.GetAtPath(path);
                i.textureType = TextureImporterType.Sprite; i.spriteImportMode = SpriteImportMode.Single;
                i.spritePixelsPerUnit = 400;
                Vector2 pivot = new Vector2(.5f, .5f);
                if (path.Contains("Mount_1994")) { pivot = new Vector2(.554f, 1-.762f); i.spritePixelsPerUnit = 660; }
                if (path.Contains("Mount_2024")) { pivot = new Vector2(.565f, 1-.770f); i.spritePixelsPerUnit = 660; }
                if (path.Contains("Leaf_1994")) { pivot = new Vector2(.086f, 1-.511f); i.spritePixelsPerUnit = 324; }
                if (path.Contains("Leaf_2024")) { pivot = new Vector2(.075f, 1-.488f); i.spritePixelsPerUnit = 324; }
                if (path.Contains("Brace")) { pivot = new Vector2(.103f, 1-.828f); i.spritePixelsPerUnit = 700; }
                if (path.Contains("Latch_Open")) pivot = new Vector2(.467f, 1-.615f);
                if (path.Contains("Latch_Closed")) pivot = new Vector2(.443f, 1-.615f);
                if (path.Contains("Osmanthus")) { pivot = new Vector2(.375f, .015f); i.spritePixelsPerUnit = 163.5f; }
                var s = new TextureImporterSettings(); i.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect; s.spriteAlignment = (int)SpriteAlignment.Custom; s.spritePivot = pivot;
                i.SetTextureSettings(s); i.alphaIsTransparency = true; i.mipmapEnabled = false;
                i.maxTextureSize = 4096; i.textureCompression = TextureImporterCompression.Uncompressed;
                i.filterMode = FilterMode.Bilinear; i.SaveAndReimport();
            }
        }
        private static LineRenderer Rope(Transform parent, string name)
        {
            var r = Node(parent, name, Vector2.zero).gameObject.AddComponent<LineRenderer>();
            r.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            r.useWorldSpace = true; r.startWidth = r.endWidth = .024f;
            r.startColor = r.endColor = new Color(.36f, .25f, .13f); r.numCapVertices = 3;
            r.sortingLayerName = "EraPlatform"; r.sortingOrder = 6; return r;
        }
        private static BoxCollider2D Support(Transform parent, string name, Vector2 a, Vector2 b, bool oneWay = false)
        {
            Vector2 d = b-a, normal = new Vector2(-d.y,d.x).normalized;
            var n = Node(parent,name,(a+b)*.5f-normal*.09f);
            n.rotation = Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
            n.gameObject.layer=LayerMask.NameToLayer("Ground");
            var box=n.gameObject.AddComponent<BoxCollider2D>(); box.size=new Vector2(d.magnitude+.025f,.18f);
            if(oneWay){box.usedByEffector=true;var e=n.gameObject.AddComponent<PlatformEffector2D>();e.useOneWay=true;}
            return box;
        }
        public static void Configure(LevelRoot room)
        {
            Transform content = room.transform.Find("Content");
            Remove(content,"L01B_DryingRack"); Remove(content,"DryingRackInteraction"); Remove(content,"RackExitBalcony");
            var tree=room.GetComponentsInChildren<TemporalObject>(true).Single(t=>t.ObjectId=="L01B_tree_branch");
            var treeDef=AssetDatabase.LoadAssetAtPath<TemporalObjectDefinition>("Assets/Game/Data/TemporalObjects/L01B_tree_branch.asset");
            treeDef.anchorX=54.4f; EditorUtility.SetDirty(treeDef); tree.EditorConfigure(treeDef);
            var present=tree.transform.Find("Present");
            foreach(Transform child in present.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
            Image(present,"NaturalOsmanthus","PROP_Osmanthus_Natural_2024",new Vector2(54.4f,0),-10);
            // Only the visible near-trunk load-bearing limb is a walking surface.
            Support(present,"BranchA",new Vector2(54.5f,2.2f),new Vector2(55.65f,2.7f));
            Support(present,"BranchB",new Vector2(55.65f,2.7f),new Vector2(56.8f,3.2f));
            Support(present,"BranchC",new Vector2(56.8f,3.2f),new Vector2(58f,3.57f));
            var young=tree.transform.Find("Past/SampleYoungTree");
            if(young!=null) young.position=new Vector3(54.4f,young.position.y,0);
            // A real supported balcony takes over before the branch tapers to twigs.
            var balcony=Node(content,"RackExitBalcony",Vector2.zero);
            var floor=Support(balcony,"BalconyDeck",new Vector2(58.05f,3.3f),new Vector2(68.9f,3.3f),true);
            var wood=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Delivery/Fixed/Strip_Wood.png");
            SpriteRenderer Beam(string name,Vector2 at,Vector2 size)
            {
                var n=Node(balcony,name,at);var r=n.gameObject.AddComponent<SpriteRenderer>();r.sprite=wood;
                r.drawMode=SpriteDrawMode.Sliced;r.size=size;r.sortingLayerName="EraPlatform";r.sortingOrder=-3;return r;
            }
            Beam("DeckTimber",new Vector2(63.48f,3.18f),new Vector2(10.9f,.25f));
            foreach(float x in new[]{59f,63.6f,68f})Beam("BalconyPost",new Vector2(x,1.5f),new Vector2(.16f,3.15f));
            Beam("BackRail",new Vector2(63.5f,4.25f),new Vector2(10.9f,.07f));
            var exit=room.GetComponentsInChildren<ExitDoor>(true).Single();exit.transform.position=new Vector3(68.15f,4.4f,0);
            // Old final platform would catch the player below the new deck; remove it.
            var exitLedge=content.Find("ExitLedge");if(exitLedge!=null)exitLedge.gameObject.SetActive(false);
            string defPath="Assets/Game/Data/TemporalObjects/L01B_drying_rack.asset";
            var def=AssetDatabase.LoadAssetAtPath<TemporalObjectDefinition>(defPath);
            if(def==null){def=ScriptableObject.CreateInstance<TemporalObjectDefinition>();AssetDatabase.CreateAsset(def,defPath);}
            def.objectId="L01B_drying_rack";def.anchorX=50.8f;def.safetyPolicy=SafetyPolicy.FullCheck;
            def.pastState=new TemporalObjectState{stateId="folding_rack",canStand=true};
            def.presentState=new TemporalObjectState{stateId="broken_rack",canStand=false};
            def.anchorHysteresis=.05f;def.switchSfxId="temporal_switch";EditorUtility.SetDirty(def);
            var root=Node(content,"L01B_DryingRack",new Vector2(50.5f,2f));var temporal=root.gameObject.AddComponent<TemporalObject>();temporal.EditorConfigure(def);
            var past=Node(root,"Past",Vector2.zero);var now=Node(root,"Present",Vector2.zero);
            LevelLayouts.SetObject(temporal,"pastRoot",past.gameObject);LevelLayouts.SetObject(temporal,"presentRoot",now.gameObject);
            Vector2 hinge=new Vector2(50.45f,1.9f);
            Image(past,"FixedMount","PROP_RackMount_1994",hinge,4);
            Image(now,"BrokenMount","PROP_RackMount_2024",hinge,4);
            var leaf=Node(past,"LeafPivot",hinge);Image(leaf,"WoodenDeck","PROP_RackLeaf_1994",hinge,5);
            var deck=leaf.gameObject.AddComponent<BoxCollider2D>();leaf.gameObject.layer=LayerMask.NameToLayer("Ground");
            deck.size=new Vector2(4.12f,.20f);deck.offset=new Vector2(2.06f,.155f);deck.enabled=false;
            // The existing ledge is y=2.0; a visible short hinge ramp avoids
            // a vertical lip at the thicker new deck's y=2.155 walking surface.
            var approach=Support(past,"HingeApproach",new Vector2(50.0f,2f),new Vector2(50.6f,2.155f));
            var approachArt=approach.gameObject.AddComponent<SpriteRenderer>();approachArt.sprite=wood;
            approachArt.drawMode=SpriteDrawMode.Sliced;approachArt.size=approach.size;
            approachArt.sortingLayerName="EraPlatform";approachArt.sortingOrder=4;
            var broken=Image(now,"BrokenLeaf","PROP_RackLeaf_2024",hinge,5);broken.transform.rotation=Quaternion.Euler(0,0,-73);
            var guide=Node(past,"Guide",new Vector2(50.47f,3f));
            var eye=Node(leaf,"RopeEye",hinge);eye.localPosition=new Vector3(3.98f,.24f,0);
            var handle=Node(past,"PullEnd",new Vector2(49.85f,2.9f));
            var foot=Node(past,"BraceFoot",new Vector2(50.45f,1.1f));
            var braceEye=Node(leaf,"BraceEye",hinge);braceEye.localPosition=new Vector3(2.2f,-.04f,0);
            var brace=Image(past,"SupportArm","PROP_RackBrace_1994",foot.position,3);
            var latch=Image(past,"Latch","PROP_RackLatch_Open",new Vector2(50.50f,1.85f),7);latch.transform.localScale=Vector3.one*.16f;
            var line=Rope(past,"LiftRope");var handLine=Rope(past,"PullRope");
            var interaction=Node(content,"DryingRackInteraction",new Vector2(49.7f,2.65f));
            interaction.gameObject.layer=LayerMask.NameToLayer("Interactable");
            var trigger=interaction.gameObject.AddComponent<BoxCollider2D>();trigger.isTrigger=true;trigger.size=new Vector2(.8f,1.1f);
            var rack=interaction.gameObject.AddComponent<FoldingDryingRack>();
            var so=new SerializedObject(rack);
            void Ref(string name,UnityEngine.Object value)=>so.FindProperty(name).objectReferenceValue=value;
            Ref("rackObject",temporal);Ref("leaf",leaf);Ref("deck",deck);Ref("brace",brace.transform);Ref("braceFoot",foot);Ref("braceEye",braceEye);
            so.FindProperty("braceSpriteLength").floatValue=Vector2.Distance(new Vector2(.103f,.828f),new Vector2(.890f,.180f))*1254/700f;
            Ref("latch",latch);Ref("latchOpen",latch.sprite);Ref("latchClosed",AssetDatabase.LoadAssetAtPath<Sprite>(Art+"PROP_RackLatch_Closed.png"));
            Ref("liftRope",line);Ref("pullRope",handLine);Ref("guide",guide);Ref("ropeEye",eye);Ref("pullEnd",handle);
            foreach(var pair in new[]{("pullSound","sfx_rack_rope_pull"),("creakSound","sfx_rack_raise_creak"),("lockSound","sfx_rack_latch_lock"),("brokenSound","sfx_rack_broken_rattle")})
                Ref(pair.Item1,AssetDatabase.LoadAssetAtPath<AudioClip>(Audio+pair.Item2+".wav"));
            so.ApplyModifiedPropertiesWithoutUndo();leaf.localRotation=Quaternion.Euler(0,0,-78);brace.gameObject.SetActive(false);
            line.positionCount=2;line.SetPosition(0,guide.position);line.SetPosition(1,eye.position);
            handLine.positionCount=2;handLine.SetPosition(0,guide.position);handLine.SetPosition(1,handle.position);
            foreach(var h in room.Definition.hintGroups??Array.Empty<HintGroup>())
                if(h.stageKey=="HintPassed_L01B_Climb")
                {h.hintLevel1="木梯旁的平台上有一套晾晒架。";h.hintLevel2="过去能拉起晾晒架，现在的桂花树有粗枝。";h.hintLevel3="爬过去的木梯，在平台按 E 拉起晾晒架，再把边界停在装置与树干锚点之间，借粗枝走上窗台。";}
            EditorUtility.SetDirty(room.Definition);
            Debug.Log("[DryingRack] Installed past-only folding deck, natural tree and short-branch route; no character animation.");
        }
        [MenuItem("BorrowTomorrow/关卡/接入桂花树晾晒架")]
        public static void Install()
        {
            AssetDatabase.Refresh();Import();
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L01_Courtyard.scene");
            Configure(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LevelRoot>(true)).Single(r=>r.RoomId=="L01B"));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Art+"installed.txt","L01B drying rack enabled");AssetDatabase.Refresh();
        }
        public static void InstallAndBuild(){Install();JumpRecoveryInstaller.BuildCurrent();}
    }
}
