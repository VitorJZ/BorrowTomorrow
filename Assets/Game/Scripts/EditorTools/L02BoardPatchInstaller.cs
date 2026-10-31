#if UNITY_EDITOR
using System;
using System.Linq;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Level;
using BorrowTomorrow.TimeBoundary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>
    /// L02 外台补板改造（策划 7.4 增补）：
    /// 把现在态维修踏台改成右端缺板的维修架，并在 1994 墙边加入旧木板。
    /// 边界停在木板锚点 9 与维修架锚点 12 之间时，两件物件同时存在，按 E 把木板搭上缺口。
    /// 只改 L02 外台，不动室内与剧情数据；可重复执行。
    /// </summary>
    public static class L02BoardPatchInstaller
    {
        private const string ScenePath = "Assets/Game/Scenes/L02_Sewing.scene";
        private const string PlankSpritePath = "Assets/Game/Art/Props/Temporal/PROP_Plank_1994.png";
        private const string MetalStripPath = "Assets/Game/Art/Delivery/Fixed/Strip_Metal.png";
        private const string MaintenanceDefPath = "Assets/Game/Data/TemporalObjects/L02_maintenance.asset";
        private const string PlankDefPath = "Assets/Game/Data/TemporalObjects/L02_plank.asset";
        private const string LiftSoundPath = "Assets/Game/Audio/SFX/DryingRack/sfx_rack_raise_creak.wav";
        private const string SettleSoundPath = "Assets/Game/Audio/SFX/DryingRack/sfx_rack_latch_lock.wav";
        private const float PlankWorldWidth = 3.6f;
        private const float PlankAngle = 58f;
        private static readonly Vector3 PlankRoot = new Vector3(8.45f, 1.5f, 0f);
        private static readonly Vector3 PlankInteract = new Vector3(9.2f, 3.3f, 0f);
        private static readonly Vector3 GapCenter = new Vector3(6.9f, 2.75f, 0f);
        private static readonly Vector2 GapSize = new Vector2(3.2f, 0.5f);
        private static readonly Vector2 PlatformLeftSize = new Vector2(4.3f, 0.5f);
        private static readonly Vector3 PlatformLeftCenter = new Vector3(3.15f, 2.75f, 0f);

        [MenuItem("BorrowTomorrow/关卡/L02 外台补板改造")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) EditorSceneManager.SaveScene(SceneManager.GetSceneAt(i));
            try
            {
                ImportSprite();
                var plankSprite = RequireSprite(PlankSpritePath);
                var metalSprite = RequireSprite(MetalStripPath);
                var plankDef = CreateOrUpdatePlankDefinition();
                var maintenanceDef = AssetDatabase.LoadAssetAtPath<TemporalObjectDefinition>(MaintenanceDefPath);
                if (maintenanceDef == null) throw new InvalidOperationException("Missing " + MaintenanceDefPath);
                maintenanceDef.anchorX = 12f;
                EditorUtility.SetDirty(maintenanceDef);

                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var room = scene.GetRootGameObjects()
                    .SelectMany(g => g.GetComponentsInChildren<LevelRoot>(true))
                    .Single(r => r.RoomId == "L02");
                var maintenance = room.GetComponentsInChildren<TemporalObject>(true)
                    .Single(t => t.ObjectId == "L02_maintenance");
                var present = maintenance.transform.Find("Present");
                if (present == null) throw new InvalidOperationException("Missing L02 maintenance Present root");
                ConfigurePlatform(present, metalSprite);
                var gapBoard = ConfigureGapBoard(present, plankSprite);
                var benchBoard = ConfigureBenchBoard(room, maintenance, plankSprite);
                ConfigurePlank(room, maintenance, plankDef, plankSprite, gapBoard, benchBoard);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[L02BoardPatch] Exterior refit complete: 1994 plank bridges the 2024 maintenance gap.");
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void ConfigurePlatform(Transform present, Sprite metalSprite)
        {
            var platform = present.Find("Platform");
            if (platform == null) platform = present.Find("PlatformLeft");
            if (platform == null) throw new InvalidOperationException("Missing L02 maintenance Platform");
            platform.name = "PlatformLeft";
            platform.localPosition = PlatformLeftCenter - present.position;
            platform.localRotation = Quaternion.identity;
            platform.localScale = Vector3.one;
            var collider = platform.GetComponent<BoxCollider2D>();
            if (collider == null) throw new InvalidOperationException("PlatformLeft needs a BoxCollider2D");
            collider.offset = Vector2.zero;
            collider.size = PlatformLeftSize;
            var own = platform.GetComponent<SpriteRenderer>();
            if (own != null) own.enabled = false;
            var art = platform.Find("ArtSkin");
            if (art == null)
            {
                var go = new GameObject("ArtSkin");
                go.transform.SetParent(platform, false);
                art = go.transform;
            }
            art.localPosition = Vector3.zero;
            art.localRotation = Quaternion.identity;
            art.localScale = new Vector3(PlatformLeftSize.x / metalSprite.bounds.size.x,
                PlatformLeftSize.y / metalSprite.bounds.size.y, 1f);
            var renderer = art.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = art.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = metalSprite;
            renderer.sortingLayerName = "EraPlatform";
            renderer.sortingOrder = 2;
        }

        /// <summary>砖台上的旧木板：1994 年玩家搬上来，2024 年仍在这里；普通物体，按旗标显示。</summary>
        private static GameObject ConfigureBenchBoard(LevelRoot room, TemporalObject maintenance, Sprite plankSprite)
        {
            var parent = maintenance.transform.parent;
            var bench = parent.Find("BenchBoard");
            if (bench == null)
            {
                var go = new GameObject("BenchBoard");
                go.transform.SetParent(parent, false);
                go.layer = maintenance.gameObject.layer;
                bench = go.transform;
            }
            bench.position = new Vector3(9.6f, 3.2f, 0f);
            bench.localRotation = Quaternion.identity;
            float scale = 3.6f / plankSprite.bounds.size.x;
            bench.localScale = new Vector3(scale, scale, 1f);
            var renderer = bench.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = bench.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = plankSprite;
            renderer.sortingLayerName = "EraPlatform";
            renderer.sortingOrder = 1;
            bench.gameObject.SetActive(false);
            return bench.gameObject;
        }

        private static GameObject ConfigureGapBoard(Transform present, Sprite plankSprite)
        {
            var gap = present.Find("GapBoard");
            if (gap == null)
            {
                var go = new GameObject("GapBoard");
                go.transform.SetParent(present, false);
                go.layer = present.gameObject.layer;
                gap = go.transform;
            }
            gap.localPosition = GapCenter - present.position;
            gap.localRotation = Quaternion.identity;
            gap.localScale = Vector3.one;
            var collider = gap.GetComponent<BoxCollider2D>();
            if (collider == null) collider = gap.gameObject.AddComponent<BoxCollider2D>();
            var platformLeft = present.Find("PlatformLeft");
            var platformCollider = platformLeft != null ? platformLeft.GetComponent<BoxCollider2D>() : null;
            collider.offset = Vector2.zero;
            collider.size = GapSize;
            collider.sharedMaterial = platformCollider != null ? platformCollider.sharedMaterial : null;
            var own = gap.GetComponent<SpriteRenderer>();
            if (own != null) own.enabled = false;
            var art = gap.Find("ArtSkin");
            if (art == null)
            {
                var go = new GameObject("ArtSkin");
                go.transform.SetParent(gap, false);
                art = go.transform;
            }
            float scale = PlankWorldWidth / plankSprite.bounds.size.x;
            art.localPosition = new Vector3(0f, .02f, 0f);
            art.localRotation = Quaternion.identity;
            art.localScale = new Vector3(scale, scale, 1f);
            var renderer = art.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = art.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = plankSprite;
            renderer.sortingLayerName = "EraPlatform";
            renderer.sortingOrder = 3;
            // 2024 年才铺上的木板已经在外面放了三十年，比 1994 年时更灰旧。
            renderer.color = new Color(.84f, .81f, .75f, 1f);
            gap.gameObject.SetActive(false);
            return gap.gameObject;
        }

        private static void ConfigurePlank(LevelRoot room, TemporalObject maintenance,
            TemporalObjectDefinition plankDef, Sprite plankSprite, GameObject gapBoard, GameObject benchBoard)
        {
            var parent = maintenance.transform.parent;
            var host = parent.Find("Temporal_L02_plank");
            if (host == null)
            {
                var go = new GameObject("Temporal_L02_plank");
                go.transform.SetParent(parent, false);
                host = go.transform;
            }
            host.gameObject.layer = maintenance.gameObject.layer;
            host.position = PlankRoot;
            var temporal = host.GetComponent<TemporalObject>();
            if (temporal == null) temporal = host.gameObject.AddComponent<TemporalObject>();
            temporal.EditorConfigure(plankDef);
            var past = host.Find("Past");
            if (past == null)
            {
                var go = new GameObject("Past");
                go.transform.SetParent(host, false);
                past = go.transform;
            }
            past.localPosition = Vector3.zero;
            past.gameObject.layer = host.gameObject.layer;
            var present = host.Find("Present");
            if (present == null)
            {
                var go = new GameObject("Present");
                go.transform.SetParent(host, false);
                present = go.transform;
            }
            present.localPosition = Vector3.zero;
            present.gameObject.layer = host.gameObject.layer;
            var art = past.Find("ArtSkin");
            if (art == null)
            {
                var go = new GameObject("ArtSkin");
                go.transform.SetParent(past, false);
                art = go.transform;
            }
            float scale = PlankWorldWidth / plankSprite.bounds.size.x;
            art.localPosition = Vector3.zero;
            art.localRotation = Quaternion.Euler(0f, 0f, PlankAngle);
            art.localScale = new Vector3(scale, scale, 1f);
            var renderer = art.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = art.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = plankSprite;
            renderer.sortingLayerName = "EraPlatform";
            renderer.sortingOrder = 3;

            var interact = host.Find("InteractPoint");
            if (interact == null)
            {
                var go = new GameObject("InteractPoint");
                go.transform.SetParent(host, false);
                interact = go.transform;
            }
            // 交互触发体必须在 Interactable 层，PlayerInteractor 只看这一层。
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            interact.gameObject.layer = interactableLayer >= 0 ? interactableLayer : host.gameObject.layer;
            interact.position = PlankInteract;
            var trigger = interact.GetComponent<BoxCollider2D>();
            if (trigger == null) trigger = interact.gameObject.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(3.0f, 1.6f);
            trigger.isTrigger = true;

            var serialized = new SerializedObject(temporal);
            serialized.FindProperty("pastRoot").objectReferenceValue = past.gameObject;
            serialized.FindProperty("presentRoot").objectReferenceValue = present.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var patch = host.GetComponent<L02BoardPatch>();
            if (patch == null) patch = host.gameObject.AddComponent<L02BoardPatch>();
            var patchSo = new SerializedObject(patch);
            patchSo.FindProperty("boardObject").objectReferenceValue = temporal;
            patchSo.FindProperty("maintenanceObject").objectReferenceValue = maintenance;
            patchSo.FindProperty("boardVisual").objectReferenceValue = art;
            patchSo.FindProperty("benchBoard").objectReferenceValue = benchBoard;
            patchSo.FindProperty("gapBoard").objectReferenceValue = gapBoard;
            patchSo.FindProperty("liftSound").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(LiftSoundPath);
            patchSo.FindProperty("settleSound").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(SettleSoundPath);
            patchSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TemporalObjectDefinition CreateOrUpdatePlankDefinition()
        {
            var def = AssetDatabase.LoadAssetAtPath<TemporalObjectDefinition>(PlankDefPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<TemporalObjectDefinition>();
                AssetDatabase.CreateAsset(def, PlankDefPath);
            }
            def.objectId = "L02_plank";
            def.anchorX = 9f;
            def.pastState = new TemporalObjectState { stateId = "visible" };
            def.presentState = new TemporalObjectState { stateId = "absent" };
            def.safetyPolicy = SafetyPolicy.Ignore;
            def.anchorHysteresis = .05f;
            def.switchSfxId = "temporal_switch";
            EditorUtility.SetDirty(def);
            return def;
        }

        private static Sprite RequireSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Missing sprite: " + path);
            return sprite;
        }

        private static void ImportSprite()
        {
            AssetDatabase.ImportAsset(PlankSpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(PlankSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            foreach (var platform in new[] { "Standalone", "iPhone", "Android", "WebGL" })
                importer.ClearPlatformTextureSettings(platform);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
#endif
