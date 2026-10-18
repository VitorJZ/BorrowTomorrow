using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Level;
using BorrowTomorrow.Player;
using BorrowTomorrow.Save;
using BorrowTomorrow.TimeBoundary;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BorrowTomorrow.Tests
{
    public sealed class DryingRackIntegrationTests
    {
        private sealed class Controls : IInputSource
        {
            public float move, climb, nudge;
            public int interactFrame=-1, consumed=-1;
            public float MoveAxis => move;
            public float ClimbAxis => climb;
            public float BoundaryNudgeDirection => nudge;
            public bool JumpPressedThisFrame => false;
            public bool JumpHeld => false;
            public bool InteractPressedThisFrame => Time.frameCount==interactFrame;
            public bool ConsumeInteract()
            {if(!InteractPressedThisFrame||consumed==Time.frameCount)return false;consumed=Time.frameCount;return true;}
            public bool RespawnPressedThisFrame => false;
            public bool ObjectiveHeld => false;
            public bool PausePressedThisFrame => false;
        }
        private Controls input;
        private PlayerMotor2D motor;
        private string output;
        private GameSession Session => GameSession.Instance;
        private BoundaryController Boundary => ServiceRegistry.Get<IBoundaryContext>() as BoundaryController;
        private void SkipDialogue()
        { if (ServiceRegistry.TryGet<IDialogueService>(out var d) && d.IsPlaying) d.SkipCurrent(); }
        private IEnumerator Until(Func<bool> ready, float timeout, string label)
        {
            float end=Time.realtimeSinceStartup+timeout;
            while(!ready()&&Time.realtimeSinceStartup<end){SkipDialogue();yield return null;}
            Assert.IsTrue(ready(),label+" at "+(motor!=null?motor.transform.position.ToString():"no player"));
        }
        private IEnumerator Walk(float x)
        {
            float end=Time.realtimeSinceStartup+10;
            while(Mathf.Abs(motor.transform.position.x-x)>.1f && Time.realtimeSinceStartup<end)
            {SkipDialogue();input.move=Mathf.Sign(x-motor.transform.position.x);yield return null;}
            input.move=0;
            Assert.That(motor.transform.position.x,Is.EqualTo(x).Within(.15f),"Route must be traversable by the actual motor");
            yield return new WaitForSeconds(.18f);
        }
        private void Adjust(float value)
        {
            Boundary.BeginMouseDrag();Assert.IsTrue(Boundary.IsAdjusting,"Must be able to adjust from stable surface");
            Boundary.RequestAdjustment(value);Boundary.EndMouseDrag();
        }
        private void Shot(string name)
        {
            var camera=Camera.main;Assert.IsNotNull(camera);
            var rt=RenderTexture.GetTemporary(1920,1080,24);var prior=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var im=new Texture2D(1920,1080,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,1920,1080),0,0);im.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),im.EncodeToPNG());
            camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(im);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(input!=null)ServiceRegistry.Unregister<IInputSource>(input);
            if(Session!=null)Object.Destroy(Session.transform.root.gameObject);
            yield return null;Time.timeScale=1;AudioListener.pause=false;SceneManager.LoadScene("MainMenu");yield return null;
        }
        [UnityTest]
        public IEnumerator RealRoute_Raise_Pause_Safety_SaveContinue_AndKeyboardDoubleAudio()
        {
            output=Path.GetFullPath("work/drying-rack/verification");Directory.CreateDirectory(output);
            SceneManager.LoadScene("Bootstrap");
            yield return Until(()=>Session!=null&&Session.State.Phase==GamePhase.MainMenu,20,"Bootstrap");
            var save=Object.FindObjectOfType<SaveService>();Assert.IsNotNull(save);
            string saves=Path.Combine(output,"isolated-saves",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(saves);
            typeof(SaveService).GetField("saveDirectory",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(save,saves);
            input=new Controls();ServiceRegistry.Register<IInputSource>(input);
            Session.LoadLevel(Session.FindLevel("L01"));
            yield return Until(()=>Session.CurrentLevel!=null&&Session.CurrentLevel.levelId=="L01"&&Session.State.IsPlayerControlAllowed,25,"L01 load");
            var room=(LevelRoot)Session.ActivateRoomForRestore("L01B");room.PlacePlayer("spawn_L01B_in");
            motor=Object.FindObjectOfType<PlayerMotor2D>();
            yield return Until(()=>motor.IsGrounded,3,"Tree-room entry grounded");
            yield return Walk(48);
            input.climb=1;
            yield return Until(()=>motor.transform.position.y>=2.06f,7,"Climb authored past ladder");input.climb=0;
            yield return Walk(49.65f);
            yield return Until(()=>motor.IsGrounded&&!motor.IsClimbingLadder&&Session.State.IsPlayerControlAllowed,5,"Stand on permanent wall platform");
            var rack=Object.FindObjectOfType<FoldingDryingRack>();Assert.IsNotNull(rack);
            Assert.IsFalse(rack.IsRaised);Assert.IsFalse(Session.StoryFlags.HasFlag(FoldingDryingRack.RaisedFlag));
            var deck=(BoxCollider2D)typeof(FoldingDryingRack).GetField("deck",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(rack);
            Assert.IsFalse(deck.enabled,"Unraised shelf must not have invisible collision");
            Shot("01-before-raise");
            Adjust(Boundary.RangeMin);
            Assert.IsTrue(rack.Interact());Assert.IsFalse(rack.IsOperating);Assert.IsFalse(rack.IsRaised);
            Shot("02-broken-present");
            Adjust(52.5f);
            var tree=room.GetComponentsInChildren<TemporalObject>().Single(t=>t.ObjectId=="L01B_tree_branch");
            Assert.AreEqual(Era.Present,tree.CurrentEra);
            input.interactFrame=Time.frameCount+1;
            yield return Until(()=>rack.IsOperating,2,"Actual E input discovers and starts rack interaction");
            yield return new WaitForSeconds(.6f);
            var leaf=(Transform)typeof(FoldingDryingRack).GetField("leaf",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(rack);
            float angle=leaf.eulerAngles.z;
            Session.State.EnterPause();yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(angle,leaf.eulerAngles.z,.001f,"Pause freezes mechanism");Session.State.ExitPause();
            yield return Until(()=>rack.IsRaised&&!rack.IsOperating,5,"Rack raises and locks");
            Assert.IsTrue(deck.enabled);Assert.IsTrue(Session.StoryFlags.HasFlag(FoldingDryingRack.RaisedFlag));
            Shot("03-raised-mixed");
            // Q/R's duplicate loops are scheduled at the same DSP instant and have matching gain.
            var emitter=Object.FindObjectsOfType<MonoBehaviour>().Single(b=>b.GetType().FullName=="BorrowTomorrow.Audio.BoundaryAudioEmitter");
            var type=emitter.GetType();input.nudge=.5f;yield return new WaitForSeconds(.25f);
            var loops=(AudioSource[])type.GetField("loops",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(emitter);
            var boost=(AudioSource[])type.GetField("keyboardBoost",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(emitter);
            Assert.Greater(loops.Sum(s=>s.volume),.05f);
            for(int i=0;i<4;i++){Assert.AreEqual(loops[i].volume,boost[i].volume,.0001f);Assert.AreSame(loops[i].clip,boost[i].clip);}
            input.nudge=0;yield return new WaitForSeconds(.3f);
            Assert.Less(boost.Sum(s=>s.volume),.001f,"Release silences doubled bus");
            Adjust(52.5f);
            yield return Walk(52);
            Assert.IsTrue(motor.IsGrounded,"Real player stands on raised shelf");
            Adjust(47);
            Assert.Greater(Boundary.CommittedBoundary,50.74f,"Stops before the hysteresis switching threshold");
            Assert.AreEqual(Era.Past,room.GetComponentsInChildren<TemporalObject>().Single(t=>t.ObjectId=="L01B_drying_rack").CurrentEra);
            Assert.IsTrue(deck.enabled && deck.gameObject.activeInHierarchy,"Supporting past shelf remains physically present");
            yield return Walk(55.5f);Assert.IsTrue(motor.IsGrounded,"Enter natural branch from rack");
            Shot("03b-standing-on-branch");
            Adjust(58);
            Assert.Less(Boundary.CommittedBoundary,54.46f,"Stops before the hysteresis switching threshold");
            Assert.AreEqual(Era.Present,tree.CurrentEra,"Supporting mature tree remains physically present");
            yield return Walk(58.5f);Assert.Greater(motor.transform.position.y,3,"Natural branch reaches balcony");
            Shot("04-natural-branch-balcony");
            yield return Walk(67.7f);Assert.IsTrue(motor.IsGrounded);
            Shot("05-exit-route");
            Session.SaveProgressNow();Session.ReturnToMainMenu();
            yield return Until(()=>Session.State.Phase==GamePhase.MainMenu,10,"Return to menu");
            Session.ContinueGame();
            yield return Until(()=>Session.State.IsPlayerControlAllowed&&Session.ActiveRoom!=null&&Session.ActiveRoom.RoomId=="L01B",20,"Continue into correct room");
            motor=Object.FindObjectOfType<PlayerMotor2D>();rack=Object.FindObjectOfType<FoldingDryingRack>();
            yield return Until(()=>rack.IsRaised,3,"Completed rack survives save and continue");
            Assert.IsTrue(Session.StoryFlags.HasFlag(FoldingDryingRack.RaisedFlag));
            File.WriteAllText(Path.Combine(output,"result.txt"),"PASS: real ladder, broken feedback, raise, pause, Q/R doubled bus, shelf/tree safety, full route to exit, save/continue.");
        }
    }
}
