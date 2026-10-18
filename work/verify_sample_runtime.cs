using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Level;
using BorrowTomorrow.Player;
using BorrowTomorrow.TimeBoundary;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.UI;
IEnumerator Review()
{
 string dir="Docs/ArtSample_2026-10-18/";string log=dir+"evidence/runtime_verification.txt";File.WriteAllText(log,"");
 Action<bool,string> Check=(pass,label)=>{File.AppendAllText(log,(pass?"PASS ":"FAIL ")+label+"\n");if(!pass)throw new InvalidOperationException(label);};
 var f=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 var session=GameSession.Instance;
 session.LoadLevel(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Game/Data/Levels/L01_L01_Courtyard.asset"));
 yield return new WaitForSeconds(1.5f);
 foreach(var a in UnityEngine.Object.FindObjectsOfType<NarrativeAutoEvent>())a.enabled=false;
 foreach(var r in UnityEngine.Object.FindObjectsOfType<LevelRoot>())r.DeactivateRoom();
 var room=UnityEngine.Object.FindObjectsOfType<LevelRoot>().Single(r=>r.RoomId=="L01B");room.ActivateRoom();room.PlacePlayer("spawn_L01B_in");
 var motor=UnityEngine.Object.FindObjectOfType<PlayerMotor2D>();var boundary=room.GetComponent<BoundaryController>();
 var type=typeof(BorrowTomorrow.Diagnostics.PlayableWalkthrough).GetNestedType("WalkInput",BindingFlags.NonPublic);var input=Activator.CreateInstance(type,true);
 var original=ServiceRegistry.Get<IInputSource>();ServiceRegistry.Register<IInputSource>((IInputSource)input);motor.GetType().GetField("input",f).SetValue(motor,input);
 var dialogue=UnityEngine.Object.FindObjectOfType<DialoguePlayer>();var view=UnityEngine.Object.FindObjectOfType<NarrativePresentation>();
 void Set(string field,object value){type.GetField(field).SetValue(input,value);}
 IEnumerator Press(){Set("interactFrame",Time.frameCount+1);yield return null;yield return new WaitForSeconds(.3f);}
 IEnumerator Move(float x){float end=Time.time+12;while(Mathf.Abs(motor.transform.position.x-x)>.1f&&Time.time<end){Set("move",Mathf.Sign(x-motor.transform.position.x));yield return null;}Set("move",0f);Check(Mathf.Abs(motor.transform.position.x-x)<.2f,"walk to "+x+" at "+motor.transform.position);}
 IEnumerator Climb(){float end=Time.time+5;Set("climb",1f);while(motor.transform.position.y<2.06f&&Time.time<end)yield return null;Set("climb",0f);Check(motor.transform.position.y>=2.06f,"climb to permanent ledge");}
 IEnumerator Shot(string name){ScreenCapture.CaptureScreenshot(dir+"screenshots/"+name+".png");yield return null;}
 view.Close();session.State.SetSubState(PlayingSubState.Free);yield return new WaitForSeconds(.4f);
 foreach(var spec in new[]{("L01B_past",69f),("L01B_present",40f),("L01B_mixed",53f)}){boundary.RestoreBoundary(spec.Item2);yield return new WaitForSeconds(.3f);yield return Shot(spec.Item1);}
 boundary.RestoreBoundary(69f);yield return new WaitForSeconds(.3f);
 yield return Move(48);yield return Climb();yield return Move(49.6f);yield return new WaitForSeconds(.4f);Check(motor.IsGrounded&&!motor.IsClimbingLadder,"stand on unchanged ledge");
 boundary.BeginMouseDrag();boundary.RequestAdjustment(53);boundary.EndMouseDrag();Check(Mathf.Abs(boundary.CommittedBoundary-53)<.05f,"normal safe boundary drag to mixed solution");
 yield return Move(50.2f);Set("jumpFrame",Time.frameCount+1);Set("move",1f);yield return null;yield return Move(51.8f);yield return new WaitForSeconds(.4f);Check(motor.IsGrounded,"jump onto fitted branch");
 yield return Move(58);yield return Shot("L01B_on_branch");yield return Move(68.1f);yield return new WaitForSeconds(.4f);Check(motor.IsGrounded,"reach exit on original colliders");yield return Shot("L01B_exit");
 // Actual event executor (not direct PresentAction calls), preserving all completion gates.
 foreach(string entryId in new[]{"DLG_L02_UNIFORM_01","DLG_L04_BOX_01","DLG_EPI_07"})
 {
  dialogue.GetType().GetMethod("End",f).Invoke(dialogue,new object[]{false});
  session.StoryFlags.SetFlags(new[]{"IdentityKnown","CompartmentSeen"});
  var entry=AssetDatabase.LoadAssetAtPath<DialogueEntry>("Assets/Game/Data/Dialogue/StoryV13/"+entryId+".asset");
  dialogue.Play(entry,DialoguePriority.Reveal);
  yield return null;
  if(entryId=="DLG_EPI_07"){Check(dialogue.IsWaitingForAction,"smooth waits for player before motion");yield return Shot("Smooth_wait_before");yield return new WaitForSeconds(.3f);yield return Press();}
  yield return new WaitForSeconds(.45f);
  session.State.EnterPause();float elapsed=(float)dialogue.GetType().GetField("actionTimer",f).GetValue(dialogue);
  yield return new WaitForSecondsRealtime(.3f);
  Check(Mathf.Abs(elapsed-(float)dialogue.GetType().GetField("actionTimer",f).GetValue(dialogue))<.001f,entryId+" pause freezes action");session.State.ExitPause();
  yield return Shot(entryId+"_moving");
  for(int sample=0;sample<8;sample++){yield return new WaitForSeconds(.16f);yield return Shot(entryId+"_frame_"+sample.ToString("00"));}
  yield return new WaitForSeconds(2.4f);
  if(entryId=="DLG_L04_BOX_01")
  {
   Check(dialogue.IsWaitingForAction&&!session.StoryFlags.HasFlag("BoxLocated"),"board holds report prompt without BoxLocated");yield return Shot("Board_wait_after");yield return new WaitForSeconds(.5f);Check(dialogue.IsWaitingForAction,"board can stay open awaiting report");yield return Press();yield return Shot("Board_report_ward");
  }
  Check(dialogue.Current!=null&&dialogue.Current.entryId==entryId,entryId+" resumes gated line after action");
  if(entryId=="DLG_EPI_07")Check(!session.StoryFlags.HasFlag("EpilogueSeen"),"smooth alone does not complete epilogue");
  yield return Shot(entryId+"_after");
 }
 dialogue.GetType().GetMethod("End",f).Invoke(dialogue,new object[]{false});
 ServiceRegistry.Register<IInputSource>(original);motor.GetType().GetField("input",f).SetValue(motor,original);
 File.AppendAllText(log,"DONE - movement + event action/pause/wait samples; auto room dialogue disabled during route isolation.\n");
}
return Review();
