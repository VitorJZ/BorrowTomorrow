using System;
using System.Collections.Generic;
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
    /// 场景结构自检：脚手架生成后运行，验证每个关卡场景的装配完整性。
    /// 这是策划 15.4 技术验收的自动化前置检查；关卡可玩性仍需人工试玩（策划 15.5）。
    /// 校验失败直接抛异常，使批处理以非零方式可见地失败。
    /// </summary>
    internal static class SceneValidator
    {
        internal static void ValidateAll(LevelDefinition[] catalog)
        {
            var failures = new List<string>();
            foreach (LevelDefinition def in catalog)
            {
                ValidateLevel(def, failures);
            }
            ValidateBootstrap(catalog, failures);

            if (failures.Count > 0)
            {
                string report = string.Join("\n - ", failures);
                Debug.LogError($"[SceneValidator] 校验失败 {failures.Count} 项：\n - {report}");
                throw new Exception($"[SceneValidator] 场景校验失败：{failures.Count} 项（详见编辑器日志）。");
            }
            Debug.Log("[SceneValidator] 全部场景校验通过。");
        }

        private static void ValidateLevel(LevelDefinition def, List<string> failures)
        {
            string path = $"Assets/Game/Scenes/{def.sceneName}.scene";
            if (!System.IO.File.Exists(path))
            {
                failures.Add($"{def.levelId}: 场景文件缺失 {path}");
                return;
            }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string id = def.levelId;

            LevelRoot[] rooms = FindObjects<LevelRoot>();
            int expectedRooms = id == "L01" ? 2 : 1;
            if (rooms.Length != expectedRooms)
            {
                failures.Add($"{id}: 房间数 {rooms.Length}，应为 {expectedRooms}");
            }

            int totalTemporal = 0;
            bool entrySpawnFound = false;
            foreach (LevelRoot room in rooms)
            {
                string roomId = room.RoomId;
                var roomSerialized = new SerializedObject(room);
                if (roomSerialized.FindProperty("definition").objectReferenceValue == null)
                {
                    failures.Add($"{id}/{roomId}: LevelDefinition 未引用");
                }
                if (roomSerialized.FindProperty("boundaryController").objectReferenceValue == null)
                {
                    failures.Add($"{id}/{roomId}: BoundaryController 未连接");
                }
                if (roomSerialized.FindProperty("playerPrefab").objectReferenceValue == null)
                {
                    failures.Add($"{id}/{roomId}: 玩家预制体未指定");
                }

                SerializedProperty spawnArray = roomSerialized.FindProperty("spawnPoints");
                int spawnCount = spawnArray != null ? spawnArray.arraySize : 0;
                if (spawnCount == 0)
                {
                    failures.Add($"{id}/{roomId}: 没有出生点");
                }
                for (int i = 0; i < spawnCount; i++)
                {
                    var spawn = spawnArray.GetArrayElementAtIndex(i).objectReferenceValue as SpawnPoint;
                    if (spawn != null && spawn.SpawnId == def.spawnId)
                    {
                        entrySpawnFound = true;
                    }
                }

                TemporalObjectRegistry registry = room.GetComponent<TemporalObjectRegistry>();
                if (registry == null)
                {
                    failures.Add($"{id}/{roomId}: 缺少 TemporalObjectRegistry");
                }
                else
                {
                    totalTemporal += registry.Objects.Count;
                    foreach (TemporalObject temporal in registry.Objects)
                    {
                        var temporalSerialized = new SerializedObject(temporal);
                        if (temporalSerialized.FindProperty("definition").objectReferenceValue == null)
                        {
                            failures.Add($"{id}/{roomId}: {temporal.ObjectId} 缺少 Definition");
                        }
                        if (temporalSerialized.FindProperty("pastRoot").objectReferenceValue == null
                            || temporalSerialized.FindProperty("presentRoot").objectReferenceValue == null)
                        {
                            failures.Add($"{id}/{roomId}: {temporal.ObjectId} 缺少双年代子物体");
                        }
                    }
                }

                if (room.GetComponent<LevelDirector>() == null)
                {
                    failures.Add($"{id}/{roomId}: 缺少 LevelDirector");
                }
            }

            if (!entrySpawnFound)
            {
                failures.Add($"{id}: 未找到入口出生点 {def.spawnId}");
            }

            int expectedTemporal = id switch
            {
                "L00" => 1, "L01" => 3, "L02" => 2, "L03" => 3, "L04" => 1, _ => 0,
            };
            if (totalTemporal != expectedTemporal)
            {
                failures.Add($"{id}: 年代物体数 {totalTemporal}，应为 {expectedTemporal}");
            }

            // 出口（策划 7.7）：L04 以 BoxLocated 标记通关，其余关卡应有出口门。
            var doors = FindObjects<ExitDoor>();
            int expectedDoors = id switch { "L00" => 1, "L01" => 2, "L02" => 1, "L03" => 1, "L04" => 0, _ => 1 };
            if (id == "L04")
            {
                var director = FindObjects<LevelDirector>().FirstOrDefault(d => d != null);
                var directorSerialized = new SerializedObject(director);
                if (string.IsNullOrEmpty(directorSerialized.FindProperty("completeLevelOnFlag").stringValue))
                {
                    failures.Add("L04: 既无出口门也未配置 completeLevelOnFlag");
                }
            }
            else if (doors.Length < expectedDoors)
            {
                failures.Add($"{id}: 出口门 {doors.Length} 个，应至少 {expectedDoors} 个");
            }

            // 跌落恢复区（策划 4.6）。
            int expectedKillZones = id switch { "L01" => 1, "L03" => 1, "L04" => 1, _ => 0 };
            int killZones = FindObjects<KillZone>().Length;
            if (killZones != expectedKillZones)
            {
                failures.Add($"{id}: 跌落区 {killZones} 个，应为 {expectedKillZones}");
            }

            Debug.Log($"[SceneValidator] {id} 校验完成（房间 {rooms.Length}，年代物体 {totalTemporal}，门 {doors.Length}，跌落区 {killZones}）。");
        }

        private static void ValidateBootstrap(LevelDefinition[] catalog, List<string> failures)
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene", OpenSceneMode.Single);

            Core.GameSession session = FindObjects<Core.GameSession>().FirstOrDefault();
            if (session == null)
            {
                failures.Add("Bootstrap: 缺少 GameSession");
                return;
            }
            var sessionSerialized = new SerializedObject(session);
            SerializedProperty catalogProperty = sessionSerialized.FindProperty("levelCatalog");
            int count = catalogProperty != null ? catalogProperty.arraySize : 0;
            if (count != catalog.Length)
            {
                failures.Add($"Bootstrap: 关卡目录 {count} 项，应为 {catalog.Length}");
            }

            Narrative.DialogueLibrary library = FindObjects<Narrative.DialogueLibrary>().FirstOrDefault();
            if (library == null)
            {
                failures.Add("Bootstrap: 缺少 DialogueLibrary");
                return;
            }
            var librarySerialized = new SerializedObject(library);
            int entries = librarySerialized.FindProperty("entries").arraySize;
            if (entries < 12)
            {
                failures.Add($"Bootstrap: 对白库只有 {entries} 条（应 ≥12 条占位对白）");
            }

            // 核心系统组件齐全（策划 10.3）。
            var root = session.gameObject;
            string[] required = { "SaveService", "CheckpointService", "AudioService", "SfxLibrary",
                "NarrativeDirector", "DialogueLibrary", "DialoguePlayer", "InputRouter", "Bootstrap" };
            foreach (string component in required)
            {
                if (root.GetComponent(component) == null)
                {
                    failures.Add($"Bootstrap: 系统根缺少 {component}");
                }
            }
            Debug.Log($"[SceneValidator] Bootstrap 校验完成（关卡目录 {count}，对白 {entries} 条）。");
        }

        private static T[] FindObjects<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }
}
