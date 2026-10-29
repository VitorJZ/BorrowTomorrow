using System;
using System.Linq;
using BorrowTomorrow.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BorrowTomorrow.EditorTools
{
    public static class RopeHookInspectionInstaller
    {
        public static void Configure(RopeDryingRack mechanism)
        {
            // Keep the hit area independent of the art's scale and inside the past-era root.
            var past = mechanism.rack.transform.Find("Past");
            var marker = past.Find("HookInspection_v16");
            if (marker == null) marker = new GameObject("HookInspection_v16").transform;
            marker.SetParent(past, false);
            marker.position = mechanism.hook.position;
            marker.localScale = Vector3.one;
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer < 0) throw new InvalidOperationException("Interactable layer missing");
            marker.gameObject.layer = layer;
            var trigger = marker.GetComponent<BoxCollider2D>();
            if (trigger == null) trigger = marker.gameObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(.9f, 1f);
            var interaction = marker.GetComponent<RopeHookInteractable>();
            if (interaction == null) interaction = marker.gameObject.AddComponent<RopeHookInteractable>();
            interaction.mechanism = mechanism;
            EditorUtility.SetDirty(interaction);
            EditorUtility.SetDirty(trigger);
        }
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before installation");
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/L01_Courtyard.scene");
            var room = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LevelRoot>(true)).Single(x => x.RoomId == "L01B");
            Configure(room.GetComponentInChildren<RopeDryingRack>(true));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
