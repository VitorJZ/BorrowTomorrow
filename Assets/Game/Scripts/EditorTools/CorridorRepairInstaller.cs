using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace BorrowTomorrow.EditorTools {
 // The maintenance-pit proposal was withdrawn. This entry restores the previous
 // layout and reapplies only the accepted photo/identity story.
 public static class CorridorRepairInstaller {
  [InitializeOnLoadMethod]static void Init(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingPlayMode)SessionState.SetBool("BorrowTomorrow.L03Preview",false);};}
  [MenuItem("BorrowTomorrow/Preview/L03 Corridor")]public static void Preview(){EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");SessionState.SetBool("BorrowTomorrow.L03Preview",true);EditorApplication.EnterPlaymode();}
  [MenuItem("BorrowTomorrow/关卡/恢复回廊原玩法（保留新剧情）")]
  public static void Install(){
   if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first");
   EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
   File.Copy("Assets/Game/Scripts/EditorTools/Backups/L03_PreRepair.scene.bytes","Assets/Game/Scenes/L03_Corridor.scene",true);
   AssetDatabase.ImportAsset("Assets/Game/Scenes/L03_Corridor.scene",ImportAssetOptions.ForceUpdate);
   CorridorIdentityStoryInstaller.Install();
   EditorSceneManager.OpenScene("Assets/Game/Scenes/L03_Corridor.scene");
   UnityEngine.Debug.Log("[CorridorRestore] Restored pre-repair layout; accepted identity story preserved. Withdrawn pit/lever mechanism is not installed.");
  }
 }
}
