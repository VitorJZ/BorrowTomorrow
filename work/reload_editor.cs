using UnityEditor.SceneManagement;
var s=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene",OpenSceneMode.Single);
return "Reloaded saved Bootstrap; no initial unsaved edits existed. dirty="+s.isDirty;
