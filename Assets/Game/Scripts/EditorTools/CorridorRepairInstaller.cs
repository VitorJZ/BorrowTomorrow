using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using BorrowTomorrow.Level;
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
   RemoveLegacyPitOverlay();
   CorridorLooseFloorInstaller.Install();
   UnityEngine.Debug.Log("[CorridorRestore] Restored pre-repair layout; accepted identity story preserved. Withdrawn pit/lever mechanism is not installed.");
  }
  // Keep the historical backup untouched, but never restore its obsolete dark pit rectangle.
  internal static bool ClearPitOverlay(LevelRoot room){
   if(room.RoomId!="L03")return false;
   var pit=room.transform.Find("Content/PitFill");
   if(pit==null)return false;
   UnityEngine.Object.DestroyImmediate(pit.gameObject);
   return true;
  }
  [MenuItem("BorrowTomorrow/关卡/清除回廊旧深坑色块（仅画面）")]
  public static void RemoveLegacyPitOverlay(){
   if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first");
   var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L03_Corridor.scene");
   bool changed=false;
   foreach(var root in scene.GetRootGameObjects())
    foreach(var room in root.GetComponentsInChildren<LevelRoot>(true))changed|=ClearPitOverlay(room);
   if(changed){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
  }
 }
}
