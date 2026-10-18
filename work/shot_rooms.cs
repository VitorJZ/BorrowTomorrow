using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Level;
using BorrowTomorrow.TimeBoundary;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.UI;
IEnumerator Run()
{
 string dir="__DIR__/";Directory.CreateDirectory(dir);
 var f=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 var gt=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var gw=EditorWindow.GetWindow(gt);
 gt.GetMethod("SetCustomResolution",f).Invoke(gw,new object[]{new Vector2(__W__,__H__),"shots"});
 yield return new WaitForSeconds(.5f);
 var session=GameSession.Instance;
 // level, room, [(label,B)]
 var plan=new (string,string,(string,float)[])[]{
  ("L00_L00_Gate","L00",new[]{("past",26f),("present",0f),("mixed",5f)}),
  ("L01_L01_Courtyard","L01A",new[]{("past",20f),("present",0f),("mixed",16f)}),
  ("L01_L01_Courtyard","L01B",new[]{("past",69f),("present",40f),("mixed",53f)}),
  ("L02_L02_Sewing","L02",new[]{("past",26f),("present",0f),("step1",20f),("step2",3f)}),
  ("L02_L02_Sewing","L02Indoor",new[]{("past",100f),("present",0f)}),
  ("L03_L03_Corridor","L03",new[]{("past",30f),("present",0f),("step1",11f),("step2",28f)}),
  ("L04_L04_Attic","L04",new[]{("past",28f),("present",0f)}),
  ("Epilogue_Epilogue","Epilogue",new[]{("only",0f)}),
 };
 string only="__ONLY__";
 string lastLevel=null;
 foreach(var p in plan)
 {
  if(only!="" && !only.Split(',').Contains(p.Item2)) continue;
  if(lastLevel!=p.Item1){session.LoadLevel(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Game/Data/Levels/"+p.Item1+".asset"));yield return new WaitForSeconds(2.2f);lastLevel=p.Item1;}
  foreach(var a in UnityEngine.Object.FindObjectsOfType<NarrativeAutoEvent>())a.enabled=false;
  foreach(var r in UnityEngine.Object.FindObjectsOfType<LevelRoot>())r.DeactivateRoom();
  var room=UnityEngine.Object.FindObjectsOfType<LevelRoot>().Single(r=>r.RoomId==p.Item2);
  room.ActivateRoom();
  var sp=room.GetComponentsInChildren<BorrowTomorrow.Level.SpawnPoint>(true).First();room.PlacePlayer(sp.SpawnId);
  var d=UnityEngine.Object.FindObjectOfType<DialoguePlayer>();if(d!=null)d.GetType().GetMethod("End",f).Invoke(d,new object[]{false});
  var view=UnityEngine.Object.FindObjectOfType<NarrativePresentation>();if(view!=null)view.Close();
  session.State.SetSubState(PlayingSubState.Free);
  var b=room.GetComponent<BoundaryController>();
  yield return new WaitForSeconds(.6f);
  foreach(var s in p.Item3)
  {
   b.RestoreBoundary(s.Item2);yield return new WaitForSeconds(.5f);
   ScreenCapture.CaptureScreenshot(dir+p.Item2+"_"+s.Item1+".png");yield return new WaitForSeconds(.3f);
  }
 }
 return_done=true;
}
bool return_done=false;
return Run();
