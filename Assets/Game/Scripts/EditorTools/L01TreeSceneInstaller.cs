using System;
using System.Linq;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>Moves the authored tree courtyard and its visual controller into an independent scene.</summary>
    public static class L01TreeSceneInstaller
    {
        public const string ScenePath = "Assets/Game/Scenes/L01_TreeCourtyard.scene";
        public const string DefinitionPath = "Assets/Game/Data/Levels/L01B_L01_TreeCourtyard.asset";
        // Older scaffolds still contain both rooms until Install runs.
        public static string AuthoredTreeScenePath => System.IO.File.Exists(ScenePath)
            ? ScenePath : "Assets/Game/Scenes/L01_Courtyard.scene";

        [MenuItem("BorrowTomorrow/拆分 L01 水渠与庭院场景")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty && !EditorSceneManager.SaveScene(SceneManager.GetSceneAt(i)))
                    throw new InvalidOperationException("Could not save open scene.");
            try
            {
                var outside = EditorSceneManager.OpenScene("Assets/Game/Scenes/L01_Courtyard.scene");
                var rooms = outside.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelRoot>(true)).ToArray();
                var canal = rooms.Single(r => r.RoomId == "L01A");
                var tree = rooms.SingleOrDefault(r => r.RoomId == "L01B");
                var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(DefinitionPath);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<LevelDefinition>();
                    AssetDatabase.CreateAsset(definition, DefinitionPath);
                }
                definition.levelId = "L01B";
                definition.sceneName = "L01_TreeCourtyard";
                definition.displayName = "树与晾衣架庭院";
                definition.initialObjective = "登上楼上的阳台。";
                // Keep authored world coordinates: puzzle anchors, safety thresholds and saves use them.
                definition.boundaryMin = 40f;
                definition.boundaryMax = 69f;
                definition.initialBoundary = 69f;
                definition.spawnId = "spawn_L01B_in";
                definition.exitRequiredFlags = canal.Definition.exitRequiredFlags;
                definition.exitLockedHint = canal.Definition.exitLockedHint;
                EditorUtility.SetDirty(definition);

                Scene inside;
                if (tree != null)
                {
                    var visual = new SerializedObject(tree).FindProperty("visualController").objectReferenceValue as Component;
                    if (visual == null || visual.transform.parent != null)
                        throw new InvalidOperationException("Expected a root-level tree visual controller.");
                    inside = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    var camera = Object.Instantiate(outside.GetRootGameObjects().Single(g => g.GetComponent<Camera>() != null));
                    camera.name = "Main Camera";
                    SceneManager.MoveGameObjectToScene(camera, inside);
                    SceneManager.MoveGameObjectToScene(tree.gameObject, inside);
                    SceneManager.MoveGameObjectToScene(visual.gameObject, inside);
                }
                else
                {
                    if (!System.IO.File.Exists(ScenePath)) throw new InvalidOperationException("Tree courtyard is missing.");
                    inside = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                    tree = inside.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelRoot>(true)).Single();
                }
                var room = new SerializedObject(tree);
                room.FindProperty("definition").objectReferenceValue = definition;
                room.FindProperty("initiallyActive").boolValue = true;
                room.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(inside, ScenePath)) throw new InvalidOperationException("Could not save tree scene.");
                EditorSceneManager.CloseScene(inside, true);
                canal.Definition.displayName = "水渠";
                EditorUtility.SetDirty(canal.Definition);
                foreach (var door in canal.GetComponentsInChildren<ExitDoor>(true))
                {
                    var so = new SerializedObject(door);
                    so.FindProperty("mode").enumValueIndex = (int)ExitDoor.TransitionMode.NextLevel;
                    so.FindProperty("targetRoom").objectReferenceValue = null;
                    so.FindProperty("targetSpawnId").stringValue = "";
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.SaveScene(outside);

                var bootstrap = EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
                var session = bootstrap.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameSession>(true)).Single();
                var serialized = new SerializedObject(session);
                var catalog = serialized.FindProperty("levelCatalog");
                var entries = Enumerable.Range(0, catalog.arraySize)
                    .Select(i => (LevelDefinition)catalog.GetArrayElementAtIndex(i).objectReferenceValue)
                    .Where(d => d != null && d.levelId != "L01B").ToList();
                entries.Insert(entries.FindIndex(d => d.levelId == "L01") + 1, definition);
                catalog.arraySize = entries.Count;
                for (int i = 0; i < entries.Count; i++) catalog.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(bootstrap);
                var build = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
                build.Insert(build.FindIndex(s => s.path.EndsWith("/L01_Courtyard.scene")) + 1,
                    new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = build.ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("[L01Split] Canal → tree courtyard → sewing yard; authored coordinates and puzzle state preserved.");
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
