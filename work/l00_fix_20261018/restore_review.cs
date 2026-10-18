using System;
using System.IO;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using BorrowTomorrow.Save;
using BorrowTomorrow.Narrative;
using Codely.Newtonsoft.Json.Linq;
IEnumerator Run()
{
 SceneManager.LoadScene("Bootstrap");
 yield return new WaitForSeconds(2f);
 var snapshot=JObject.Parse(File.ReadAllText("work/l00_fix_20261018/review_state.json"));
 var session=GameSession.Instance;
 session.StoryFlags.RestoreFrom(snapshot["flags"].Values<string>());
 session.LoadLevel(session.FindLevel("L00"));
 float deadline=Time.realtimeSinceStartup+15f;
 while(session.State.Phase!=GamePhase.Playing&&Time.realtimeSinceStartup<deadline)yield return null;
 yield return null;
 ((BoundaryController)ServiceRegistry.Get<IBoundaryContext>()).RestoreBoundary(snapshot["boundary"].Value<float>());
 ServiceRegistry.Get<IPlayerContext>().TeleportToPosition(new Vector2(snapshot["x"].Value<float>(),snapshot["y"].Value<float>()));
 session.State.SetSubState(PlayingSubState.Free);
 var gt=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(gt).Focus();
 yield return new WaitForSeconds(.25f);
 File.WriteAllText("work/l00_fix_20261018/restored.txt", "L00 restored at x="+ServiceRegistry.Get<IPlayerContext>().Position.x+" boundary="+ServiceRegistry.Get<IBoundaryContext>().CommittedBoundary+"; no blocking posts="+(UnityEngine.Object.FindObjectsOfType<Transform>().Count(t=>t.name=="PostL"||t.name=="PostR")==0));
 ScreenCapture.CaptureScreenshot("work/l00_fix_20261018/L00_fixed.png");
}
return Run();
