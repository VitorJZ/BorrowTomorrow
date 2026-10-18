using UnityEngine;
using UnityEditor;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.UI;
using BorrowTomorrow.TimeBoundary;
using BorrowTomorrow.Core;
using System;
using System.Linq;
using System.Collections.Generic;
var gt=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var gw=EditorWindow.GetWindow(gt);var f=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
var q=new Queue<Action>();string output="Docs/ArtSample_2026-10-18/screenshots/";
var view=UnityEngine.Object.FindObjectOfType<NarrativePresentation>();
var boundary=UnityEngine.Object.FindObjectsOfType<BoundaryController>().Single(b=>b.RoomId=="L01B");
foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1280,720),new Vector2(1440,1080)})
{
 var s=size;var suffix=(int)s.x+"x"+(int)s.y;
 q.Enqueue(()=>{view.Close();GameSession.Instance.State.SetSubState(PlayingSubState.Free);boundary.RestoreBoundary(53);gt.GetMethod("SetCustomResolution",f).Invoke(gw,new object[]{s,"Art sample"});});
 q.Enqueue(()=>ScreenCapture.CaptureScreenshot(output+"L01B_final_"+suffix+".png"));
 foreach(var id in new[]{"ACT_L02_CUFF","ACT_L04_BOARD","ACT_EPI_SMOOTH"})
 {
  var key=id;q.Enqueue(()=>{GameSession.Instance.State.SetSubState(PlayingSubState.Cutscene);var action=AssetDatabase.LoadAssetAtPath<NarrativeAction>("Assets/Game/Data/Narrative/Actions/"+key+".asset");view.PresentAction(action,key=="ACT_L04_BOARD"?1:.65f,key=="ACT_L04_BOARD"?action.confirmAfter:null,true);});
  q.Enqueue(()=>ScreenCapture.CaptureScreenshot(output+key+"_final_"+suffix+".png"));
 }
}
q.Enqueue(()=>{view.Close();GameSession.Instance.State.SetSubState(PlayingSubState.Free);gt.GetMethod("SetCustomResolution",f).Invoke(gw,new object[]{new Vector2(1920,1080),"Art sample"});});
double next=EditorApplication.timeSinceStartup;EditorApplication.CallbackFunction tick=null;tick=()=>{if(EditorApplication.timeSinceStartup<next)return;if(q.Count==0){EditorApplication.update-=tick;return;}q.Dequeue()();next=EditorApplication.timeSinceStartup+.65;};EditorApplication.update+=tick;
return "Capturing final sample at three resolutions";
