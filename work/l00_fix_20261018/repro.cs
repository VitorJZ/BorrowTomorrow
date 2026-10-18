using System;
using System.Linq;
using System.IO;
using System.Collections;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using BorrowTomorrow.Player;
using UnityEngine;
public class L00WalkInput : IInputSource
{
 public float MoveAxis=>1f;public float ClimbAxis=>0f;public bool JumpPressedThisFrame=>false;public bool JumpHeld=>false;public bool InteractPressedThisFrame=>false;public bool ConsumeInteract()=>false;public float BoundaryNudgeDirection=>0f;public bool RespawnPressedThisFrame=>false;public bool HintPressedThisFrame=>false;public bool ObjectiveHeld=>false;public bool PausePressedThisFrame=>false;
}
IEnumerator Run()
{
 UnityEngine.SceneManagement.SceneManager.LoadScene("Bootstrap");
 yield return new WaitForSeconds(2f);
 var session=GameSession.Instance;
 session.StoryFlags.SetFlag("IntroPlayed");
 session.LoadLevel(session.FindLevel("L00"));
 yield return new WaitForSeconds(2.5f);
 ServiceRegistry.Get<IDialogueService>().SkipCurrent();
 var boundary=(BoundaryController)ServiceRegistry.Get<IBoundaryContext>();
 var motor=UnityEngine.Object.FindObjectOfType<PlayerMotor2D>();
 boundary.RestoreBoundary(26f);
 motor.TeleportTo(new Vector2(7f,.05f));
 typeof(PlayerMotor2D).GetField("input",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(motor,new L00WalkInput());
 yield return new WaitForSeconds(1.5f);
 boundary.RestoreBoundary(0f);
 yield return new WaitForSeconds(1.8f);
 var state=new {x=motor.transform.position.x,phase=session.State.Phase.ToString(),substate=session.State.SubState.ToString(),boundary=boundary.CommittedBoundary,
 colliders=UnityEngine.Object.FindObjectsOfType<Collider2D>().Where(c=>!c.isTrigger).Select(c=>new {c.name,c.enabled,active=c.gameObject.activeInHierarchy,bounds=c.bounds.ToString(),root=c.transform.root.name}).ToArray()};
 var json=Codely.Newtonsoft.Json.JsonConvert.SerializeObject(state);File.WriteAllText("work/l00_fix_20261018/repro.json",json);
}
return Run();
