using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;
using System.IO;
using System.Text;
public class SampleTestResults : ICallbacks
{
 public void RunStarted(ITestAdaptor t){}
 public void TestStarted(ITestAdaptor t){}
 public void TestFinished(ITestResultAdaptor t){}
 public void RunFinished(ITestResultAdaptor t){var text=new StringBuilder();void Walk(ITestResultAdaptor n){text.AppendLine(n.ResultState+" "+n.FullName+" "+n.Message);if(n.HasChildren)foreach(var c in n.Children)Walk(c);}Walk(t);File.WriteAllText("Docs/ArtSample_2026-10-18/evidence/story_tests.txt","passed="+t.PassCount+" failed="+t.FailCount+"\n"+text);}
}
var api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new SampleTestResults());api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{"BorrowTomorrow.Tests.DialogueLifecycleTests","BorrowTomorrow.Tests.StoryContentTests"}}));
return "Started narrative regression tests";
