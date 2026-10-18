using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using BorrowTomorrow.TimeBoundary;
if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Must edit the saved scene outside play mode");
for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
 if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new System.InvalidOperationException("Unsaved scene edits must be preserved");
var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L00_Gate.scene");
var fence=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TemporalObject>(true)).Single(t=>t.ObjectId=="L00_fence_gate");
foreach(string name in new[]{"PostL","PostR"}){
 var post=fence.transform.Find("Present/"+name);
 if(post!=null)UnityEngine.Object.DestroyImmediate(post.gameObject);
}
EditorSceneManager.MarkSceneDirty(scene);
EditorSceneManager.SaveScene(scene);
return "Saved L00 gate: removed two blocking present-era posts; past fence and overhead lintel retained";
