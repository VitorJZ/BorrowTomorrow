using System;
using System.Linq;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Level;
using BorrowTomorrow.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class L01BWallMemoryInstaller
    {
        public static void Configure(LevelRoot room)
        {
            var entry=AssetDatabase.LoadAssetAtPath<DialogueEntry>("Assets/Game/Data/Dialogue/L01B_V6/V4_0105.asset");
            if(entry==null)throw new InvalidOperationException("Missing L01B tree memory");
            var content=room.transform.Find("NarrativeContent");
            if(content==null)throw new InvalidOperationException("Missing narrative content");
            var wall=content.Find("height_marks");
            if(wall==null){var go=new GameObject("height_marks");go.transform.SetParent(content,false);wall=go.transform;}
            wall.gameObject.SetActive(true);
            wall.position=new Vector3(44.5f,1f,0);
            int layer=LayerMask.NameToLayer("Interactable");if(layer>=0)wall.gameObject.layer=layer;
            var col=wall.GetComponent<BoxCollider2D>();if(col==null)col=wall.gameObject.AddComponent<BoxCollider2D>();
            col.enabled=true;col.isTrigger=true;col.size=new Vector2(1.15f,1.65f);
            var inv=wall.GetComponent<NarrativeInteractable>();if(inv==null)inv=wall.gameObject.AddComponent<NarrativeInteractable>();
            inv.EditorConfigure(entry,DialoguePriority.Story,true);
            LevelLayouts.SetField(inv,"interactId","L01B_height_marks");
            LevelLayouts.SetField(inv,"prompt","查看墙面");
            LevelLayouts.SetField(inv,"replayEntry",entry);
            LevelLayouts.SetField(inv,"replayWholeChain",true);
            if(wall.GetComponent<SceneMemoryClick>()==null)wall.gameObject.AddComponent<SceneMemoryClick>();
            // Shared entry/completion flag: the balcony is a fallback for players who missed the wall.
            // Manual replays are read-only and do not reapply flags or objectives.
            var balcony=room.GetComponentsInChildren<NarrativeAutoEvent>(true)
                .FirstOrDefault(e=>e.name=="TreeMemoryOnBalcony_v7");
            if(balcony!=null){balcony.entry=entry;EditorUtility.SetDirty(balcony);}
            EditorUtility.SetDirty(inv);
        }
        [MenuItem("BorrowTomorrow/桂花树/恢复墙面回忆调查")]
        public static void Install()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L01_Courtyard.scene");
            var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LevelRoot>(true)).Single(r=>r.RoomId=="L01B");
            Configure(room);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
            Debug.Log("[WallMemory] Restored E/click investigation; balcony shares HeightMarksSeen to avoid automatic repetition.");
        }
    }
}
