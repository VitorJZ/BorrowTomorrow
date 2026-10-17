using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>
    /// 项目脚手架（供比赛期团队快速起步与重建）：
    /// 1. 配置物理层、排序层（策划 8.5 图层）与构建设置；
    /// 2. 创建 Bootstrap / MainMenu / L00–L04 / Epilogue 场景骨架与系统根装配；
    /// 3. 按“策划方案第 7 章”的灰盒坐标生成 LevelDefinition 与示例年代物体。
    /// 可从菜单运行，也可命令行批处理运行：
    /// Tuanjie.exe -batchmode -quit -projectPath &lt;项目&gt; -executeMethod BorrowTomorrow.EditorTools.ProjectScaffolder.RunFromCommandLine
    /// </summary>
    public static class ProjectScaffolder
    {
        private const string ScenesDir = "Assets/Game/Scenes";
        private const string DataDir = "Assets/Game/Data";
        private const string ArtUiDir = "Assets/Game/Art/UI";

        [MenuItem("BorrowTomorrow/一键搭建项目骨架")]
        public static void ScaffoldAll()
        {
            SetupProjectSettings();
            string whiteSprite = EnsureWhiteRectSprite();
            LevelDefinition[] catalog = CreateLevelDefinitions();
            CreateBootstrapScene();
            CreateMainMenuScene();
            CreateLevelScenes(catalog, whiteSprite);
            SetBuildSceneList(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectScaffolder] 项目骨架搭建完成。");
        }

        public static void RunFromCommandLine()
        {
            ScaffoldAll();
        }

        // ---------------------------------------------------------------- 设置

        private static void SetupProjectSettings()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[ProjectScaffolder] 找不到 TagManager.asset。");
                return;
            }
            var tagManager = new SerializedObject(assets[0]);

            // 物理层（User Layer 8 起）。
            SerializedProperty layers = tagManager.FindProperty("layers");
            SetLayerName(layers, 8, "Player");
            SetLayerName(layers, 9, "Ground");
            SetLayerName(layers, 10, "Interactable");
            SetLayerName(layers, 11, "Temporal");
            SetLayerName(layers, 12, "KillZone");
            SetLayerName(layers, 13, "Ladder");

            // 排序层（策划 8.5：远景天空→…→边界特效；UI 由 Canvas 处理）。
            SerializedProperty sortingLayers = tagManager.FindProperty("m_SortingLayers");
            string[] order =
            {
                "Sky", "FarBuildings", "EraBackground", "ConstantStructure",
                "EraPlatform", "Characters", "Foreground", "BoundaryFx",
            };
            sortingLayers.arraySize = 0;
            foreach (string layer in order)
            {
                SerializedProperty appended = AddArrayElement(sortingLayers);
                appended.FindPropertyRelative("name").stringValue = layer;
                appended.FindPropertyRelative("uniqueID").intValue = layer.GetHashCode();
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty AddArrayElement(SerializedProperty array)
        {
            array.arraySize += 1;
            return array.GetArrayElementAtIndex(array.arraySize - 1);
        }

        private static void SetLayerName(SerializedProperty layers, int index, string name)
        {
            SerializedProperty element = layers.GetArrayElementAtIndex(index);
            if (element != null && string.IsNullOrEmpty(element.stringValue))
            {
                element.stringValue = name;
            }
            else if (element != null && element.stringValue != name)
            {
                Debug.LogWarning($"[ProjectScaffolder] Layer {index} 已被占用（{element.stringValue}），跳过 {name}。");
            }
        }

        // ---------------------------------------------------------------- 资产

        /// <summary>生成 1x1 白色矩形 Sprite（背景遮罩与灰盒通用）。</summary>
        private static string EnsureWhiteRectSprite()
        {
            Directory.CreateDirectory(ArtUiDir);
            string path = $"{ArtUiDir}/WhiteRect.png";
            if (File.Exists(path))
            {
                return path;
            }
            var texture = new Texture2D(4, 4);
            Color[] pixels = Enumerable.Repeat(Color.white, 16).ToArray();
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; // 默认 Single 模式
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return path;
        }

        private static LevelDefinition[] CreateLevelDefinitions()
        {
            string levelsDir = $"{DataDir}/Levels";
            Directory.CreateDirectory(levelsDir);

            // 灰盒初值全部来自策划方案第 7 章与 3.2/3.6 的任务文本。
            var specs = new (string id, string scene, string display, float min, float max, float initial,
                string spawn, string objective, string[] exitFlags, string exitHint, HintGroup[] hints)[]
            {
                ("L00", "L00_Gate", "院门", 0f, 28f, 26f, "spawn_gate_out",
                    Objectives.ReachAtticFindBox, new string[0], "先开门，进院子再说。",
                    new[]
                    {
                        new HintGroup { stageKey = "L00_GatePassed", hintLevel1 = "看看栅栏。",
                            hintLevel2 = "它现在不完整。", hintLevel3 = "把边界拉到栅栏左边。" },
                    }),
                ("L01", "L01_Courtyard", "水渠与树", 0f, 32f, 32f, "spawn_courtyard_in",
                    Objectives.ReachAtticFindBox, new string[0], "先跨过水渠，再往上走。",
                    new[]
                    {
                        new HintGroup { stageKey = "L01_BridgePassed", hintLevel1 = "水渠上盖板看起来不结实。",
                            hintLevel2 = "以前它是好的。", hintLevel3 = "让盖板回到过去，再踩上去。" },
                    }),
                ("L02", "L02_Sewing", "缝纫间外台", 0f, 26f, 26f, "spawn_sewing_ground",
                    Objectives.ReachAtticFindBox, new[] { StoryFlags.UniformRead }, "袖口的事还没弄明白。",
                    new[]
                    {
                        new HintGroup { stageKey = StoryFlags.UniformRead, hintLevel1 = "两个地方需要不同年代。",
                            hintLevel2 = "先找不会变化的砖台。", hintLevel3 = "先爬旧梯，站上砖台，再让左侧踏台出现。" },
                    }),
                ("L03", "L03_Corridor", "楼上回廊", 0f, 30f, 30f, "spawn_corridor_in",
                    Objectives.ReachAtticFindBox, new[] { StoryFlags.PhotoRevealed }, "阁楼门前有张照片。",
                    new[]
                    {
                        new HintGroup { stageKey = StoryFlags.PhotoRevealed, hintLevel1 = "左边的地板缺了一段。",
                            hintLevel2 = "右边踩板能用，柜子挡着路。", hintLevel3 = "过去地板接上现在踏板，再让柜子回到空位。" },
                    }),
                ("L04", "L04_Attic", "阁楼窗外", 0f, 28f, 0f, "spawn_attic_window",
                    Objectives.TellXiaoxaoBoxLocation, new[] { StoryFlags.BoxLocated }, "树还没有长到窗边。",
                    new[]
                    {
                        new HintGroup { stageKey = StoryFlags.BoxLocated, hintLevel1 = "小时候的树够不到窗户。",
                            hintLevel2 = "树要长大。", hintLevel3 = "把边界拉到树干左边，让整棵树进入现在。" },
                    }),
                ("Epilogue", "Epilogue", "两天后", 0f, 1f, 0f, "spawn_epilogue",
                    "（尾声演出）", new string[0], string.Empty, new HintGroup[0]),
            };

            var catalog = new List<LevelDefinition>();
            foreach (var spec in specs)
            {
                string path = $"{levelsDir}/{spec.id}_{spec.scene}.asset";
                LevelDefinition def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<LevelDefinition>();
                    AssetDatabase.CreateAsset(def, path);
                }
                def.levelId = spec.id;
                def.sceneName = spec.scene;
                def.displayName = spec.display;
                def.boundaryMin = spec.min;
                def.boundaryMax = spec.max;
                def.initialBoundary = spec.initial;
                def.spawnId = spec.spawn;
                def.initialObjective = spec.objective;
                def.exitRequiredFlags = spec.exitFlags;
                def.exitLockedHint = spec.exitHint;
                def.hintGroups = spec.hints;
                EditorUtility.SetDirty(def);
                catalog.Add(def);
            }
            return catalog.ToArray();
        }

        // ---------------------------------------------------------------- 场景

        private static void CreateBootstrapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var systemRoot = new GameObject("SystemRoot");
            systemRoot.AddComponent<Core.Bootstrap>();
            systemRoot.AddComponent<Core.GameSession>();
            systemRoot.AddComponent<Save.SaveService>();
            systemRoot.AddComponent<Save.CheckpointService>();
            systemRoot.AddComponent<Audio.AudioService>();
            systemRoot.AddComponent<Audio.SfxLibrary>();
            systemRoot.AddComponent<Narrative.NarrativeDirector>();
            systemRoot.AddComponent<Narrative.DialogueLibrary>();
            systemRoot.AddComponent<Narrative.DialoguePlayer>();
            systemRoot.AddComponent<Player.InputRouter>();

            // 常驻 UI 根（策划 10.3：系统根随 Bootstrap 常驻，重开关卡不重复创建）。
            var uiRoot = new GameObject("UICanvas");
            uiRoot.transform.SetParent(systemRoot.transform);
            var canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); // 设计基准（策划 8.6）
            uiRoot.AddComponent<GraphicRaycaster>();
            uiRoot.AddComponent<UI.HudService>();
            CreateChild<UI.SubtitlePanel>(uiRoot.transform, "SubtitlePanel");
            CreateChild<UI.ObjectivePanel>(uiRoot.transform, "ObjectivePanel");
            CreateChild<UI.InteractionPrompt>(uiRoot.transform, "InteractionPrompt");
            var handle = CreateChild<UI.BoundaryHandle>(uiRoot.transform, "BoundaryHandle");
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 44); // 至少 44px 命中区（策划 5.3）
            CreateChild<UI.HintToast>(uiRoot.transform, "HintToast");
            CreateChild<UI.EraBadge>(uiRoot.transform, "EraBadge");
            var fadeObject = CreateChild<UI.FadeScreen>(uiRoot.transform, "FadeScreen");
            fadeObject.AddComponent<UnityEngine.UI.Image>();
            CreateChild<UI.PauseMenu>(uiRoot.transform, "PauseMenu");
            CreateChild<UI.ResumeConfirmDialog>(uiRoot.transform, "ResumeConfirmDialog");

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            Directory.CreateDirectory(ScenesDir);
            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/Bootstrap.scene");
        }

        private static void CreateMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var menu = new GameObject("MenuCanvas");
            var canvas = menu.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            menu.AddComponent<CanvasScaler>();
            menu.AddComponent<GraphicRaycaster>();
            menu.AddComponent<UI.MainMenuUi>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/MainMenu.scene");
        }

        private static void CreateLevelScenes(LevelDefinition[] catalog, string whiteSpritePath)
        {
            Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(whiteSpritePath);
            foreach (LevelDefinition def in catalog)
            {
                CreateLevelScene(def, white);
            }
        }

        private static void CreateLevelScene(LevelDefinition def, Sprite white)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 房间根（策划 10.3）。
            var roomRoot = new GameObject("RoomRoot_L00");
            var levelRoot = roomRoot.AddComponent<Level.LevelRoot>();
            var registry = roomRoot.AddComponent<TimeBoundary.TemporalObjectRegistry>();
            var boundary = roomRoot.AddComponent<TimeBoundary.BoundaryController>();
            roomRoot.AddComponent<Level.LevelDirector>();
            roomRoot.AddComponent<Level.HintDirector>();
            roomRoot.name = $"RoomRoot_{def.levelId}";
            roomRoot.layer = LayerMask.NameToLayer("Ground");

            // 视觉根：双年代背景遮罩（策划 11.1）。
            var visualRoot = new GameObject("TemporalVisual");
            var visual = visualRoot.AddComponent<TimeBoundary.TemporalVisualController>();
            var pastMask = CreateMaskNode(visualRoot.transform, "PastMask", white);
            var presentMask = CreateMaskNode(visualRoot.transform, "PresentMask", white);

            // 相机（正交，策划 6.1）：复用场景自带 Main Camera，避免重复相机与标签冲突。
            Camera cam = Camera.main;
            if (cam == null)
            {
                var cameraGo = new GameObject("Main Camera");
                cam = cameraGo.AddComponent<Camera>();
                cameraGo.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.backgroundColor = new Color(0.92f, 0.93f, 0.95f);
            if (cam.GetComponent<Level.CameraRig2D>() == null)
            {
                cam.gameObject.AddComponent<Level.CameraRig2D>();
            }

            // 内容骨架：地面、出生点、跌落区、检查点、示例年代物体、出口。
            var content = new GameObject("Content");
            var ground = CreateBox(content.transform, "Ground", new Vector2(14f, -1f), new Vector2(32f, 1f), white);
            ground.layer = LayerMask.NameToLayer("Ground");
            var kill = CreateBox(content.transform, "KillZone", new Vector2(14f, -8f), new Vector2(64f, 1f), white);
            kill.AddComponent<Level.KillZone>();

            var spawn = new GameObject($"Spawn_{def.spawnId}");
            spawn.transform.SetParent(content.transform);
            spawn.transform.position = new Vector3(2f, 0.6f, 0f);
            var spawnPoint = spawn.AddComponent<Level.SpawnPoint>();
            SetPrivateField(spawnPoint, "spawnId", def.spawnId);

            var checkpoint = new GameObject("Checkpoint_mid");
            checkpoint.transform.SetParent(content.transform);
            checkpoint.transform.position = new Vector3(12f, 1.2f, 0f);
            var cpCollider = checkpoint.AddComponent<BoxCollider2D>();
            cpCollider.isTrigger = true;
            cpCollider.size = new Vector2(1.2f, 1.6f);
            var cpMarker = checkpoint.AddComponent<Level.CheckpointMarker>();
            SetPrivateField(cpMarker, "checkpointId", $"cp_{def.levelId}_mid");

            // 示例年代物体：本关第一个关键机关坐标（策划第 7 章）。
            var example = CreateTemporalExample(content.transform, def, white);
            var exit = new GameObject("Exit_NextLevel");
            exit.transform.SetParent(content.transform);
            exit.transform.position = new Vector3(23f, 1f, 0f);
            exit.layer = LayerMask.NameToLayer("Interactable");
            var exitCollider = exit.AddComponent<BoxCollider2D>();
            exitCollider.size = new Vector2(0.6f, 2f);
            exit.AddComponent<Level.ExitDoor>();

            // 连接房间根的字段。
            SetPrivateField(levelRoot, "definition", def);
            SetPrivateField(levelRoot, "roomId", def.levelId);
            SetPrivateField(levelRoot, "overrideBoundaryRange", true);
            SetPrivateField(levelRoot, "boundaryMin", def.boundaryMin);
            SetPrivateField(levelRoot, "boundaryMax", def.boundaryMax);
            SetPrivateField(levelRoot, "initialBoundary", def.initialBoundary);
            SetPrivateField(levelRoot, "boundaryController", boundary);
            SetPrivateField(levelRoot, "visualController", visual);
            SetPrivateArray(levelRoot, "spawnPoints", new[] { spawnPoint });
            SetPrivateField(visual, "pastMaskRoot", pastMask.transform);
            SetPrivateField(visual, "presentMaskRoot", presentMask.transform);
            SetPrivateField(visual, "roomLeft", def.boundaryMin - 2f);
            SetPrivateField(visual, "roomRight", def.boundaryMax + 2f);
            SetPrivateField(boundary, "keyboardAdjustSpeed", 4f);

            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/{def.sceneName}.scene");
        }

        /// <summary>示例年代物体（策划第 7 章每关第一个关键机关灰盒）。</summary>
        private static GameObject CreateTemporalExample(Transform parent, LevelDefinition def, Sprite white)
        {
            (string id, float anchorX, bool pastCanStand, bool presentCanStand, float width, float height) spec = def.levelId switch
            {
                "L00" => ("fence_gate", 10f, true, false, 1.2f, 2.6f),   // 栅栏：过去完整阻挡，现在下部缺口（策划 7.1）
                "L01" => ("plank_bridge", 12f, true, false, 4f, 0.4f),    // 水渠盖板（策划 7.2）
                "L02" => ("old_ladder", 17f, true, false, 0.8f, 2.4f),    // 旧木梯（策划 7.4）
                "L03" => ("floor_plank", 7f, true, false, 4.5f, 0.4f),    // 过去木地板（策划 7.5）
                "L04" => ("courtyard_tree", 4f, true, false, 1.4f, 3.2f), // 树：锚点统一树干（策划 7.6/11.5）
                _ => ("epilogue_none", 0.5f, false, false, 1f, 1f),
            };

            string defDir = $"{DataDir}/TemporalObjects";
            Directory.CreateDirectory(defDir);
            string assetPath = $"{defDir}/{def.levelId}_{spec.id}.asset";
            var objectDef = AssetDatabase.LoadAssetAtPath<TemporalObjectDefinition>(assetPath);
            if (objectDef == null)
            {
                objectDef = ScriptableObject.CreateInstance<TemporalObjectDefinition>();
                AssetDatabase.CreateAsset(objectDef, assetPath);
            }
            objectDef.objectId = spec.id;
            objectDef.anchorX = spec.anchorX;
            objectDef.safetyPolicy = SafetyPolicy.FullCheck;
            objectDef.pastState = new TemporalObjectState { stateId = $"{spec.id}_past", canStand = spec.pastCanStand, blocksMovement = spec.pastCanStand };
            objectDef.presentState = new TemporalObjectState { stateId = $"{spec.id}_present", canStand = spec.presentCanStand, blocksMovement = spec.presentCanStand };
            EditorUtility.SetDirty(objectDef);

            var obj = new GameObject($"Temporal_{spec.id}");
            obj.transform.SetParent(parent);
            obj.transform.position = new Vector3(spec.anchorX, spec.height * 0.5f, 0f);
            obj.layer = LayerMask.NameToLayer("Temporal");
            var temporal = obj.AddComponent<TimeBoundary.TemporalObject>();
            SetPrivateField(temporal, "definition", objectDef);
            SetPrivateField(temporal, "objectId", spec.id);
            SetPrivateField(temporal, "anchorX", spec.anchorX);

            var pastRoot = CreateEraChild(obj.transform, "Past", white, spec.width, spec.height);
            var presentRoot = CreateEraChild(obj.transform, "Present", white, spec.width, spec.height * 0.5f);
            SetPrivateField(temporal, "pastRoot", pastRoot);
            SetPrivateField(temporal, "presentRoot", presentRoot);
            return obj;
        }

        private static GameObject CreateEraChild(Transform parent, string era, Sprite white, float width, float height)
        {
            var child = new GameObject(era);
            child.transform.SetParent(parent);
            child.transform.localPosition = Vector3.zero;
            child.layer = parent.gameObject.layer;
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = white;
            renderer.sortingLayerName = "EraPlatform";
            renderer.color = era == "Past" ? new Color(0.86f, 0.72f, 0.48f) : new Color(0.62f, 0.72f, 0.66f);
            renderer.size = new Vector2(width, height);
            var collider = child.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(width, height);
            return child;
        }

        private static GameObject CreateMaskNode(Transform parent, string name, Sprite white)
        {
            var node = new GameObject(name);
            node.transform.SetParent(parent);
            node.transform.localPosition = Vector3.zero;
            var renderer = node.AddComponent<SpriteRenderer>();
            renderer.sprite = white;
            renderer.color = new Color(1f, 1f, 1f, 0.08f);
            renderer.sortingLayerName = "EraBackground";
            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            node.AddComponent<SpriteMask>();
            return node;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector2 position, Vector2 size, Sprite white)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent);
            box.transform.position = new Vector3(position.x, position.y, 0f);
            var renderer = box.AddComponent<SpriteRenderer>();
            renderer.sprite = white;
            renderer.color = new Color(0.45f, 0.42f, 0.4f);
            renderer.sortingLayerName = "ConstantStructure";
            renderer.size = size;
            var collider = box.AddComponent<BoxCollider2D>();
            collider.size = size;
            return box;
        }

        private static GameObject CreateChild<T>(Transform parent, string name) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            child.AddComponent<T>();
            return child;
        }

        private static void SetPrivateField(UnityEngine.Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[ProjectScaffolder] 找不到字段 {field}（{target.GetType().Name}）。");
                return;
            }
            switch (value)
            {
                case float f: property.floatValue = f; break;
                case bool b: property.boolValue = b; break;
                case string s: property.stringValue = s; break;
                case UnityEngine.Object o: property.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateArray<T>(UnityEngine.Object target, string field, T[] value) where T : UnityEngine.Object
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                return;
            }
            property.arraySize = value.Length;
            for (int i = 0; i < value.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = value[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBuildSceneList(LevelDefinition[] catalog)
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene($"{ScenesDir}/Bootstrap.scene", true),
                new EditorBuildSettingsScene($"{ScenesDir}/MainMenu.scene", true),
            };
            foreach (LevelDefinition def in catalog)
            {
                scenes.Add(new EditorBuildSettingsScene($"{ScenesDir}/{def.sceneName}.scene", true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
