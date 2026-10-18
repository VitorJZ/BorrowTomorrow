using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using BorrowTomorrow.UI;
using BorrowTomorrow.Narrative;
var q=new Queue<Action>();var view=UnityEngine.Object.FindObjectOfType<NarrativePresentation>();
foreach(string id in new[]{"ACT_L02_CUFF","ACT_L04_BOARD","ACT_EPI_SMOOTH"})foreach(float progress in new[]{0f,.5f,1f})
{var name=id;var t=progress;q.Enqueue(()=>view.PresentAction(AssetDatabase.LoadAssetAtPath<NarrativeAction>("Assets/Game/Data/Narrative/Actions/"+name+".asset"),t,t==0?"开始动作":t==1?"确认动作结果":null,true));q.Enqueue(()=>ScreenCapture.CaptureScreenshot("Docs/ArtSample_2026-10-18/screenshots/"+name+"_"+t.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+".png"));}
double next=EditorApplication.timeSinceStartup;EditorApplication.CallbackFunction tick=null;
tick=()=>{if(EditorApplication.timeSinceStartup<next)return;if(q.Count==0){EditorApplication.update-=tick;return;}q.Dequeue()();next=EditorApplication.timeSinceStartup+.65;};EditorApplication.update+=tick;
return "Capturing 9 layered action states";
