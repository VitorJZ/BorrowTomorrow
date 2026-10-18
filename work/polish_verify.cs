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
 string dir="Docs/ArtSample_2026-10-18/Polish/";string log=dir+"evidence/runtime_verification.txt";File.WriteAllText(log,"");
 Action<bool,string> Check=(pass,label)=>{File.AppendAllText(log,(pass?"PASS ":"FAIL ")+label+"\n");if(!pass)throw new InvalidOperationException(label);};
 var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 var session=GameSession.Instance;session.LoadLevel(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Game/Data/Levels/L01_L01_Courtyard.asset"));yield return new WaitForSeconds(1.5f);
 foreach(var e in UnityEngine.Object.FindObjectsOfType<NarrativeAutoEvent>())e.enabled=false;
 foreach(var r in UnityEngine.Object.FindObjectsOfType<LevelRoot>())r.DeactivateRoom();
 var room=UnityEngine.Object.FindObjectsOfType<LevelRoot>().Single(r=>r.RoomId=="L01B");room.ActivateRoom();room.PlacePlayer("spawn_L01B_in");
 var motor=UnityEngine.Object.FindObjectOfType<PlayerMotor2D>();var boundary=room.GetComponent<BoundaryController>();
 var art=motor.GetComponent<CharacterArtAnimator>();Check(Mathf.Abs(art.visual.transform.localScale.x-1.3f)<.001f&&Mathf.Abs(art.visual.transform.localScale.y-1.3f)<.001f,"hero visual uniform scale 1.30");
 Check(motor.transform.localScale==Vector3.one,"motor root retains original scale");Check(motor.GetComponent<CapsuleCollider2D>().size==new Vector2(.6f,1.6f),"original collision capsule retained");
 var type=typeof(BorrowTomorrow.Diagnostics.PlayableWalkthrough).GetNestedType("WalkInput",BindingFlags.NonPublic);var input=Activator.CreateInstance(type,true);var original=ServiceRegistry.Get<IInputSource>();
 ServiceRegistry.Register<IInputSource>((IInputSource)input);motor.GetType().GetField("input",flags).SetValue(motor,input);
 var view=UnityEngine.Object.FindObjectOfType<NarrativePresentation>();view.Close();session.State.SetSubState(PlayingSubState.Free);
 void Set(string field,object value){type.GetField(field).SetValue(input,value);}
 IEnumerator Shot(string name){ScreenCapture.CaptureScreenshot(dir+"screenshots/"+name+".png");yield return null;}
 IEnumerator Move(float x){float end=Time.time+12;while(Mathf.Abs(motor.transform.position.x-x)>.1f&&Time.time<end){Set("move",Mathf.Sign(x-motor.transform.position.x));yield return null;}Set("move",0f);Check(Mathf.Abs(motor.transform.position.x-x)<.2f,"walk to "+x+" at "+motor.transform.position);}
 var gt=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var gw=EditorWindow.GetWindow(gt);
 foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1280,720),new Vector2(1440,1080)})
 {
  gt.GetMethod("SetCustomResolution",flags).Invoke(gw,new object[]{size,"L01B polish"});yield return new WaitForSeconds(.5f);
  foreach(var era in new[]{("past",69f),("present",40f),("mixed",53f)})
  {boundary.RestoreBoundary(era.Item2);yield return new WaitForSeconds(.5f);yield return Shot("L01B_"+era.Item1+"_"+(int)size.x+"x"+(int)size.y);}
 }
 gt.GetMethod("SetCustomResolution",flags).Invoke(gw,new object[]{new Vector2(1920,1080),"L01B polish"});boundary.RestoreBoundary(69);yield return new WaitForSeconds(.4f);
 yield return Move(48);float climbUntil=Time.time+5;Set("climb",1f);while(motor.transform.position.y<2.06f&&Time.time<climbUntil)yield return null;Set("climb",0f);Check(motor.transform.position.y>=2.06f,"climb to ledge");
 yield return Move(49.6f);yield return new WaitForSeconds(.4f);Check(motor.IsGrounded&&!motor.IsClimbingLadder,"stand on ledge before drag");yield return Shot("L01B_ladder_ledge");
 boundary.BeginMouseDrag();boundary.RequestAdjustment(53);boundary.EndMouseDrag();Check(Mathf.Abs(boundary.CommittedBoundary-53)<.05f,"normal safe drag retains mixed solution");
 yield return Move(50.2f);Set("jumpFrame",Time.frameCount+1);Set("move",1f);yield return null;yield return Move(51.8f);yield return new WaitForSeconds(.4f);Check(motor.IsGrounded,"land on branch after visual enlargement");yield return Shot("L01B_branch_start");
 yield return Move(58);yield return Shot("L01B_branch_middle");yield return Move(65.5f);yield return Shot("L01B_branch_end");yield return Move(68.1f);yield return new WaitForSeconds(.4f);Check(motor.IsGrounded,"reach exit grounded");yield return Shot("L01B_exit");
 ServiceRegistry.Register<IInputSource>(original);motor.GetType().GetField("input",flags).SetValue(motor,original);
 File.AppendAllText(log,"DONE: three era states at three sizes, normal ladder/drag/jump/branch/exit route. Room automatic dialogue disabled for route isolation; not a full new-game playthrough.\n");
}
return Review();
