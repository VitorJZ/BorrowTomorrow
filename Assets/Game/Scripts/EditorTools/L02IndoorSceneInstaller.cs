using System;
using System.Linq;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Level;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.TimeBoundary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>Preserves the authored interior while moving it into its own scene.</summary>
    public static class L02IndoorSceneInstaller
    {
        public const string ScenePath = "Assets/Game/Scenes/L02_SewingRoom.scene";
        private const string DefinitionPath = "Assets/Game/Data/Levels/L02Indoor_L02_SewingRoom.asset";

        [MenuItem("BorrowTomorrow/拆分 L02 缝纫间室内场景")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            // Do not discard unsaved artist edits when opening the two scenes.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) EditorSceneManager.SaveScene(SceneManager.GetSceneAt(i));
            try
            {
                var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(DefinitionPath);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<LevelDefinition>();
                    AssetDatabase.CreateAsset(definition, DefinitionPath);
                }
                definition.levelId = "L02Indoor";
                definition.sceneName = "L02_SewingRoom";
                definition.displayName = "缝纫间";
                definition.initialObjective = "查看缝纫桌上的校服。";
                definition.boundaryMin = 0f;
                definition.boundaryMax = 18f;
                definition.spawnId = "spawn_sewing_indoor";
                definition.exitRequiredFlags = new[] { "UniformRead" };
                definition.exitLockedHint = "先看看缝纫桌上的旧校服吧。";
                EditorUtility.SetDirty(definition);

                var outsideScene = EditorSceneManager.OpenScene("Assets/Game/Scenes/L02_Sewing.scene");
                var rooms = outsideScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelRoot>(true)).ToArray();
                var outside = rooms.Single(r => r.RoomId == "L02");
                var interior = rooms.FirstOrDefault(r => r.RoomId == "L02Indoor");
                if (interior != null)
                {
                    var insideScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    var camera = Object.Instantiate(outsideScene.GetRootGameObjects().Single(g => g.GetComponent<Camera>() != null));
                    camera.name = "Main Camera";
                    SceneManager.MoveGameObjectToScene(camera, insideScene);
                    SceneManager.MoveGameObjectToScene(interior.gameObject, insideScene);
                    // All former interior content was authored in world X=40..58.
                    foreach (Transform child in interior.transform) child.position += Vector3.left * 40f;
                    var tidy = interior.GetComponentInChildren<ClothTidyInteractable>(true);
                    if (tidy != null) tidy.chairPosition += Vector3.left * 40f;
                    ConfigureInterior(interior, definition);
                    EditorSceneManager.SaveScene(insideScene, ScenePath);
                    EditorSceneManager.CloseScene(insideScene, true);
                }
                else
                {
                    if (!System.IO.File.Exists(ScenePath)) throw new InvalidOperationException("L02 interior is missing.");
                    var insideScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                    interior = insideScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelRoot>(true)).Single();
                    ConfigureInterior(interior, definition);
                    EditorSceneManager.SaveScene(insideScene);
                    EditorSceneManager.CloseScene(insideScene, true);
                }
                outside.Definition.exitRequiredFlags = new string[0];
                outside.Definition.exitLockedHint = "";
                EditorUtility.SetDirty(outside.Definition);
                foreach (var door in outside.GetComponentsInChildren<ExitDoor>(true))
                {
                    var so = new SerializedObject(door);
                    so.FindProperty("mode").enumValueIndex = (int)ExitDoor.TransitionMode.NextLevel;
                    so.FindProperty("targetRoom").objectReferenceValue = null;
                    so.FindProperty("targetSpawnId").stringValue = "";
                    so.FindProperty("ignoreLevelConditions").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.SaveScene(outsideScene);

                var bootstrap = EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
                var session = bootstrap.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameSession>(true)).Single();
                var catalog = new SerializedObject(session);
                var levels = catalog.FindProperty("levelCatalog");
                var entries = Enumerable.Range(0, levels.arraySize)
                    .Select(i => (LevelDefinition)levels.GetArrayElementAtIndex(i).objectReferenceValue)
                    .Where(d => d != null && d.levelId != "L02Indoor").ToList();
                entries.Insert(entries.FindIndex(d => d.levelId == "L02") + 1, definition);
                levels.arraySize = entries.Count;
                for (int i = 0; i < entries.Count; i++) levels.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
                catalog.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(bootstrap);
                var buildScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
                buildScenes.Insert(buildScenes.FindIndex(s => s.path.EndsWith("/L02_Sewing.scene")) + 1,
                    new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("[L02Indoor] Split complete: exterior → sewing room → corridor; past-only props use the standard boundary system.");
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void ConfigureInterior(LevelRoot room, LevelDefinition definition)
        {
            var so = new SerializedObject(room);
            so.FindProperty("definition").objectReferenceValue = definition;
            so.FindProperty("initiallyActive").boolValue = true;
            so.FindProperty("overrideBoundaryRange").boolValue = false;
            so.FindProperty("boundaryMin").floatValue = 0f;
            so.FindProperty("boundaryMax").floatValue = 18f;
            so.FindProperty("initialBoundary").floatValue = definition.initialBoundary;
            var bounds = so.FindProperty("cameraBounds").rectValue;
            if (bounds.xMin >= 39f) bounds.x -= 40f;
            so.FindProperty("cameraBounds").rectValue = bounds;
            so.ApplyModifiedPropertiesWithoutUndo();
            var content = room.transform.Find("NarrativeContent");
            PastOnly(content, "uniform", "Narrative_uniform");
            PastOnly(content, "ledger", "Narrative_ledger");
            PastOnly(content, "machine", "SewingMachine");
            PastOnly(content, "fabrics", "Fabrics");
            PastOnly(content, "clothes", "TidyClothes", "Clothes");
        }

        private static void PastOnly(Transform content, string id, params string[] objectNames)
        {
            if (content.Find("Temporal_" + id) != null) return;
            var targets = objectNames.Select(n => content.Find(n)).ToArray();
            if (targets.Any(t => t == null)) throw new InvalidOperationException("Missing sewing-room prop: " + id);
            var go = new GameObject("Temporal_" + id);
            go.transform.SetParent(content, false);
            go.transform.position = targets[0].position;
            var past = new GameObject("Past"); past.transform.SetParent(go.transform, false);
            var present = new GameObject("Present"); present.transform.SetParent(go.transform, false);
            foreach (var target in targets) target.SetParent(past.transform, true);
            var path = "Assets/Game/Data/TemporalObjects/L02Indoor_" + id + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<TemporalObjectDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<TemporalObjectDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.objectId = "L02Indoor_" + id;
            def.anchorX = targets[0].position.x;
            def.safetyPolicy = SafetyPolicy.Ignore;
            def.anchorHysteresis = .05f;
            def.switchSfxId = "";
            def.pastState = new TemporalObjectState { stateId = "visible" };
            def.presentState = new TemporalObjectState { stateId = "absent" };
            EditorUtility.SetDirty(def);
            var temporal = go.AddComponent<TemporalObject>();
            temporal.EditorConfigure(def);
            var so = new SerializedObject(temporal);
            so.FindProperty("pastRoot").objectReferenceValue = past;
            so.FindProperty("presentRoot").objectReferenceValue = present;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
