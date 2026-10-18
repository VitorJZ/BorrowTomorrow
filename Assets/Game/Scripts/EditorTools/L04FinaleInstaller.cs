using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Level;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.Player;
using BorrowTomorrow.TimeBoundary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>
    /// L04 终局关重做（三代人的路，见 Docs/18）：
    /// 1994 外婆的竹梯 → 2024 后装的铁爬梯 → 种了三十年的桂花树。
    /// 标准解：推边界过竹梯(过去)上腰檐 → 拉回铁梯与树干之间(铁梯现在、树仍是幼苗)上墀头 →
    /// 站在墀头上把边界拉过树干，树长大、长枝挡住铁梯同时接到暗格。
    /// 坐标与 Art/Delivery/L04v3 背景逐项配准（背景以 32.24×18.14 世界单位铺满取景，底边 y=-3.6）。
    /// 只改 L04 场景与 L04 数据，可重复执行。
    /// </summary>
    public static class L04FinaleInstaller
    {
        private const string ArtDir = "Assets/Game/Art/Delivery/L04v3/";
        private const string SourceDir = "ArtRegen_2026-10-18/L04v3/final/";

        // ---------------------------------------------------------------- 布局（世界坐标）
        internal const float Yard = -1.79f;
        internal const float TreeAnchor = 14.3f, MetalAnchor = 18.8f, BambooAnchor = 25.2f;
        internal const float BoundaryMin = 0f, BoundaryMax = 30.5f, InitialBoundary = 16.5f;

        // 恒定站立面：x0, x1, 顶面 y
        internal static readonly Vector3 Sill = new Vector3(18.5f, 26.8f, 2.11f);      // 阁楼窗台线脚
        internal static readonly Vector3 Cornice = new Vector3(17.9f, 27.75f, 4.87f);  // 腰檐（单向，梯子穿过）
        internal static readonly Vector3 Corbel = new Vector3(17.9f, 20.05f, 7.45f);   // 左上墀头（单向）
        internal static readonly Vector3 NicheLedge = new Vector3(24.4f, 27.1f, 7.63f); // 暗格小窗台
        internal static readonly Vector2 Niche = new Vector2(25.72f, 8.45f);

        // 成年长枝上沿（从墙角到暗格小台）：与 PROP_L04_TreeAdult 实测枝面一致。
        internal static readonly Vector2 BranchStart = new Vector2(17.55f, 5.45f);
        internal static readonly Vector2 BranchEnd = new Vector2(24.55f, 7.63f);
        private const float BranchThickness = .45f;

        // 成年树贴图：裁切图中树根中心像素与像素→世界比例（见 Docs/18 配准表）。
        private const float TreePxScale = .01454f;
        private static readonly Vector2 TreeRootPx = new Vector2(540f, 1023f);

        private static readonly Rect CameraBounds = new Rect(0f, -3.25f, 31f, 17.44f);
        private const float BgWidth = 32.24f, BgCenterY = 5.47f;

        // ---------------------------------------------------------------- 入口

        [MenuItem("BorrowTomorrow/重做 L04 终局关（竹梯·铁梯·桂花树）")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                ImportArt();
                var defs = UpdateTemporalDefinitions();
                UpdateLevelDefinition();
                var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/L04_Attic.scene", OpenSceneMode.Single);
                var room = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelRoot>(true)).First(r => r.RoomId == "L04");
                Rebuild(room, defs);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[L04Finale] installed");
        }

        // ---------------------------------------------------------------- 资源

        private static void ImportArt()
        {
            Directory.CreateDirectory(ArtDir);
            if (Directory.Exists(SourceDir))
                foreach (string src in Directory.GetFiles(SourceDir, "*.png"))
                    File.Copy(src, ArtDir + Path.GetFileName(src), true);
            AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles(ArtDir, "*.png").Select(p => p.Replace('\\', '/')))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool bg = Path.GetFileName(path).StartsWith("BG_", StringComparison.Ordinal);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 2048;
                importer.textureCompression = bg ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static Sprite Art(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + name + ".png");
            if (sprite == null) throw new FileNotFoundException("Missing L04 art " + name);
            return sprite;
        }

        // ---------------------------------------------------------------- 数据

        private static Dictionary<string, TemporalObjectDefinition> UpdateTemporalDefinitions()
        {
            var map = new Dictionary<string, TemporalObjectDefinition>();
            var specs = new (string id, float anchor, SafetyPolicy policy, string past, bool pastStand, bool pastBlock, string present, bool presentStand, bool presentBlock)[]
            {
                ("L04_tree", TreeAnchor, SafetyPolicy.FullCheck, "sapling", false, false, "adult_tree_branch", true, true),
                ("L04_metal_ladder", MetalAnchor, SafetyPolicy.FullCheck, "absent", false, false, "iron_ladder", false, false),
                ("L04_bamboo_ladder", BambooAnchor, SafetyPolicy.FullCheck, "bamboo_ladder", false, false, "absent", false, false),
            };
            foreach (var s in specs)
            {
                string path = "Assets/Game/Data/TemporalObjects/" + s.id + ".asset";
                var def = AssetDatabase.LoadAssetAtPath<TemporalObjectDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<TemporalObjectDefinition>();
                    AssetDatabase.CreateAsset(def, path);
                }
                def.objectId = s.id;
                def.anchorX = s.anchor;
                def.safetyPolicy = s.policy;
                def.anchorHysteresis = .05f;
                def.switchSfxId = "temporal_switch";
                def.pastState = new TemporalObjectState { stateId = s.past, canStand = s.pastStand, blocksMovement = s.pastBlock };
                def.presentState = new TemporalObjectState { stateId = s.present, canStand = s.presentStand, blocksMovement = s.presentBlock };
                EditorUtility.SetDirty(def);
                map[s.id] = def;
            }
            return map;
        }

        private static void UpdateLevelDefinition()
        {
            var def = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Game/Data/Levels/L04_L04_Attic.asset");
            def.boundaryMin = BoundaryMin;
            def.boundaryMax = BoundaryMax;
            def.initialBoundary = InitialBoundary;
            EditorUtility.SetDirty(def);
        }

        // ---------------------------------------------------------------- 场景

        /// <summary>供其它安装器（如交付美术接入）重建 L04 时调用，保证不会退回旧布局。</summary>
        internal static void RebuildRoom(LevelRoot room)
        {
            ImportArt();
            var defs = UpdateTemporalDefinitions();
            UpdateLevelDefinition();
            Rebuild(room, defs);
        }

        internal static void Rebuild(LevelRoot room, Dictionary<string, TemporalObjectDefinition> defs)
        {
            var friction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Game/Data/Physics/GroundFriction.physicMaterial");
            int playerMask = 1 << LayerMask.NameToLayer("Player");

            Transform content = room.transform.Find("Content");
            for (int i = content.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(content.GetChild(i).gameObject);

            // 恒定结构：站立面只放碰撞，画面来自背景。
            Solid(content, "WindowSill", Sill, .35f, friction, false);
            Solid(content, "Cornice", Cornice, .4f, friction, true);
            Solid(content, "Corbel", Corbel, .35f, friction, true);
            Solid(content, "NicheLedge", NicheLedge, .35f, friction, false);
            Wall(content, "WallL", 0f, friction);
            Wall(content, "WallR", 30.8f, friction);

            // 年代物体
            var tree = Temporal(content, defs["L04_tree"]);
            var treePast = Era(tree, "Past");
            var treePresent = Era(tree, "Present");
            ArtDeliveryInstaller.PutByBounds(treePast.transform, "Sapling", Art("PROP_L04_Sapling_1994"),
                new Vector2(TreeAnchor - .15f, Yard - .05f), 1.35f, "EraPlatform", -10);
            PlaceTree(treePresent.transform);
            Branch(treePresent.transform, friction);
            Link(tree, treePast, treePresent);

            var metal = Temporal(content, defs["L04_metal_ladder"]);
            var metalPast = Era(metal, "Past");
            var metalPresent = Era(metal, "Present");
            Ladder(metalPresent.transform, "MetalLadder", Art("PROP_L04_MetalLadder_2024"), MetalAnchor, Cornice.z, Corbel.z, .78f);
            Link(metal, metalPast, metalPresent);

            var bamboo = Temporal(content, defs["L04_bamboo_ladder"]);
            var bambooPast = Era(bamboo, "Past");
            var bambooPresent = Era(bamboo, "Present");
            Ladder(bambooPast.transform, "BambooLadder", Art("PROP_L04_BambooLadder_1994"), BambooAnchor - .05f, Sill.z, Cornice.z, .98f);
            Link(bamboo, bambooPast, bambooPresent);

            // 跌到院子 = 回最近检查点。
            var kill = new GameObject("KillZone");
            kill.transform.SetParent(content, false);
            kill.transform.position = new Vector3(15f, Yard + .2f, 0f);
            kill.layer = LayerMask.NameToLayer("KillZone");
            var killCol = kill.AddComponent<BoxCollider2D>();
            killCol.isTrigger = true; killCol.size = new Vector2(40f, 1f);
            LevelLayouts.SetField(kill.AddComponent<KillZone>(), "playerMask", playerMask);

            // 出生点与检查点
            var spawns = new List<SpawnPoint>
            {
                Spawn(content, "spawn_attic_window", new Vector2(20.4f, Sill.z)),
                Spawn(content, "cp_L04_window", new Vector2(20.4f, Sill.z)),
                Spawn(content, "cp_L04_cornice", new Vector2(23.4f, Cornice.z)),
                Spawn(content, "cp_L04_eave", new Vector2(18.9f, Corbel.z)),
            };
            Checkpoint(content, "cp_L04_window", new Vector2(20.4f, Sill.z), playerMask);
            Checkpoint(content, "cp_L04_cornice", new Vector2(23.4f, Cornice.z), playerMask);
            Checkpoint(content, "cp_L04_eave", new Vector2(18.9f, Corbel.z), playerMask);
            var so = new SerializedObject(room);
            var sp = so.FindProperty("spawnPoints");
            sp.arraySize = spawns.Count;
            for (int i = 0; i < spawns.Count; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
            so.FindProperty("overrideBoundaryRange").boolValue = true;
            so.FindProperty("boundaryMin").floatValue = BoundaryMin;
            so.FindProperty("boundaryMax").floatValue = BoundaryMax;
            so.FindProperty("initialBoundary").floatValue = InitialBoundary;
            so.FindProperty("cameraBounds").rectValue = CameraBounds;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 双年代背景（外立面画在背景里，随边界连续切开）。
            var backdrop = room.GetComponentInChildren<EraArtBackdrop>(true);
            foreach (var pair in new[] { (backdrop.past, "BG_L04_Attic_1994_v3"), (backdrop.present, "BG_L04_Attic_2024_v3") })
            {
                var sprite = Art(pair.Item2);
                pair.Item1.drawMode = SpriteDrawMode.Simple;
                pair.Item1.sprite = sprite;
                pair.Item1.color = Color.white;
                pair.Item1.transform.position = new Vector3(15.5f, BgCenterY, 0f);
                float scale = BgWidth / sprite.bounds.size.x;
                pair.Item1.transform.localScale = new Vector3(scale, scale, 1f);
                EditorUtility.SetDirty(pair.Item1);
            }

            ConfigureNarrative(room);
            LevelLayouts.SetField(room.GetComponent<LevelDirector>(), "completeLevelOnFlag", StoryFlags.BoxLocated);
            EditorUtility.SetDirty(room);
        }

        /// <summary>叙事物跟随新布局：窗边观察在起点窗洞；暗格在暗格小台上方；树长大的感叹只在高处（墀头/长枝）播放。</summary>
        private static void ConfigureNarrative(LevelRoot room)
        {
            Transform narrative = room.transform.Find("NarrativeContent");
            if (narrative == null) return;
            var boards = narrative.Find("Narrative_window_boards");
            if (boards != null) boards.position = new Vector3(20.9f, Sill.z + .8f, 0f);
            var niche = narrative.Find("Narrative_niche");
            if (niche != null)
            {
                niche.position = new Vector3(Niche.x, Niche.y, 0f);
                var col = niche.GetComponent<BoxCollider2D>();
                if (col != null) col.size = new Vector2(1.3f, 1.4f);
                var sr = niche.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;
            }
            foreach (var auto in narrative.GetComponentsInChildren<NarrativeAutoEvent>(true))
            {
                if (auto.presentMechanism == "L04_tree") auto.minimumY = Corbel.z - 1.6f;
                EditorUtility.SetDirty(auto);
            }
        }

        // ---------------------------------------------------------------- 构建件

        private static void Solid(Transform parent, string name, Vector3 span, float thickness, PhysicsMaterial2D friction, bool oneWay)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((span.x + span.y) * .5f, span.z - thickness * .5f, 0f);
            go.layer = LayerMask.NameToLayer("Ground");
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(span.y - span.x, thickness);
            col.sharedMaterial = friction;
            if (oneWay)
            {
                col.usedByEffector = true;
                go.AddComponent<PlatformEffector2D>().useOneWay = true;
            }
        }

        private static void Wall(Transform parent, string name, float x, PhysicsMaterial2D friction)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, 6f, 0f);
            go.layer = LayerMask.NameToLayer("Ground");
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(.6f, 20f);
            col.sharedMaterial = friction;
        }

        private static TemporalObject Temporal(Transform parent, TemporalObjectDefinition def)
        {
            var go = new GameObject("Temporal_" + def.objectId);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(def.anchorX, Yard, 0f);
            go.layer = LayerMask.NameToLayer("Temporal");
            var t = go.AddComponent<TemporalObject>();
            t.EditorConfigure(def);
            return t;
        }

        private static GameObject Era(TemporalObject host, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(host.transform, false);
            go.layer = host.gameObject.layer;
            return go;
        }

        private static void Link(TemporalObject host, GameObject past, GameObject present)
        {
            LevelLayouts.SetObject(host, "pastRoot", past);
            LevelLayouts.SetObject(host, "presentRoot", present);
        }

        /// <summary>成年桂花树：按树根像素与实测比例摆放（贴图中心为轴心）。</summary>
        private static void PlaceTree(Transform parent)
        {
            var sprite = Art("PROP_L04_TreeAdult_2024");
            float wPx = sprite.texture.width, hPx = sprite.texture.height;
            var go = new GameObject("AdultTree");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(
                TreeAnchor + (wPx * .5f - TreeRootPx.x) * TreePxScale,
                Yard + (TreeRootPx.y - hPx * .5f) * TreePxScale, 0f);
            float s = TreePxScale * sprite.pixelsPerUnit;
            go.transform.localScale = new Vector3(s, s, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sortingLayerName = "EraPlatform"; sr.sortingOrder = -10;
        }

        /// <summary>长枝碰撞：上沿从墙角到暗格小台。它在墙前横过铁梯（站在腰檐左段会被覆盖→安全阻挡/挡住攀爬），
        /// 同时是从墀头走到暗格的唯一通路。</summary>
        private static void Branch(Transform parent, PhysicsMaterial2D friction)
        {
            var go = new GameObject("BranchWalk");
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            Vector2 d = BranchEnd - BranchStart;
            Vector2 n = new Vector2(-d.y, d.x).normalized;
            go.transform.position = (BranchStart + BranchEnd) * .5f - n * (BranchThickness * .5f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(d.magnitude, BranchThickness);
            col.sharedMaterial = friction;
        }

        /// <summary>梯子：贴图底边落在下层站立面，顶部略高于上层；攀爬触发区超出上层 0.7（与 LevelLayouts.LadderContent 一致）。</summary>
        private static void Ladder(Transform parent, string name, Sprite sprite, float x, float bottom, float top, float width)
        {
            ArtDeliveryInstaller.PutByBounds(parent, name, sprite, new Vector2(x, bottom), width, "EraPlatform", 3);
            var zone = new GameObject("LadderZone");
            zone.transform.SetParent(parent, false);
            float zBottom = bottom + .2f, zTop = top + .7f;
            zone.transform.position = new Vector3(x, (zBottom + zTop) * .5f, 0f);
            zone.layer = LayerMask.NameToLayer("Ladder");
            var trigger = zone.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(.8f, zTop - zBottom);
            zone.AddComponent<LadderZone>();
        }

        private static SpawnPoint Spawn(Transform parent, string id, Vector2 feet)
        {
            var go = new GameObject("Spawn_" + id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(feet.x, feet.y + .05f, 0f);
            var p = go.AddComponent<SpawnPoint>();
            LevelLayouts.SetField(p, "spawnId", id);
            LevelLayouts.SetField(p, "placementOffset", Vector2.zero);
            return p;
        }

        private static void Checkpoint(Transform parent, string id, Vector2 feet, int playerMask)
        {
            var go = new GameObject("Checkpoint_" + id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(feet.x, feet.y + .8f, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true; col.size = new Vector2(1.2f, 1.6f);
            var marker = go.AddComponent<CheckpointMarker>();
            LevelLayouts.SetField(marker, "checkpointId", id);
            LevelLayouts.SetField(marker, "playerMask", playerMask);
        }
    }
}
