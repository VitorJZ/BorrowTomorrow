using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;
using System.IO;
using System.Text;
public class L00FixTestResults : ICallbacks
{
 public void RunStarted(ITestAdaptor t){}
 public void TestStarted(ITestAdaptor t){}
 public void TestFinished(ITestResultAdaptor t){}
 public void RunFinished(ITestResultAdaptor t){var text=new StringBuilder();void Walk(ITestResultAdaptor n){text.AppendLine(n.ResultState+" "+n.FullName+" "+n.Message);if(n.HasChildren)foreach(var c in n.Children)Walk(c);}Walk(t);File.WriteAllText("work/l00_fix_20261018/editmode_results.txt","passed="+t.PassCount+" failed="+t.FailCount+"\n"+text);}
}
var api=ScriptableObject.CreateInstance<TestRunnerApi>();
api.RegisterCallbacks(new L00FixTestResults());
api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{"BorrowTomorrow.Tests.DialogueLifecycleTests","BorrowTomorrow.Tests.StoryContentTests"}}));
return "Started dialogue and content EditMode regression checks";
