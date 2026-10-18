using System;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
namespace BorrowTomorrow.Player
{
    [DisallowMultipleComponent]
    public sealed class CharacterArtAnimator : MonoBehaviour, ICharacterArt
    {
        [Serializable] public sealed class Clip
        {
            public string name;
            public Sprite[] frames;
            public float fps=12;
            public bool loop;
            public AnimationClip animation;
            public Vector2 visualOffset;
            public float visualScale=1f;
            // Positive only for locomotion that follows actual world movement.
            public float distancePerCycle;
            // Authored holds for the accepted jump, including the two recovery frames.
            public float[] frameDurations;
        }
        public SpriteRenderer visual;
        public Clip[] clips;
        private PlayerMotor2D motor;
        private Rigidbody2D body;
        private Clip current;
        private float time;
        private string gesture;
        private bool facingLeft;
        private float landingElapsed=-1f;
        private bool jumpActive;
        private float launchSpeed=5.8f;
        private float unsupportedTime;
        private bool visiblyAirborne;
        private Vector2 previousPosition;
        private bool hasPreviousPosition;
        private Vector3 baseVisualPosition;
        private Vector3 baseVisualScale=Vector3.one;
        private bool hasVisualTransform;
        private void Awake(){motor=GetComponent<PlayerMotor2D>();body=GetComponent<Rigidbody2D>();CacheVisualTransform();}
        private void CacheVisualTransform()
        {
            if(visual==null||hasVisualTransform)return;
            baseVisualPosition=visual.transform.localPosition;
            baseVisualScale=visual.transform.localScale;
            hasVisualTransform=true;
        }
        private void OnEnable()
        {
            if(motor==null)motor=GetComponent<PlayerMotor2D>();
            if(body==null)body=GetComponent<Rigidbody2D>();
            ResetMotion();
            if(motor!=null){motor.JumpStarted+=OnJumpStarted;motor.MotionReset+=ResetMotion;}
            ServiceRegistry.Register<ICharacterArt>(this);
        }
        private void OnDisable()
        {
            if(motor!=null){motor.JumpStarted-=OnJumpStarted;motor.MotionReset-=ResetMotion;}
            ServiceRegistry.Unregister<ICharacterArt>(this);
        }
        private void ResetMotion()
        {
            hasPreviousPosition=false;unsupportedTime=0;visiblyAirborne=false;
            jumpActive=false;landingElapsed=-1;gesture=null;current=null;time=0;
        }
        private void OnJumpStarted()
        {
            jumpActive=true;landingElapsed=-1;gesture=null;
            launchSpeed=Mathf.Max(1f,body.velocity.y);
        }
        public void PlayGesture(string name)
        {
            if(motor!=null&&motor.IsClimbingLadder)return;
            gesture=name;Set(name,true);
        }
        private Clip Find(string name){if(clips!=null)foreach(var c in clips)if(c.name==name)return c;return null;}
        private void Set(string name,bool force=false){var c=Find(name);if(c==null)return;if(current!=c||force){current=c;time=0;}}
        private static float Duration(Clip clip)
        {
            if(clip==null||clip.frames==null||clip.frames.Length==0)return 0;
            if(clip.frameDurations!=null&&clip.frameDurations.Length==clip.frames.Length)
            {
                float total=0;foreach(float hold in clip.frameDurations)total+=Mathf.Max(.001f,hold);return total;
            }
            return clip.animation!=null?clip.animation.length:clip.frames.Length/Mathf.Max(1,clip.fps);
        }
        private void Phase(string name,float normalized)
        {
            Set(name);time=Duration(current)*Mathf.Clamp01(normalized);
        }
        private void LateUpdate()
        {
            if(visual==null||motor==null||body==null)return;
            CacheVisualTransform();
            Vector2 position=body.position;
            float verticalDistance=hasPreviousPosition?position.y-previousPosition.y:0f;
            previousPosition=position;hasPreviousPosition=true;
            var s=GameSession.Instance;if(s==null||s.State.IsPaused)return;
            float dt=Time.deltaTime;
            if(Mathf.Abs(body.velocity.x)>.08f)facingLeft=body.velocity.x<0;
            // Ignore momentary support-probe loss on seams/slopes. Only an actual
            // airborne sequence may trigger landing frames and restart locomotion.
            unsupportedTime=motor.IsGrounded||motor.IsClimbingLadder?0:unsupportedTime+dt;
            bool airborne=!motor.IsClimbingLadder&&!motor.IsGrounded&&(body.velocity.y>1f||unsupportedTime>.1f);
            bool supported=motor.IsGrounded&&!motor.IsClimbingLadder&&body.velocity.y<=.2f;
            if(supported&&visiblyAirborne){jumpActive=false;landingElapsed=0;}
            else if(landingElapsed>=0)landingElapsed+=dt;
            airborne=!motor.IsClimbingLadder&&(jumpActive||airborne);
            visiblyAirborne=airborne;
            if(landingElapsed>=0&&(landingElapsed>=Duration(Find("JumpRecover"))||
                (Mathf.Abs(body.velocity.x)>.1f&&landingElapsed>.10f)))landingElapsed=-1;
            if(motor.IsClimbingLadder)
            {
                // A movement gesture must not hide the ladder pose. Advance by
                // real displacement so a blocked/stopped climber also stops.
                gesture=null;jumpActive=false;landingElapsed=-1;visiblyAirborne=false;Set("Climb");
                if(current!=null&&Mathf.Abs(verticalDistance)>.00001f)
                {
                    float duration=current.animation!=null?current.animation.length:
                        (current.frames==null?0:current.frames.Length/Mathf.Max(1,current.fps));
                    time+=current.distancePerCycle>0f?
                        verticalDistance*duration/current.distancePerCycle:
                        Mathf.Sign(verticalDistance)*dt;
                }
            }
            else if(gesture!=null)
            {
                Set(gesture);time+=dt;
                if(current==null||time>=Duration(current))gesture=null;
            }
            if(gesture==null&&!motor.IsClimbingLadder)
            {
                if(s.State.SubState==PlayingSubState.AdjustingBoundary){Set("Interact");time=.42f;}
                else if(motor.IsPreparingJump)
                {
                    landingElapsed=-1;Phase("JumpPrepare",motor.JumpPreparationProgress);
                }
                else if(airborne)
                {
                    if(body.velocity.y>.85f)Phase("JumpRise",1f-body.velocity.y/launchSpeed);
                    else if(body.velocity.y>=-.85f&&jumpActive)Phase("JumpApex",0);
                    else Phase("JumpFall",(-body.velocity.y-.85f)/Mathf.Max(1,launchSpeed-.85f));
                }
                else if(landingElapsed>=0){Set("JumpRecover");time=landingElapsed;}
                else {Set(Mathf.Abs(body.velocity.x)>.1f?"Walk":"Idle");time+=dt;}
            }
            if(current==null||current.frames==null||current.frames.Length==0)return;
            // Runtime checks showed SampleAnimation leaving the idle sprite
            // unchanged for these object-reference curves. Render the exact same
            // approved frame table directly, with one sprite writer. Retain the
            // native clip's authored duration (including any terminal hold).
            float clipDuration=Duration(current);
            float sampleTime=current.loop&&clipDuration>0?Mathf.Repeat(time,clipDuration):Mathf.Clamp(time,0,clipDuration);
            int index=0;
            if(current.frameDurations!=null&&current.frameDurations.Length==current.frames.Length)
            {
                float end=0;
                for(index=0;index<current.frames.Length-1;index++)
                {
                    end+=Mathf.Max(.001f,current.frameDurations[index]);if(sampleTime<end)break;
                }
            }
            else index=Mathf.Clamp(Mathf.FloorToInt(sampleTime*current.fps),0,current.frames.Length-1);
            visual.sprite=current.frames[index];
            visual.transform.localPosition=baseVisualPosition+(Vector3)current.visualOffset;
            visual.transform.localScale=baseVisualScale*(current.visualScale>0f?current.visualScale:1f);
            // The ladder is a rear view; horizontal facing belongs to side-view
            // movement and must be restored only when leaving the ladder.
            visual.flipX=!motor.IsClimbingLadder&&facingLeft;visual.color=Color.white;
        }
    }
}
