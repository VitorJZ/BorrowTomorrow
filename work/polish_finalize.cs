using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
if(EditorApplication.isPlaying)return "Wait for play mode to stop";
for(int n=0;n<SceneManager.sceneCount;n++)if(SceneManager.GetSceneAt(n).isDirty)throw new System.InvalidOperationException("Review left a dirty scene; inspect before replacing it.");
EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
return "Review stopped; saved Bootstrap restored";
