using System.Collections.Generic;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Level;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.Player;
using BorrowTomorrow.TimeBoundary;
using UnityEditor;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>
    /// 灰盒关卡布局构建器：按策划方案第 7 章的锚点坐标生成每关内容。
    /// 全部几何均为简单形状占位（白方块/斜坡），正式美术按 Docs/10 资源清单替换。
    /// 布局规则：
    /// - 必经水平跳跃 ≤1.2、向上跳 ≤0.6（策划 6.2）；
    /// - 关键隔断缺口 > 理论最大跳距 2.25（策划 16 风险二：谜题不能被普通跳跃绕过）；
    /// - 梯顶平台为单向碰撞（PlatformEffector），攀爬可穿过、落下可站稳；
    /// - 恒定站稳面挂高摩擦 PhysicsMaterial（策划 7.4：安全砖台可静止站立）。
    /// </summary>
    internal static class LevelLayouts
    {
        internal sealed class RoomContext
        {
            public Transform Content;
            public Sprite White;
            public PhysicsMaterial2D Friction;
            public int PlayerMaskValue;
            public Dictionary<string, string> TemporalDefPaths;
            public Dictionary<string, string> DialoguePaths;

            /// <summary>年代物体定义查找：按路径惰性加载（始终取得引擎当前的规范实例，
            /// 规避批处理中资产实例在导入/场景操作期间被销毁导致的伪 null 引用）。</summary>
            public TemporalObjectDefinition FindTemporalDef(string id)
            {
                if (!TemporalDefPaths.TryGetValue(id, out string path))
                {
                    return null;
                }
                return AssetDatabase.LoadAssetAtPath<TemporalObjectDefinition>(path);
            }

            /// <summary>对白条目查找：按路径惰性加载（同上）。</summary>
            public DialogueEntry FindDialogue(string id)
            {
                if (!DialoguePaths.TryGetValue(id, out string path))
                {
                    return null;
                }
                return AssetDatabase.LoadAssetAtPath<DialogueEntry>(path);
            }
        }

        // ------------------------------------------------------------ 通用构建件

        /// <summary>
        /// 按世界尺寸渲染 Sprite。用 Sliced 绘制模式 + renderer.size：
        /// Simple 模式下 size 不生效（灰盒曾全部渲染成贴图原尺寸小点）；
        /// Tiled 模式按贴图尺寸生成真实网格（4×4 贴图铺 700×25 瓦片 ≈ 7 万顶点报错）；
        /// Sliced 对零边框纯色贴图就是一次拉伸，恒定 4 顶点，且不影响同物体上的碰撞体。
        /// </summary>
        internal static void SizeSprite(SpriteRenderer renderer, Vector2 worldSize)
        {
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = worldSize;
        }

        /// <summary>灰盒盒子：SpriteRenderer + 可选 BoxCollider2D（可旋转）。</summary>
        internal static GameObject Box(RoomContext ctx, string name, Vector2 center, Vector2 size, Color color,
            string sortingLayer, string layerName, bool solid, bool oneWay = false, float angle = 0f)
        {
            var box = new GameObject(name);
            box.transform.SetParent(ctx.Content);
            box.transform.position = new Vector3(center.x, center.y, 0f);
            box.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                box.layer = layer;
            }
            var renderer = box.AddComponent<SpriteRenderer>();
            renderer.sprite = ctx.White;
            renderer.color = color;
            renderer.sortingLayerName = sortingLayer;
            SizeSprite(renderer, size);
            if (solid)
            {
                var collider = box.AddComponent<BoxCollider2D>();
                collider.size = size;
                collider.sharedMaterial = ctx.Friction;
                if (oneWay)
                {
                    collider.usedByEffector = true;
                    var effector = box.AddComponent<PlatformEffector2D>();
                    effector.useOneWay = true;
                }
            }
            return box;
        }

        /// <summary>斜坡盒子：p1→p2 为上表面连线，厚度向下延伸（树枝缓坡）。</summary>
        internal static GameObject SlopeBox(RoomContext ctx, string name, Vector2 p1, Vector2 p2, float thickness,
            Color color, string sortingLayer, string layerName)
        {
            Vector2 direction = (p2 - p1).normalized;
            Vector2 upNormal = new Vector2(-direction.y, direction.x);
            if (upNormal.y < 0f)
            {
                upNormal = -upNormal; // 保证法线朝上
            }
            Vector2 center = (p1 + p2) * 0.5f - upNormal * (thickness * 0.5f);
            float length = Vector2.Distance(p1, p2);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Box(ctx, name, center, new Vector2(length, thickness), color, sortingLayer, layerName,
                solid: true, oneWay: false, angle);
        }

        /// <summary>出生点（策划 12.2：优先安全出生点 ID）。</summary>
        internal static SpawnPoint Spawn(RoomContext ctx, string id, Vector2 feetPos)
        {
            var spawn = new GameObject($"Spawn_{id}");
            spawn.transform.SetParent(ctx.Content);
            spawn.transform.position = new Vector3(feetPos.x, feetPos.y, 0f);
            var point = spawn.AddComponent<SpawnPoint>();
            SetField(point, "spawnId", id);
            SetField(point, "placementOffset", Vector2.zero);
            return point;
        }

        /// <summary>检查点标记（策划 7.x：触发采集空间快照）。</summary>
        internal static CheckpointMarker Checkpoint(RoomContext ctx, string id, Vector2 center)
        {
            var marker = new GameObject($"Checkpoint_{id}");
            marker.transform.SetParent(ctx.Content);
            marker.transform.position = new Vector3(center.x, center.y, 0f);
            var collider = marker.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.2f, 1.6f);
            var component = marker.AddComponent<CheckpointMarker>();
            SetField(component, "checkpointId", id);
            SetField(component, "playerMask", ctx.PlayerMaskValue);
            return component;
        }

        /// <summary>跌落恢复区（策划 4.6）。</summary>
        internal static void KillZone(RoomContext ctx, Vector2 center, Vector2 size)
        {
            var zone = new GameObject("KillZone");
            zone.transform.SetParent(ctx.Content);
            zone.transform.position = new Vector3(center.x, center.y, 0f);
            int layer = LayerMask.NameToLayer("KillZone");
            if (layer >= 0)
            {
                zone.layer = layer;
            }
            var collider = zone.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = size;
            var kill = zone.AddComponent<KillZone>();
            SetField(kill, "playerMask", ctx.PlayerMaskValue);
        }

        /// <summary>出口门（策划 7.7：E 确认转场，不因接近边缘误切关）。</summary>
        internal static ExitDoor Door(RoomContext ctx, string prompt, Vector2 center,
            ExitDoor.TransitionMode mode, LevelRoot targetRoom, string targetSpawnId)
        {
            var door = new GameObject($"Exit_{prompt}");
            door.transform.SetParent(ctx.Content);
            door.transform.position = new Vector3(center.x, center.y, 0f);
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer >= 0)
            {
                door.layer = layer;
            }
            var collider = door.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.6f, 2.4f);
            var exit = door.AddComponent<ExitDoor>();
            SetField(exit, "prompt", prompt);
            SetField(exit, "mode", (int)mode);
            if (targetRoom != null)
            {
                SetObject(exit, "targetRoom", targetRoom);
            }
            if (!string.IsNullOrEmpty(targetSpawnId))
            {
                SetField(exit, "targetSpawnId", targetSpawnId);
            }
            return exit;
        }

        /// <summary>叙事物（调查：校服/照片/账本/暗格，策划 3.1 渠道 D）。</summary>
        internal static NarrativeInteractable Narrative(RoomContext ctx, string id, string prompt, Vector2 center,
            Vector2 size, string dialogueEntryId, DialoguePriority priority, bool replayable)
        {
            var obj = new GameObject($"Narrative_{id}");
            obj.transform.SetParent(ctx.Content);
            obj.transform.position = new Vector3(center.x, center.y, 0f);
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer >= 0)
            {
                obj.layer = layer;
            }
            var collider = obj.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = size;
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = ctx.White;
            renderer.color = new Color(0.95f, 0.85f, 0.3f, 0.85f);
            renderer.sortingLayerName = "EraPlatform";
            SizeSprite(renderer, size);
            var component = obj.AddComponent<NarrativeInteractable>();
            SetField(component, "interactId", id);
            SetField(component, "prompt", prompt);
            DialogueEntry entry = ctx.FindDialogue(dialogueEntryId);
            if (entry != null)
            {
                // 直接字段赋值 + SetDirty：批处理下最可靠的资产引用注入方式。
                component.EditorConfigure(entry, priority, replayable);
            }
            else
            {
                SetField(component, "replayable", replayable);
                Debug.LogWarning($"[LevelLayouts] 叙事物 {id} 找不到对白条目 {dialogueEntryId}");
            }
            return component;
        }

        /// <summary>年代物体（策划 4.3：恒定/年代/叙事/装饰四类；本方法建年代物体）。</summary>
        internal static TemporalObject Temporal(RoomContext ctx, string definitionId, Vector2 position)
        {
            var obj = new GameObject($"Temporal_{definitionId}");
            obj.transform.SetParent(ctx.Content);
            obj.transform.position = new Vector3(position.x, position.y, 0f);
            int layer = LayerMask.NameToLayer("Temporal");
            if (layer >= 0)
            {
                obj.layer = layer;
            }
            var temporal = obj.AddComponent<TemporalObject>();
            TemporalObjectDefinition definition = ctx.FindTemporalDef(definitionId);
            if (definition == null)
            {
                Debug.LogWarning($"[LevelLayouts] 找不到年代物体定义 {definitionId}");
            }
            else
            {
                // 直接字段赋值 + SetDirty：批处理下最可靠的资产引用注入方式。
                temporal.EditorConfigure(definition);
            }
            return temporal;
        }

        /// <summary>年代物体的某个年代状态子物体（策划 11.2：物理与渲染同步整体切换）。</summary>
        internal static GameObject EraChild(RoomContext ctx, TemporalObject host, string era)
        {
            var child = new GameObject(era);
            child.transform.SetParent(host.transform, false);
            child.layer = host.gameObject.layer;
            return child;
        }

        /// <summary>梯子内容：视觉立杆+横档 + 攀爬 Trigger（挂到过去状态子物体，策划 11.3）。
        /// centerX/bottomY/topY 均为世界坐标。</summary>
        internal static void LadderContent(RoomContext ctx, GameObject eraChild,
            float centerX, float bottomY, float topY)
        {
            float height = topY - bottomY;
            float midY = (bottomY + topY) * 0.5f;
            Vector2 parentWorld = eraChild.transform.position;

            // 视觉立杆（无碰撞）。
            var bar = new GameObject("Bar");
            bar.transform.SetParent(eraChild.transform, false);
            bar.transform.localPosition = new Vector3(centerX, midY, 0f) - (Vector3)parentWorld;
            var barRenderer = bar.AddComponent<SpriteRenderer>();
            barRenderer.sprite = ctx.White;
            barRenderer.color = new Color(0.55f, 0.38f, 0.22f);
            barRenderer.sortingLayerName = "EraPlatform";
            SizeSprite(barRenderer, new Vector2(0.18f, height));

            // 横档（无碰撞，每 0.5 单位一根）。
            for (float y = bottomY + 0.3f; y < topY; y += 0.5f)
            {
                var rung = new GameObject($"Rung_{y:0.0}");
                rung.transform.SetParent(eraChild.transform, false);
                rung.transform.localPosition = new Vector3(centerX, y, 0f) - (Vector3)parentWorld;
                var rungRenderer = rung.AddComponent<SpriteRenderer>();
                rungRenderer.sprite = ctx.White;
                rungRenderer.color = new Color(0.7f, 0.55f, 0.35f);
                rungRenderer.sortingLayerName = "EraPlatform";
                SizeSprite(rungRenderer, new Vector2(0.55f, 0.07f));
            }

            // 攀爬触发区（梯顶超出平台面 0.7，便于爬出后落到相邻平台，策划 11.3）。
            // 触发器放独立的 Ladder 层：地面探测掩码（Ground|Temporal）不含它，玩家贴梯悬空不会被误判为落地。
            var zone = new GameObject("LadderZone");
            zone.transform.SetParent(eraChild.transform, false);
            zone.transform.localPosition = new Vector3(centerX, midY, 0f) - (Vector3)parentWorld;
            int ladderLayer = LayerMask.NameToLayer("Ladder");
            if (ladderLayer >= 0)
            {
                zone.layer = ladderLayer;
            }
            var trigger = zone.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.8f, height);
            zone.AddComponent<LadderZone>();
        }

        // ------------------------------------------------------------ 私有字段工具

        internal static void SetField(UnityEngine.Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[LevelLayouts] 找不到字段 {field}（{target.GetType().Name}）。");
                return;
            }
            switch (value)
            {
                case float f: property.floatValue = f; break;
                case int i: property.intValue = i; break;
                case bool b: property.boolValue = b; break;
                case string s: property.stringValue = s; break;
                case Vector2 v: property.vector2Value = v; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetObject(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property != null)
            {
                property.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        internal static void SetRect(UnityEngine.Object target, string field, Rect rect)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property != null)
            {
                property.rectValue = rect;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[LevelLayouts] 找不到字段 {field}（{target.GetType().Name}）。");
            }
        }

        // ------------------------------------------------------------ 关卡布局（策划第 7 章）

        internal static readonly Color ConstantColor = new Color(0.62f, 0.58f, 0.52f);
        internal static readonly Color PastColor = new Color(0.86f, 0.68f, 0.42f);
        internal static readonly Color PresentColor = new Color(0.58f, 0.72f, 0.64f);
        internal static readonly Color WallColor = new Color(0.35f, 0.33f, 0.3f);
        internal static readonly Color DoorColor = new Color(0.5f, 0.3f, 0.2f);

        /// <summary>L00 院门（策划 7.1）：起点 x=2，栅栏 x=10，出口 x=23，初始全过去 B=26。</summary>
        internal static List<SpawnPoint> BuildL00(RoomContext ctx)
        {
            Box(ctx, "Ground", new Vector2(13f, -0.5f), new Vector2(28f, 1f), ConstantColor, "ConstantStructure", "Ground", true);
            Box(ctx, "WallL", new Vector2(0f, 2f), new Vector2(0.6f, 10f), WallColor, "ConstantStructure", "Ground", true);
            Box(ctx, "WallR", new Vector2(26f, 2f), new Vector2(0.6f, 10f), WallColor, "ConstantStructure", "Ground", true);

            // 栅栏（锚点 x=10）：过去完整阻挡（高于跳高 0.93）；现在下部留成年人缺口（策划 7.1）。
            TemporalObject fence = Temporal(ctx, "L00_fence_gate", new Vector2(10f, 0f));
            GameObject past = EraChild(ctx, fence, "Past");
            past.transform.localPosition = Vector3.zero;
            BoxChild(ctx, past.transform, "FencePast", new Vector2(10f, 1.7f), new Vector2(1.2f, 3.4f), PastColor);
            GameObject present = EraChild(ctx, fence, "Present");
            present.transform.localPosition = Vector3.zero;
            BoxChild(ctx, present.transform, "FencePresent", new Vector2(10f, 2.65f), new Vector2(1.2f, 1.5f), PresentColor);
            SetObject(fence, "pastRoot", past);
            SetObject(fence, "presentRoot", present);

            var spawns = new List<SpawnPoint>
            {
                Spawn(ctx, "spawn_gate_out", new Vector2(2f, 0.05f)),
                Spawn(ctx, "cp_L00_courtyard", new Vector2(14f, 0.05f)),
            };
            Checkpoint(ctx, "cp_L00_courtyard", new Vector2(14f, 0.8f));
            Door(ctx, "推门进院", new Vector2(23f, 1.2f), ExitDoor.TransitionMode.NextLevel, null, null);
            return spawns;
        }

        /// <summary>L01A 水渠盖板（策划 7.2）：缺口宽 4，盖板锚点 x=12，初始全现在（盖板腐坏）。
        /// roomB 为同场景的 L01B 房间根（过桥门 SameSceneRoom 转场目标）。</summary>
        internal static List<SpawnPoint> BuildL01A(RoomContext ctx, LevelRoot roomB)
        {
            Box(ctx, "BankL", new Vector2(5f, -0.5f), new Vector2(10f, 1f), ConstantColor, "ConstantStructure", "Ground", true);
            Box(ctx, "BankR", new Vector2(17.5f, -0.5f), new Vector2(7f, 1f), ConstantColor, "ConstantStructure", "Ground", true);
            Box(ctx, "WallL", new Vector2(0f, 2f), new Vector2(0.6f, 10f), WallColor, "ConstantStructure", "Ground", true);
            Box(ctx, "WallR", new Vector2(20f, 2f), new Vector2(0.6f, 10f), WallColor, "ConstantStructure", "Ground", true);
            KillZone(ctx, new Vector2(12f, -4.5f), new Vector2(6f, 1f));

            // 盖板（锚点 x=12）：过去完好可站；现在腐坏无碰撞（策划 7.2 安全阻挡教学）。
            TemporalObject plank = Temporal(ctx, "L01A_plank_bridge", new Vector2(12f, 0f));
            GameObject past = EraChild(ctx, plank, "Past");
            past.transform.localPosition = Vector3.zero;
            BoxChild(ctx, past.transform, "PlankPast", new Vector2(12f, -0.175f), new Vector2(4.4f, 0.35f), PastColor);
            GameObject present = EraChild(ctx, plank, "Present");
            present.transform.localPosition = Vector3.zero;
            BoxChild(ctx, present.transform, "StubL", new Vector2(10.8f, 0.1f), new Vector2(1.1f, 0.28f),
                new Color(PresentColor.r, PresentColor.g, PresentColor.b, 0.6f), solid: false, angle: 12f);
            BoxChild(ctx, present.transform, "StubR", new Vector2(13.1f, 0.02f), new Vector2(1.1f, 0.28f),
                new Color(PresentColor.r, PresentColor.g, PresentColor.b, 0.6f), solid: false, angle: -18f);
            SetObject(plank, "pastRoot", past);
            SetObject(plank, "presentRoot", present);

            var spawns = new List<SpawnPoint>
            {
                Spawn(ctx, "spawn_courtyard_in", new Vector2(2f, 0.05f)),
                Spawn(ctx, "cp_L01A_bank", new Vector2(15.5f, 0.05f)),
            };
            Checkpoint(ctx, "cp_L01A_bank", new Vector2(15.5f, 0.8f));
            Door(ctx, "过桥进院", new Vector2(19.5f, 1.2f), ExitDoor.TransitionMode.SameSceneRoom, roomB, "spawn_L01B_in");
            return spawns;
        }

        /// <summary>
        /// L01B 旧踏台与新树枝（策划 7.3，世界坐标整体偏移 +40）：
        /// 过去阶梯锚点 48；树枝整体锚点 58；初始全过去（有阶梯、无成年枝条）。
        /// 标准解：B 放在 48 与 58 之间，同时保留过去阶梯与现在树枝（策划 7.3）。
        /// </summary>
        internal static List<SpawnPoint> BuildL01B(RoomContext ctx)
        {
            Box(ctx, "Ground", new Vector2(54.5f, -0.5f), new Vector2(29f, 1f), ConstantColor, "ConstantStructure", "Ground", true);
            Box(ctx, "WallL", new Vector2(40f, 2f), new Vector2(0.6f, 10f), WallColor, "ConstantStructure", "Ground", true);
            Box(ctx, "WallR", new Vector2(69f, 2f), new Vector2(0.6f, 10f), WallColor, "ConstantStructure", "Ground", true);

            // 梯顶平台（单向碰撞：攀爬可穿过，落下可站稳）。
            Box(ctx, "Ledge", new Vector2(47.75f, 1.8f), new Vector2(5.5f, 0.4f), ConstantColor,
                "ConstantStructure", "Ground", true, oneWay: true);

            // 旧木梯（过去可爬，现在断裂）。
            TemporalObject ladder = Temporal(ctx, "L01B_wood_ladder", new Vector2(48f, 0f));
            GameObject past = EraChild(ctx, ladder, "Past");
            past.transform.localPosition = Vector3.zero;
            LadderContent(ctx, past, 48f, 0.2f, 2.7f);
            GameObject present = EraChild(ctx, ladder, "Present");
            present.transform.localPosition = Vector3.zero;
            BoxChild(ctx, present.transform, "BrokenStub", new Vector2(48f, 0.55f), new Vector2(0.15f, 1f),
                new Color(0.4f, 0.3f, 0.18f), solid: false, angle: 28f);
            SetObject(ladder, "pastRoot", past);
            SetObject(ladder, "presentRoot", present);

            // 院子里的树（锚点 x=58）：过去幼树无可走枝条；现在粗枝接向出口。
            TemporalObject tree = Temporal(ctx, "L01B_tree_branch", new Vector2(58f, 0f));
            GameObject treePast = EraChild(ctx, tree, "Past");
            treePast.transform.localPosition = Vector3.zero;
            BoxChild(ctx, treePast.transform, "Sapling", new Vector2(58f, 0.75f), new Vector2(0.3f, 1.5f),
                new Color(0.45f, 0.65f, 0.35f), solid: false);
            GameObject treePresent = EraChild(ctx, tree, "Present");
            treePresent.transform.localPosition = Vector3.zero;
            BoxChild(ctx, treePresent.transform, "Trunk", new Vector2(58f, 1.1f), new Vector2(0.7f, 2.2f),
                new Color(0.4f, 0.3f, 0.2f), solid: false);
            SlopeChild(ctx, treePresent.transform, "BranchA", new Vector2(50.9f, 2.12f), new Vector2(56f, 2.47f), 0.35f);
            SlopeChild(ctx, treePresent.transform, "BranchB", new Vector2(56f, 2.47f), new Vector2(61.5f, 2.82f), 0.35f);
            SlopeChild(ctx, treePresent.transform, "BranchC", new Vector2(61.5f, 2.82f), new Vector2(66f, 3.15f), 0.35f);
            SetObject(tree, "pastRoot", treePast);
            SetObject(tree, "presentRoot", treePresent);

            // 出口平台与门（树枝终点 y≈3.15，策划 7.3：终点 x=26→世界 66）。
            Box(ctx, "ExitLedge", new Vector2(67.4f, 2.95f), new Vector2(3.2f, 0.4f), ConstantColor,
                "ConstantStructure", "Ground", true);

            var spawns = new List<SpawnPoint>
            {
                Spawn(ctx, "spawn_L01B_in", new Vector2(42f, 0.05f)),
                Spawn(ctx, "cp_L01B_entry", new Vector2(42.5f, 0.05f)),
            };
            Checkpoint(ctx, "cp_L01B_entry", new Vector2(42.5f, 0.8f));
            Door(ctx, "上楼", new Vector2(68.2f, 4.35f), ExitDoor.TransitionMode.NextLevel, null, null);
            return spawns;
        }

        /// <summary>
        /// L02 缝纫间外台（策划 7.4）：维修踏台 x=6、恒定砖台 x=8.5~13.2、旧木梯 x=13、地面起点 x=23。
        /// 初始全现在（木梯断裂、踏台悬空不可达）→ 必须先右移边界修梯上砖台，再左移边界放踏台。
        /// </summary>
        internal static List<SpawnPoint> BuildL02(RoomContext ctx)
        {
            Box(ctx, "Ground", new Vector2(13f, -0.5f), new Vector2(28f, 1f), ConstantColor, "ConstantStructure", "Ground", true);
            Box(ctx, "WallL", new Vector2(0f, 2f), new Vector2(0.6f, 10f), WallColor, "ConstantStructure", "Ground", true);
            Box(ctx, "WallR", new Vector2(26f, 2f), new Vector2(0.6f, 10f), WallColor, "ConstantStructure", "Ground", true);

            // 恒定砖台（中途检查点；单向碰撞供梯子穿过）。
            Box(ctx, "BrickPlatform", new Vector2(10.85f, 2.75f), new Vector2(4.7f, 0.5f),
                new Color(0.72f, 0.55f, 0.45f), "ConstantStructure", "Ground", true, oneWay: true);

            // 旧木梯（过去可爬：地面 y=0 → 砖台 y=3；现在断裂）。
            TemporalObject ladder = Temporal(ctx, "L02_old_ladder", new Vector2(13f, 0f));
            GameObject past = EraChild(ctx, ladder, "Past");
            past.transform.localPosition = Vector3.zero;
            LadderContent(ctx, past, 13f, 0.2f, 3.7f);
            GameObject present = EraChild(ctx, ladder, "Present");
            present.transform.localPosition = Vector3.zero;
            BoxChild(ctx, present.transform, "BrokenStub", new Vector2(13f, 0.5f), new Vector2(0.15f, 0.9f),
                new Color(0.4f, 0.3f, 0.18f), solid: false, angle: 25f);
            SetObject(ladder, "pastRoot", past);
            SetObject(ladder, "presentRoot", present);

            // 现在维修踏台（过去完全不存在；现在形成砖台通向左侧出口的路径，策划 7.4）。
            TemporalObject maintenance = Temporal(ctx, "L02_maintenance", new Vector2(6f, 0f));
            GameObject maintenancePast = EraChild(ctx, maintenance, "Past");
            maintenancePast.transform.localPosition = Vector3.zero;
            GameObject maintenancePresent = EraChild(ctx, maintenance, "Present");
            maintenancePresent.transform.localPosition = Vector3.zero;
            BoxChild(ctx, maintenancePresent.transform, "Platform", new Vector2(4.75f, 2.75f), new Vector2(7.5f, 0.5f),
                PresentColor);
            SetObject(maintenance, "pastRoot", maintenancePast);
            SetObject(maintenance, "presentRoot", maintenancePresent);

            var spawns = new List<SpawnPoint>
            {
                Spawn(ctx, "spawn_sewing_ground", new Vector2(23f, 0.05f)),
                Spawn(ctx, "cp_L02_brick", new Vector2(11.5f, 3.05f)),
            };
            Checkpoint(ctx, "cp_L02_brick", new Vector2(11.5f, 3.85f));
            Door(ctx, "进缝纫间", new Vector2(1.6f, 4.2f), ExitDoor.TransitionMode.NextLevel, null, null);
            return spawns;
        }

        /// <summary>
        /// L03 楼上回廊（策划 7.5）：同一上层高度 y=3，下方跌落区。
        /// 初始全现在（地板缺失、踏板可用、柜子挡路）。
        /// 标准解：B 在 7 与 15 之间（过去地板+现在踏板）→ 恒定门槛 → B 推过 24（柜子回到空位）→ 照片区。
        /// </summary>
        internal static List<SpawnPoint> BuildL03(RoomContext ctx)
        {
            Box(ctx, "EntryLedge", new Vector2(2.75f, 2.8f), new Vector2(3.5f, 0.4f), ConstantColor,
                "ConstantStructure", "Ground", true);
            Box(ctx, "WallL", new Vector2(0.7f, 3f), new Vector2(0.6f, 10f), WallColor, "ConstantStructure", "Ground", true);
            Box(ctx, "WallR", new Vector2(30.7f, 3f), new Vector2(0.6f, 10f), WallColor, "ConstantStructure", "Ground", true);

            // 过去木地板（锚点 7，区段 4.5~10）。
            TemporalObject floor = Temporal(ctx, "L03_floor", new Vector2(7f, 0f));
            GameObject floorPast = EraChild(ctx, floor, "Past");
            floorPast.transform.localPosition = Vector3.zero;
            BoxChild(ctx, floorPast.transform, "Plank", new Vector2(7.25f, 2.8f), new Vector2(5.5f, 0.4f), PastColor);
            GameObject floorPresent = EraChild(ctx, floor, "Present");
            floorPresent.transform.localPosition = Vector3.zero;
            SetObject(floor, "pastRoot", floorPast);
            SetObject(floor, "presentRoot", floorPresent);

            // 现在金属踏板（锚点 15，区段 10~18）。
            TemporalObject walkway = Temporal(ctx, "L03_walkway", new Vector2(15f, 0f));
            GameObject walkwayPast = EraChild(ctx, walkway, "Past");
            walkwayPast.transform.localPosition = Vector3.zero;
            GameObject walkwayPresent = EraChild(ctx, walkway, "Present");
            walkwayPresent.transform.localPosition = Vector3.zero;
            BoxChild(ctx, walkwayPresent.transform, "Plate", new Vector2(14f, 2.8f), new Vector2(8f, 0.4f),
                new Color(0.62f, 0.68f, 0.72f));
            SetObject(walkway, "pastRoot", walkwayPast);
            SetObject(walkway, "presentRoot", walkwayPresent);

            // 恒定石门槛（策划 7.5 检查点）与门槛后方的恒定短地面（到照片区）。
            Box(ctx, "Threshold", new Vector2(20f, 2.75f), new Vector2(4f, 0.5f), new Color(0.7f, 0.7f, 0.66f),
                "ConstantStructure", "Ground", true);
            Box(ctx, "FloorToPhoto", new Vector2(26.25f, 2.8f), new Vector2(8.5f, 0.4f), ConstantColor,
                "ConstantStructure", "Ground", true);

            // 储物柜（锚点 24）：现在固定到顶的隔间（无立顶可站）；过去空位。
            TemporalObject cabinet = Temporal(ctx, "L03_cabinet", new Vector2(24f, 0f));
            GameObject cabinetPast = EraChild(ctx, cabinet, "Past");
            cabinetPast.transform.localPosition = Vector3.zero;
            GameObject cabinetPresent = EraChild(ctx, cabinet, "Present");
            cabinetPresent.transform.localPosition = Vector3.zero;
            BoxChild(ctx, cabinetPresent.transform, "Cabinet", new Vector2(24f, 4.4f), new Vector2(2f, 3.6f),
                new Color(0.5f, 0.52f, 0.56f));
            SetObject(cabinet, "pastRoot", cabinetPast);
            SetObject(cabinet, "presentRoot", cabinetPresent);

            KillZone(ctx, new Vector2(13f, -3.5f), new Vector2(24f, 1f));

            var spawns = new List<SpawnPoint>
            {
                Spawn(ctx, "spawn_corridor_in", new Vector2(2f, 3.05f)),
                Spawn(ctx, "cp_L03_threshold", new Vector2(20f, 3.05f)),
                Spawn(ctx, "cp_L03_photo", new Vector2(26.5f, 3.05f)),
            };
            Checkpoint(ctx, "cp_L03_threshold", new Vector2(20f, 3.85f));
            Checkpoint(ctx, "cp_L03_photo", new Vector2(26.5f, 3.85f));
            Door(ctx, "推开阁楼门", new Vector2(29.8f, 4.2f), ExitDoor.TransitionMode.NextLevel, null, null);
            return spawns;
        }

        /// <summary>
        /// L04 阁楼窗外（策划 7.6）：树干锚点 x=4，窗台 x=21.5~30.5（y=3），暗格 x=26、y=4.5。
        /// 初始全过去（幼树）；标准解：E 看暗格 → B&lt;4 让整棵树成年 → 窗台跳上粗枝 → 沿缓坡到暗格 → E 触摸 → 通关。
        /// </summary>
        internal static List<SpawnPoint> BuildL04(RoomContext ctx)
        {
            // Keep the standing/adjustment area outside the future branch bounds.
            // The previous spawn was inside the underside of the high niche ledge.
            Box(ctx, "WindowPlatform", new Vector2(20.5f, 2.8f), new Vector2(3f, 0.4f), ConstantColor,
                "ConstantStructure", "Ground", true);
            Box(ctx, "WallL", new Vector2(0f, 3f), new Vector2(0.6f, 14f), WallColor, "ConstantStructure", "Ground", true);
            Box(ctx, "WallR", new Vector2(30.8f, 3f), new Vector2(0.6f, 14f), WallColor, "ConstantStructure", "Ground", true);

            // 暗格窗台（恒定小平台，y=4.5）与暗格视觉。
            Box(ctx, "CompartmentLedge", new Vector2(26.5f, 4.3f), new Vector2(2.2f, 0.4f), ConstantColor,
                "ConstantStructure", "Ground", true);
            Box(ctx, "CompartmentVisual", new Vector2(26.5f, 5.15f), new Vector2(1.4f, 1f),
                new Color(0.28f, 0.3f, 0.36f), "ConstantStructure", "Default", false);

            // 整棵树（锚点 x=4，策划 11.5：全部通行枝条同步切换）。
            TemporalObject tree = Temporal(ctx, "L04_tree", new Vector2(4f, 0f));
            GameObject treePast = EraChild(ctx, tree, "Past");
            treePast.transform.localPosition = Vector3.zero;
            BoxChild(ctx, treePast.transform, "Sapling", new Vector2(4f, -5f), new Vector2(0.4f, 2f),
                new Color(0.45f, 0.65f, 0.35f), solid: false);
            GameObject treePresent = EraChild(ctx, tree, "Present");
            treePresent.transform.localPosition = Vector3.zero;
            BoxChild(ctx, treePresent.transform, "YardGround", new Vector2(14f, -6.5f), new Vector2(32f, 1f),
                new Color(0.42f, 0.5f, 0.38f), solid: false);
            BoxChild(ctx, treePresent.transform, "Trunk", new Vector2(4f, -3.5f), new Vector2(0.8f, 6f),
                new Color(0.4f, 0.3f, 0.2f), solid: false);
            SlopeChild(ctx, treePresent.transform, "BranchA", new Vector2(21.8f, 3.28f), new Vector2(23.4f, 3.91f), 0.35f);
            SlopeChild(ctx, treePresent.transform, "BranchB", new Vector2(23.4f, 3.91f), new Vector2(25.4f, 4.52f), 0.35f);
            SetObject(tree, "pastRoot", treePast);
            SetObject(tree, "presentRoot", treePresent);

            KillZone(ctx, new Vector2(14f, -3.5f), new Vector2(34f, 1f));

            var spawns = new List<SpawnPoint>
            {
                Spawn(ctx, "spawn_attic_window", new Vector2(20.3f, 3.05f)),
                Spawn(ctx, "cp_L04_window", new Vector2(20.3f, 3.05f)),
            };
            Checkpoint(ctx, "cp_L04_window", new Vector2(20.3f, 3.85f));
            return spawns;
        }

        /// <summary>尾声（策划 3.9）：两天后病房，铁盒与短信，读完后离开 → 制作名单。</summary>
        internal static List<SpawnPoint> BuildEpilogue(RoomContext ctx)
        {
            Box(ctx, "Ground", new Vector2(8f, -0.5f), new Vector2(18f, 1f), new Color(0.9f, 0.88f, 0.82f),
                "ConstantStructure", "Ground", true);
            Box(ctx, "Bed", new Vector2(3f, 0.6f), new Vector2(2.4f, 1.2f), new Color(0.85f, 0.87f, 0.82f),
                "ConstantStructure", "Ground", false);
            Box(ctx, "Table", new Vector2(7f, 0.5f), new Vector2(1.4f, 1f), new Color(0.75f, 0.72f, 0.65f),
                "ConstantStructure", "Ground", false);
            Box(ctx, "WallL", new Vector2(0f, 2f), new Vector2(0.6f, 10f), WallColor, "ConstantStructure", "Ground", true);
            Box(ctx, "WallR", new Vector2(15.7f, 2f), new Vector2(0.6f, 10f), WallColor, "ConstantStructure", "Ground", true);

            var spawns = new List<SpawnPoint>
            {
                Spawn(ctx, "spawn_epilogue", new Vector2(1.5f, 0.05f)),
            };
            Door(ctx, "离开", new Vector2(13.5f, 1.2f), ExitDoor.TransitionMode.NextLevel, null, null);
            return spawns;
        }

        // ------------------------------------------------------------ 子物体盒子/斜坡（挂在年代状态子物体下）

        private static void BoxChild(RoomContext ctx, Transform parent, string name, Vector2 center, Vector2 size,
            Color color, bool solid = true, float angle = 0f)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent, false);
            // center 为世界坐标：换算到父物体（年代状态子物体）的局部坐标。
            box.transform.localPosition = new Vector3(center.x, center.y, 0f) - parent.position;
            box.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            box.layer = parent.gameObject.layer;
            var renderer = box.AddComponent<SpriteRenderer>();
            renderer.sprite = ctx.White;
            renderer.color = color;
            renderer.sortingLayerName = "EraPlatform";
            SizeSprite(renderer, size);
            if (solid)
            {
                var collider = box.AddComponent<BoxCollider2D>();
                collider.size = size;
                collider.sharedMaterial = ctx.Friction;
            }
        }

        private static void SlopeChild(RoomContext ctx, Transform parent, string name, Vector2 p1, Vector2 p2,
            float thickness)
        {
            Vector2 direction = (p2 - p1).normalized;
            Vector2 upNormal = new Vector2(-direction.y, direction.x);
            if (upNormal.y < 0f)
            {
                upNormal = -upNormal;
            }
            Vector2 center = (p1 + p2) * 0.5f - upNormal * (thickness * 0.5f);
            float length = Vector2.Distance(p1, p2);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            BoxChild(ctx, parent, name, center, new Vector2(length, thickness), PresentColor, solid: true, angle);
        }
    }
}
