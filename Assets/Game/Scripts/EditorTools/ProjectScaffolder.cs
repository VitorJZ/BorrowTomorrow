using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Level;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>
    /// 项目脚手架（供比赛期团队快速起步与重建）：
    /// 1. 配置物理层、排序层（策划 8.5 图层）与构建设置；
    /// 2. 生成数据资产：LevelDefinition（策划第 7 章初值）、TemporalObjectDefinition、占位 DialogueEntry；
    /// 3. 生成玩家灰盒预制体、中文字体与摩擦物理材质；
    /// 4. 创建 Bootstrap / MainMenu / L00–L04 / Epilogue 场景骨架（灰盒布局见 LevelLayouts）；
    /// 5. 场景结构自检（SceneValidator）。
    /// 可从菜单运行，也可命令行批处理运行：
    /// Tuanjie.exe -batchmode -quit -projectPath &lt;项目&gt; -executeMethod BorrowTomorrow.EditorTools.ProjectScaffolder.RunFromCommandLine
    /// </summary>
    public static class ProjectScaffolder
    {
        private const string ScenesDir = "Assets/Game/Scenes";
        private const string DataDir = "Assets/Game/Data";
        private const string ArtUiDir = "Assets/Game/Art/UI";
        private const string PrefabsDir = "Assets/Game/Prefabs";

        // ---------------------------------------------------------------- 入口

        [MenuItem("BorrowTomorrow/一键搭建项目骨架")]
        public static void ScaffoldAll()
        {
            LevelDefinition[] catalog = RunScaffold();
            SceneValidator.ValidateAll(catalog);
        }

        public static void RunFromCommandLine()
        {
            LevelDefinition[] catalog = RunScaffold();
            SceneValidator.ValidateAll(catalog);
        }

        private static LevelDefinition[] RunScaffold()
        {
            SetupProjectSettings();
            string whiteSprite = EnsureWhiteRectSprite();
            Font font = EnsureChineseFont();
            PhysicsMaterial2D friction = EnsureFrictionMaterial();
            LevelDefinition[] catalog = CreateLevelDefinitions();
            Dictionary<string, string> temporalDefPaths = CreateTemporalObjectDefinitions();
            Dictionary<string, string> dialoguePaths = CreateDialogueAssets();
            // 数据资产立即落盘：后续预制体/场景保存触发的导入不会丢弃内存中的修改。
            AssetDatabase.SaveAssets();
            GameObject playerPrefab = CreatePlayerPrefab(whiteSprite);
            CreateBootstrapScene(catalog, dialoguePaths, font);
            CreateMainMenuScene(font);
            CreateLevelScenes(catalog, whiteSprite, font, playerPrefab, friction, temporalDefPaths, dialoguePaths);
            SetBuildSceneList(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectScaffolder] 项目骨架搭建完成。");
            return catalog;
        }

        // ---------------------------------------------------------------- 项目设置

        private static void SetupProjectSettings()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[ProjectScaffolder] 找不到 TagManager.asset。");
                return;
            }
            var tagManager = new SerializedObject(assets[0]);

            SerializedProperty layers = tagManager.FindProperty("layers");
            SetLayerName(layers, 8, "Player");
            SetLayerName(layers, 9, "Ground");
            SetLayerName(layers, 10, "Interactable");
            SetLayerName(layers, 11, "Temporal");
            SetLayerName(layers, 12, "KillZone");
            SetLayerName(layers, 13, "Ladder");

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

        // ---------------------------------------------------------------- 基础资产

        /// <summary>生成 1×1 白色矩形 Sprite（灰盒与遮罩通用）。</summary>
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
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return path;
        }

        /// <summary>
        /// 占位中文字体：复制系统黑体（simhei）进工程，仅限灰盒开发使用；
        /// 正式发布必须替换为获得授权的字库（策划 8.6 对话字库要求，见 Docs/10 资源清单）。
        /// </summary>
        private static Font EnsureChineseFont()
        {
            const string dir = "Assets/Game/Art/UI/Fonts";
            string target = $"{dir}/PlaceholderChinese.ttf";
            Font font = AssetDatabase.LoadAssetAtPath<Font>(target);
            if (font != null)
            {
                return font;
            }
            Directory.CreateDirectory(dir);
            const string simhei = "C:/Windows/Fonts/simhei.ttf";
            if (File.Exists(simhei))
            {
                File.Copy(simhei, target, true);
                AssetDatabase.ImportAsset(target);
                font = AssetDatabase.LoadAssetAtPath<Font>(target);
            }
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Debug.LogWarning("[ProjectScaffolder] 未找到 simhei.ttf，灰盒文字回落到内置字体（中文可能显示不全）。");
            }
            return font;
        }

        /// <summary>高摩擦物理材质：恒定站稳面可静止站立（策划 7.4 安全砖台）。</summary>
        private static PhysicsMaterial2D EnsureFrictionMaterial()
        {
            const string path = "Assets/Game/Data/Physics/GroundFriction.physicMaterial";
            PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material == null)
            {
                Directory.CreateDirectory("Assets/Game/Data/Physics");
                material = new PhysicsMaterial2D("GroundFriction")
                {
                    friction = 0.9f,
                    bounciness = 0f,
                };
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        // ---------------------------------------------------------------- 数据资产

        /// <summary>关卡定义：灰盒初值全部来自策划第 7 章（边界、出生点、出口条件、三级提示）。</summary>
        private static LevelDefinition[] CreateLevelDefinitions()
        {
            string levelsDir = $"{DataDir}/Levels";
            Directory.CreateDirectory(levelsDir);

            var specs = new (string id, string scene, string display, float min, float max, float initial,
                string spawn, string objective, string[] exitFlags, string exitHint,
                (string stage, string h1, string h2, string h3)[] hints)[]
            {
                ("L00", "L00_Gate", "院门", 0f, 26f, 26f, "spawn_gate_out",
                    Objectives.ReachAtticFindBox, new string[0], "先开门，进院子再说。",
                    new[] { ("Trigger_L00_gate_passed", "看看栅栏。", "它现在不完整。", "把边界拉到栅栏左边。") }),
                ("L01", "L01_Courtyard", "水渠与树", 0f, 20f, 0f, "spawn_courtyard_in",
                    Objectives.ReachAtticFindBox, new string[0], "先跨过水渠，再往上走。",
                    new[] { ("Trigger_L01_bridge_passed", "水渠上盖板看起来不结实。", "以前它是好的。", "让盖板回到过去，再踩上去。") }),
                ("L02", "L02_Sewing", "缝纫间外台", 0f, 26f, 0f, "spawn_sewing_ground",
                    Objectives.ReachAtticFindBox, new[] { StoryFlags.UniformRead }, "袖口的事还没弄明白。",
                    new[] { (StoryFlags.UniformRead, "两个地方需要不同年代。", "先找不会变化的砖台。", "先爬旧梯，站上砖台，再让左侧踏台出现。") }),
                ("L03", "L03_Corridor", "楼上回廊", 0f, 30f, 0f, "spawn_corridor_in",
                    Objectives.ReachAtticFindBox, new[] { StoryFlags.PhotoRevealed }, "阁楼门前有张照片。",
                    new[] { (StoryFlags.PhotoRevealed, "左边的地板缺了一段。", "右边踩板能用，柜子挡着路。", "过去地板接上现在踏板，再让柜子回到空位。") }),
                ("L04", "L04_Attic", "阁楼窗外", 0f, 28f, 27f, "spawn_attic_window",
                    Objectives.TellXiaoxaoBoxLocation, new[] { StoryFlags.BoxLocated }, "树还没有长到窗边。",
                    new[] { (StoryFlags.BoxLocated, "小时候的树够不到窗户。", "树要长大。", "把边界拉到树干左边，让整棵树进入现在。") }),
                ("Epilogue", "Epilogue", "两天后", 0f, 1f, 0f, "spawn_epilogue",
                    "（尾声演出）", new string[0], string.Empty, Array.Empty<(string, string, string, string)>()),
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
                def.hintGroups = spec.hints
                    .Select(h => new HintGroup { stageKey = h.stage, hintLevel1 = h.h1, hintLevel2 = h.h2, hintLevel3 = h.h3 })
                    .ToArray();
                EditorUtility.SetDirty(def);
                catalog.Add(def);
            }
            return catalog.ToArray();
        }

        /// <summary>年代物体定义：策划 13.3 交接契约（稳定 ID、锚点、两年代状态、安全策略、切换音效）。
        /// 返回 id → 资产路径（查找时惰性加载）。</summary>
        private static Dictionary<string, string> CreateTemporalObjectDefinitions()
        {
            string dir = $"{DataDir}/TemporalObjects";
            Directory.CreateDirectory(dir);

            var specs = new (string id, float anchor, SafetyPolicy policy,
                string pastStateId, bool pastCanStand, bool pastBlocks,
                string presentStateId, bool presentCanStand, bool presentBlocks)[]
            {
                ("L00_fence_gate", 10f, SafetyPolicy.FullCheck, "fence_full", false, true, "fence_gap", false, true),
                ("L01A_plank_bridge", 12f, SafetyPolicy.FullCheck, "plank_solid", true, false, "plank_rotten", false, false),
                ("L01B_wood_ladder", 48f, SafetyPolicy.FullCheck, "ladder_intact", false, false, "ladder_broken", false, false),
                ("L01B_tree_branch", 58f, SafetyPolicy.FullCheck, "sapling", false, false, "adult_branch", true, false),
                ("L02_old_ladder", 13f, SafetyPolicy.FullCheck, "ladder_intact", false, false, "ladder_broken", false, false),
                ("L02_maintenance", 6f, SafetyPolicy.FullCheck, "absent", false, false, "platform", true, false),
                ("L03_floor", 7f, SafetyPolicy.FullCheck, "wood_floor", true, false, "missing", false, false),
                ("L03_walkway", 15f, SafetyPolicy.FullCheck, "missing", false, false, "metal_plate", true, false),
                ("L03_cabinet", 24f, SafetyPolicy.BlockIfCoveringPlayer, "empty", false, false, "cabinet_full", false, true),
                ("L04_tree", 4f, SafetyPolicy.FullCheck, "sapling", false, false, "adult_tree", true, false),
            };

            var map = new Dictionary<string, string>();
            foreach (var spec in specs)
            {
                string path = $"{dir}/{spec.id}.asset";
                TemporalObjectDefinition def = AssetDatabase.LoadAssetAtPath<TemporalObjectDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<TemporalObjectDefinition>();
                    AssetDatabase.CreateAsset(def, path);
                }
                def.objectId = spec.id;
                def.anchorX = spec.anchor;
                def.safetyPolicy = spec.policy;
                def.anchorHysteresis = 0.05f;
                def.switchSfxId = "temporal_switch";
                def.pastState = new TemporalObjectState { stateId = spec.pastStateId, canStand = spec.pastCanStand, blocksMovement = spec.pastBlocks };
                def.presentState = new TemporalObjectState { stateId = spec.presentStateId, canStand = spec.presentCanStand, blocksMovement = spec.presentBlocks };
                EditorUtility.SetDirty(def);
                map[spec.id] = path; // 只存路径：查找时惰性加载，规避批处理中实例被销毁的伪 null
            }
            return map;
        }
        /// <summary>
        /// 占位对白：文本取自策划 03/3.5/3.6/3.8/3.9 的锁定脚本，用于灰盒期打通标记与出口链路；
        /// B 组在此之上扩写全部对白并替换（接线见 Docs/10 资源清单）。返回 id → 资产路径。
        /// </summary>
        private static Dictionary<string, string> CreateDialogueAssets()
        {
            string dir = $"{DataDir}/Dialogue";
            Directory.CreateDirectory(dir);

            var specs = new (string id, string speaker, string text, string next, string[] setFlags)[]
            {
                ("gate_self_talk", "主角", "院子……和记忆里一样旧。", null, null),
                ("bridge_self_talk", "主角", "对岸的台阶还在就好了。", null, null),
                ("uniform_self", "主角", "袖口留了一寸。明年还能穿。", "uniform_kid", null),
                ("uniform_kid", "童年林晓", "外婆，你怎么知道我明年长多高？", "uniform_gran", null),
                ("uniform_gran", "年轻外婆", "不知道呀。先替你留一点。", null, new[] { StoryFlags.UniformRead }),
                ("ledger_check", "主角", "账本：晓晓校服，不记钱。", null, null),
                ("photo_back", "主角", "照片背面：1994年秋。妈妈和晓晓。树种下的第一年。", "photo_front", null),
                ("photo_front", "主角", "翻过来——外婆和小女孩站在新栽的树旁。外婆的衣饰，和我身上的一模一样。", "photo_voice",
                    new[] { StoryFlags.PhotoRevealed, StoryFlags.IdentityKnown }),
                ("photo_voice", "成年林晓", "外婆，是我，晓晓。我三十七了。你不用再给我放袖口了。", null, null),
                ("compartment_view", "主角", "高处的木板后面……好像有个蓝色的角。", null, null),
                ("compartment_touch", "主角", "东边阁楼……窗户左上边。木板后头。蓝的。", null, new[] { StoryFlags.BoxLocated }),
                ("epilogue_letter", "林晓", "（信）晓晓：这些布你小时候都喜欢，我给你留着。等你有了自己的家，带过去。", null,
                    new[] { StoryFlags.EpilogueSeen }),
            };

            var map = new Dictionary<string, string>();
            foreach (var spec in specs)
            {
                string path = $"{dir}/D_{spec.id}.asset";
                DialogueEntry entry = AssetDatabase.LoadAssetAtPath<DialogueEntry>(path);
                if (entry == null)
                {
                    entry = ScriptableObject.CreateInstance<DialogueEntry>();
                    AssetDatabase.CreateAsset(entry, path);
                }
                entry.entryId = spec.id;
                entry.speakerLabel = spec.speaker;
                entry.subtitle = spec.text;
                entry.nextId = spec.next;
                entry.setFlags = spec.setFlags ?? Array.Empty<string>();
                entry.requiredFlags = Array.Empty<string>();
                entry.holdForConfirm = true;
                entry.minDurationSeconds = 1.2f;
                EditorUtility.SetDirty(entry);
                map[spec.id] = path; // 只存路径：查找时惰性加载
            }
            return map;
        }

        // ---------------------------------------------------------------- 玩家预制体

        /// <summary>玩家灰盒预制体：Rigidbody2D + 胶囊 + 运动器 + 交互器 + 占位外观（策划 8.4 剪影比例）。</summary>
        private static GameObject CreatePlayerPrefab(string whiteSpritePath)
        {
            Directory.CreateDirectory(PrefabsDir);
            string path = $"{PrefabsDir}/Player_Graybox.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }
            Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(whiteSpritePath);

            var root = new GameObject("Player_Graybox");
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                root.layer = playerLayer;
            }
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.6f, 1.6f);
            capsule.offset = new Vector2(0f, 0.8f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = white;
            renderer.color = new Color(0.47f, 0.16f, 0.18f); // 策划 8.4：暗红针织外搭
            renderer.sortingLayerName = "Characters";
            renderer.size = new Vector2(0.55f, 1.55f);

            var motor = root.AddComponent<Player.PlayerMotor2D>();
            int groundLayer = LayerMask.NameToLayer("Ground");
            int temporalLayer = LayerMask.NameToLayer("Temporal");
            LevelLayouts.SetField(motor, "groundMask", (1 << groundLayer) | (1 << temporalLayer));

            var interactor = root.AddComponent<Player.PlayerInteractor>();
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            LevelLayouts.SetField(interactor, "interactableMask", 1 << interactableLayer);
            LevelLayouts.SetField(interactor, "detectionRadius", 1.4f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------- Bootstrap 场景

        private static void CreateBootstrapScene(LevelDefinition[] catalog,
            Dictionary<string, string> dialoguePaths, Font font)
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

            // 关卡目录（通关顺序，策划 10.4：GameSession 协调加载）。
            var gameSession = systemRoot.GetComponent<Core.GameSession>();
            SetObjectArray(gameSession, "levelCatalog", catalog);
            LevelLayouts.SetField(gameSession, "mainMenuSceneName", "MainMenu");

            // 对白库集中维护（策划 10.5）：按路径惰性加载后装配。
            var library = systemRoot.GetComponent<Narrative.DialogueLibrary>();
            DialogueEntry[] dialogueEntries = dialoguePaths.Values
                .Select(AssetDatabase.LoadAssetAtPath<DialogueEntry>)
                .Where(e => e != null)
                .ToArray();
            SetObjectArray(library, "entries", dialogueEntries);

            // 常驻 UI 根（策划 10.3：系统根随 Bootstrap 常驻，重开关卡不重复创建）。
            var uiRoot = new GameObject("UICanvas");
            uiRoot.transform.SetParent(systemRoot.transform);
            var canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); // 设计基准（策划 8.6）
            uiRoot.AddComponent<GraphicRaycaster>();
            var hud = uiRoot.AddComponent<UI.HudService>();

            // 左上目标（策划 5.4）。
            var objective = CreateChild<UI.ObjectivePanel>(uiRoot.transform, "ObjectivePanel");
            Stretch((RectTransform)objective.transform, new Vector2(-560f, 470f), new Vector2(640f, 90f));
            Text objectiveText = MakeText(objective.transform, "ObjectiveText", string.Empty, font, 28,
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(600f, 80f));
            LevelLayouts.SetObject(objective, "objectiveText", objectiveText);

            // 顶部年代把手（命中区 ≥44px，策划 5.3）。
            var handle = CreateChild<UI.BoundaryHandle>(uiRoot.transform, "BoundaryHandle");
            ((RectTransform)handle.transform).sizeDelta = new Vector2(240, 44);
            ((RectTransform)handle.transform).anchoredPosition = new Vector2(0f, 480f);

            // 底部对白字幕（策划 5.4：最多两行）。
            var subtitle = CreateChild<UI.SubtitlePanel>(uiRoot.transform, "SubtitlePanel");
            Stretch((RectTransform)subtitle.transform, new Vector2(0f, -190f), new Vector2(1100f, 150f));
            Text speakerText = MakeText(subtitle.transform, "SpeakerText", string.Empty, font, 24,
                TextAnchor.MiddleLeft, new Vector2(0f, 45f), new Vector2(1000f, 40f));
            Text contentText = MakeText(subtitle.transform, "ContentText", string.Empty, font, 30,
                TextAnchor.UpperLeft, new Vector2(0f, -5f), new Vector2(1040f, 90f));
            LevelLayouts.SetObject(subtitle, "speakerText", speakerText);
            LevelLayouts.SetObject(subtitle, "contentText", contentText);

            // 交互 E 提示（底部中间）。
            var prompt = CreateChild<UI.InteractionPrompt>(uiRoot.transform, "InteractionPrompt");
            Stretch((RectTransform)prompt.transform, new Vector2(0f, 90f), new Vector2(400f, 50f));
            Text promptText = MakeText(prompt.transform, "PromptText", string.Empty, font, 24,
                TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(380f, 46f));
            LevelLayouts.SetObject(prompt, "hintText", promptText);

            // 提示气泡（策划 12.4）。
            var toast = CreateChild<UI.HintToast>(uiRoot.transform, "HintToast");
            Stretch((RectTransform)toast.transform, new Vector2(0f, 160f), new Vector2(700f, 50f));
            Text toastText = MakeText(toast.transform, "HintText", string.Empty, font, 24,
                TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(680f, 46f));
            LevelLayouts.SetObject(toast, "hintText", toastText);

            // 年代角标（策划 4.2：过门转场后重新标清两侧年代）。
            var badge = CreateChild<UI.EraBadge>(uiRoot.transform, "EraBadge");
            Stretch((RectTransform)badge.transform, new Vector2(0f, 380f), new Vector2(300f, 70f));
            Text badgeText = MakeText(badge.transform, "BadgeText", string.Empty, font, 40,
                TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(280f, 66f));
            LevelLayouts.SetObject(badge, "badgeText", badgeText);

            // 边界受阻提示（策划 4.5：先到稳固的地方）。
            Text blockedText = MakeText(uiRoot.transform, "BlockedToast", string.Empty, font, 26,
                TextAnchor.MiddleCenter, new Vector2(0f, 220f), new Vector2(700f, 50f));
            LevelLayouts.SetObject(hud, "blockedToastText", blockedText);

            // 全屏淡入淡出（策划 4.6）。
            var fadeObject = CreateChild<UI.FadeScreen>(uiRoot.transform, "FadeScreen");
            Stretch((RectTransform)fadeObject.transform, Vector2.zero, Vector2.zero, true);

            // 暂停提示（灰盒文字版；正式菜单由 B 组制作）。
            var pauseMenu = CreateChild<UI.PauseMenu>(uiRoot.transform, "PauseMenu");
            Stretch((RectTransform)pauseMenu.transform, new Vector2(0f, 0f), new Vector2(560f, 160f));
            MakeText(pauseMenu.transform, "PauseLabel", "已暂停（按 Esc 继续）", font, 30,
                TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(540f, 60f));

            // 恢复确认框（策划 4.6/5.1）。
            var resumeDialog = CreateChild<UI.ResumeConfirmDialog>(uiRoot.transform, "ResumeConfirmDialog");
            Stretch((RectTransform)resumeDialog.transform, new Vector2(0f, 0f), new Vector2(640f, 120f));
            MakeText(resumeDialog.transform, "ConfirmLabel", "按 Backspace 再次确认恢复检查点；按 E 取消", font, 24,
                TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(620f, 100f));

            // 事件系统挂在系统根下：随 DontDestroyOnLoad 常驻（策划 10.3：重开关卡不重复创建）。
            var eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(systemRoot.transform);
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            Directory.CreateDirectory(ScenesDir);
            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/Bootstrap.scene");
        }

        // ---------------------------------------------------------------- 主菜单场景

        private static void CreateMainMenuScene(Font font)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var menu = new GameObject("MenuCanvas");
            var canvas = menu.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = menu.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            menu.AddComponent<GraphicRaycaster>();
            var menuUi = menu.AddComponent<UI.MainMenuUi>();

            MakeText(menu.transform, "Title", "借一点明天", font, 64, TextAnchor.MiddleCenter,
                new Vector2(0f, 330f), new Vector2(900f, 120f));
            MakeText(menu.transform, "Subtitle", "灰盒开发版（占位美术）", font, 22, TextAnchor.MiddleCenter,
                new Vector2(0f, 250f), new Vector2(600f, 40f));

            Button continueButton = MakeButton(menu.transform, "继续游戏", font, new Vector2(0f, 60f));
            Button newGameButton = MakeButton(menu.transform, "开始新游戏", font, new Vector2(0f, -20f));
            Button quitButton = MakeButton(menu.transform, "退出游戏", font, new Vector2(0f, -100f));

            UnityEventTools.AddPersistentListener(continueButton.onClick, menuUi.OnContinue);
            UnityEventTools.AddPersistentListener(newGameButton.onClick, menuUi.OnNewGame);
            UnityEventTools.AddPersistentListener(quitButton.onClick, menuUi.QuitGame);

            LevelLayouts.SetObject(menuUi, "continueButton", continueButton);
            LevelLayouts.SetObject(menuUi, "newGameButton", newGameButton);
            LevelLayouts.SetObject(menuUi, "quitButton", quitButton);

            // 事件系统由 Bootstrap 场景的系统根常驻提供，此处不再单独创建（避免重复 EventSystem）。

            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/MainMenu.scene");
        }

        // ---------------------------------------------------------------- 关卡场景

        private static void CreateLevelScenes(LevelDefinition[] catalog, string whiteSpritePath, Font font,
            GameObject playerPrefab, PhysicsMaterial2D friction,
            Dictionary<string, string> temporalDefPaths,
            Dictionary<string, string> dialoguePaths)
        {
            Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(whiteSpritePath);
            foreach (LevelDefinition def in catalog)
            {
                CreateLevelScene(def, white, font, playerPrefab, friction, temporalDefPaths, dialoguePaths);
            }
        }

        private static void CreateLevelScene(LevelDefinition def, Sprite white, Font font,
            GameObject playerPrefab, PhysicsMaterial2D friction,
            Dictionary<string, string> temporalDefPaths,
            Dictionary<string, string> dialoguePaths)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

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
            cam.backgroundColor = new Color(0.9f, 0.92f, 0.94f);
            if (cam.GetComponent<Level.CameraRig2D>() == null)
            {
                cam.gameObject.AddComponent<Level.CameraRig2D>();
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            var ctx = new LevelLayouts.RoomContext
            {
                White = white,
                Friction = friction,
                PlayerMaskValue = 1 << playerLayer,
                TemporalDefPaths = temporalDefPaths,
                DialoguePaths = dialoguePaths,
            };

            switch (def.levelId)
            {
                case "L00":
                {
                    LevelRoot room = CreateRoom(def, "L00", true, 0f, 26f, 26f, new Rect(0f, -2f, 26f, 8f),
                        playerPrefab, white);
                    ctx.Content = RoomContent(room);
                    SetObjectArray(room, "spawnPoints", LevelLayouts.BuildL00(ctx).ToArray());
                    break;
                }
                case "L01":
                {
                    LevelRoot roomA = CreateRoom(def, "L01A", true, 0f, 20f, 0f, new Rect(0f, -2f, 20f, 8f),
                        playerPrefab, white);
                    LevelRoot roomB = CreateRoom(def, "L01B", false, 40f, 69f, 69f, new Rect(40f, -2f, 30f, 8f),
                        playerPrefab, white);
                    ctx.Content = RoomContent(roomA);
                    SetObjectArray(roomA, "spawnPoints", LevelLayouts.BuildL01A(ctx, roomB).ToArray());
                    ctx.Content = RoomContent(roomB);
                    SetObjectArray(roomB, "spawnPoints", LevelLayouts.BuildL01B(ctx).ToArray());
                    break;
                }
                case "L02":
                {
                    LevelRoot room = CreateRoom(def, "L02", true, 0f, 26f, 0f, new Rect(0f, -2f, 26f, 9f),
                        playerPrefab, white);
                    ctx.Content = RoomContent(room);
                    SetObjectArray(room, "spawnPoints", LevelLayouts.BuildL02(ctx).ToArray());
                    break;
                }
                case "L03":
                {
                    LevelRoot room = CreateRoom(def, "L03", true, 0f, 30f, 0f, new Rect(0f, -2f, 31f, 10f),
                        playerPrefab, white);
                    ctx.Content = RoomContent(room);
                    SetObjectArray(room, "spawnPoints", LevelLayouts.BuildL03(ctx).ToArray());
                    break;
                }
                case "L04":
                {
                    LevelRoot room = CreateRoom(def, "L04", true, 0f, 28f, 27f, new Rect(0f, -3f, 31f, 11f),
                        playerPrefab, white);
                    ctx.Content = RoomContent(room);
                    SetObjectArray(room, "spawnPoints", LevelLayouts.BuildL04(ctx).ToArray());
                    // 暗格对白写入 BoxLocated 后自动通关（策划 7.6 标准解第 4 步）。
                    LevelLayouts.SetField(room.GetComponent<Level.LevelDirector>(), "completeLevelOnFlag",
                        StoryFlags.BoxLocated);
                    break;
                }
                case "Epilogue":
                {
                    LevelRoot room = CreateRoom(def, "Epilogue", true, 0f, 1f, 0f, new Rect(0f, -2f, 16f, 8f),
                        playerPrefab, white);
                    ctx.Content = RoomContent(room);
                    SetObjectArray(room, "spawnPoints", LevelLayouts.BuildEpilogue(ctx).ToArray());
                    break;
                }
            }

            EditorSceneManager.SaveScene(scene, $"{ScenesDir}/{def.sceneName}.scene");
        }

        /// <summary>房间内容子目录：年代物体必须挂在 RoomRoot 下才能注册进本房间注册表（策划 10.3）。</summary>
        private static Transform RoomContent(LevelRoot room)
        {
            Transform content = room.transform.Find("Content");
            return content != null ? content : room.transform;
        }

        /// <summary>房间根（策划 10.3）：LevelRoot + 注册表 + 边界 + 导演 + 提示 + 双年代遮罩 + 内容子目录。</summary>
        private static LevelRoot CreateRoom(LevelDefinition def, string roomId, bool initiallyActive,
            float min, float max, float initial, Rect cameraBounds, GameObject playerPrefab, Sprite white)
        {
            var roomGo = new GameObject($"RoomRoot_{roomId}");
            var levelRoot = roomGo.AddComponent<Level.LevelRoot>();
            roomGo.AddComponent<TimeBoundary.TemporalObjectRegistry>();
            TimeBoundary.BoundaryController boundary = roomGo.AddComponent<TimeBoundary.BoundaryController>();
            roomGo.AddComponent<Level.LevelDirector>();
            roomGo.AddComponent<Level.HintDirector>();

            // 内容子目录：本房间的全部可交互与年代物体都挂在这里，
            // 保证 TemporalObject 能通过 GetComponentInParent 注册进本房间注册表。
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(roomGo.transform);

            var visualGo = new GameObject($"TemporalVisual_{roomId}");
            TimeBoundary.TemporalVisualController visual = visualGo.AddComponent<TimeBoundary.TemporalVisualController>();
            GameObject pastMask = CreateMaskNode(visualGo.transform, "PastMask", white);
            GameObject presentMask = CreateMaskNode(visualGo.transform, "PresentMask", white);

            LevelLayouts.SetObject(levelRoot, "definition", def);
            LevelLayouts.SetField(levelRoot, "roomId", roomId);
            LevelLayouts.SetField(levelRoot, "overrideBoundaryRange", true);
            LevelLayouts.SetField(levelRoot, "boundaryMin", min);
            LevelLayouts.SetField(levelRoot, "boundaryMax", max);
            LevelLayouts.SetField(levelRoot, "initialBoundary", initial);
            LevelLayouts.SetField(levelRoot, "initiallyActive", initiallyActive);
            LevelLayouts.SetRect(levelRoot, "cameraBounds", cameraBounds);
            LevelLayouts.SetObject(levelRoot, "boundaryController", boundary);
            LevelLayouts.SetObject(levelRoot, "visualController", visual);
            LevelLayouts.SetObject(levelRoot, "playerPrefab", playerPrefab);

            LevelLayouts.SetObject(visual, "pastMaskRoot", pastMask.transform);
            LevelLayouts.SetObject(visual, "presentMaskRoot", presentMask.transform);
            LevelLayouts.SetField(visual, "roomLeft", min - 2f);
            LevelLayouts.SetField(visual, "roomRight", max + 2f);
            return levelRoot;
        }

        private static GameObject CreateMaskNode(Transform parent, string name, Sprite white)
        {
            var node = new GameObject(name);
            node.transform.SetParent(parent);
            node.transform.localPosition = Vector3.zero;
            var renderer = node.AddComponent<SpriteRenderer>();
            renderer.sprite = white;
            renderer.color = new Color(1f, 1f, 1f, 0.06f);
            renderer.sortingLayerName = "EraBackground";
            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            node.AddComponent<SpriteMask>();
            return node;
        }

        // ---------------------------------------------------------------- 构建列表

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

        // ---------------------------------------------------------------- UI 工具

        private static GameObject CreateChild<T>(Transform parent, string name) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            // UI 子物体显式挂 RectTransform：普通 MonoBehaviour 面板组件不会自动带来它。
            if (child.GetComponent<RectTransform>() == null)
            {
                child.AddComponent<RectTransform>();
            }
            child.AddComponent<T>();
            return child;
        }

        /// <summary>设置 UI 元素的位置与尺寸（默认居中锚点）。</summary>
        private static void Stretch(RectTransform rect, Vector2 anchoredPos, Vector2 size, bool fullScreen = false)
        {
            if (fullScreen)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                return;
            }
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
        }

        private static Text MakeText(Transform parent, string name, string content, Font font, int fontSize,
            TextAnchor alignment, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.16f, 0.15f, 0.14f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return text;
        }

        private static Button MakeButton(Transform parent, string label, Font font, Vector2 anchoredPos)
        {
            var go = new GameObject($"Button_{label}");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.85f, 0.82f, 0.74f, 0.95f);
            var button = go.AddComponent<Button>();
            var text = MakeText(go.transform, "Label", label, font, 26, TextAnchor.MiddleCenter, Vector2.zero,
                new Vector2(240f, 44f));
            text.color = new Color(0.2f, 0.16f, 0.12f);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(260f, 54f);
            return button;
        }

        private static void SetObjectArray(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[ProjectScaffolder] 找不到数组字段 {field}（{target.GetType().Name}）。");
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
