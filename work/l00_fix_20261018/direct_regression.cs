using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using BorrowTomorrow.Tests;
using UnityEditor;
using UnityEngine;
IEnumerator Run()
{
 var suite=new BootToGameSmokeTests();
 var stack=new Stack<IEnumerator>();
 stack.Push(suite.L00_NoticeFollowsEraAndOpenGateAllowsWalkingBothWays());
 Exception error=null;
 while(stack.Count>0)
 {
  var step=stack.Peek(); bool more=false;
  try { more=step.MoveNext(); } catch(Exception ex){error=ex;break;}
  if(!more){stack.Pop();continue;}
  if(step.Current is IEnumerator nested){stack.Push(nested);continue;}
  yield return step.Current;
 }
 if(error!=null){
 var m=UnityEngine.Object.FindObjectOfType<BorrowTomorrow.Player.PlayerMotor2D>();
 var b=BorrowTomorrow.Core.ServiceRegistry.Get<BorrowTomorrow.Core.Interfaces.IBoundaryContext>();
 var state=new {phase=BorrowTomorrow.Core.GameSession.Instance.State.Phase.ToString(),substate=BorrowTomorrow.Core.GameSession.Instance.State.SubState.ToString(),boundary=b.CommittedBoundary,
 motor=m.transform.position.ToString(),input=((BorrowTomorrow.Core.Interfaces.IInputSource)typeof(BorrowTomorrow.Player.PlayerMotor2D).GetField("input",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(m)).MoveAxis,
 colliders=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(UnityEngine.Object.FindObjectsOfType<Collider2D>(),c=>new{c.name,c.enabled,c.isTrigger,bounds=c.bounds.ToString()}))};
 File.WriteAllText("work/l00_fix_20261018/failure_state.json",Codely.Newtonsoft.Json.JsonConvert.SerializeObject(state));
 }
 yield return suite.Cleanup();
 File.WriteAllText("work/l00_fix_20261018/direct_result.txt",error==null?"PASS L00 notice art, hidden interaction, repeated era changes, closed fence, physical traversal left and right.":error.ToString());
 if(error!=null)throw error;
}
return Run();
