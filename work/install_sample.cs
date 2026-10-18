using BorrowTomorrow.EditorTools;
using UnityEditor.SceneManagement;
EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene",OpenSceneMode.Single);
ArtSampleInstaller.Install();
return "Installed";
