using UnityEditor;
using UnityEditor.SceneManagement;
foreach(var path in new[]{"Assets/Game/Data/Art/SampleL01BBranch.asset","Assets/Game/Data/Art/SampleL01BBranch.mat","Assets/Game/Art/SampleV1/SampleTransparent.shader"})AssetDatabase.DeleteAsset(path);
EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene",OpenSceneMode.Single);
return "Removed rejected mesh assets; saved Bootstrap open";
