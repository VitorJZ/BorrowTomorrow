using System;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
namespace BorrowTomorrow.Player
{
    [DisallowMultipleComponent]
    public sealed class CharacterArtAnimator : MonoBehaviour, ICharacterArt
    {
        [Serializable] public sealed class Clip { public string name; public Sprite[] frames; public float fps=12; public bool loop; }
        public SpriteRenderer visual;
        public Clip[] clips;
        private PlayerMotor2D motor;
        private Rigidbody2D body;
        private Clip current;
        private float time;
        private string gesture;
        private bool facingLeft;
        private float landedTime;
        private bool wasGrounded;
        private void Awake(){motor=GetComponent<PlayerMotor2D>();body=GetComponent<Rigidbody2D>();}
        private void OnEnable()=>ServiceRegistry.Register<ICharacterArt>(this);
        private void OnDisable()=>ServiceRegistry.Unregister<ICharacterArt>(this);
        public void PlayGesture(string name){gesture=name;Set(name,true);}
        private Clip Find(string name){if(clips!=null)foreach(var c in clips)if(c.name==name)return c;return null;}
        private void Set(string name,bool force=false){var c=Find(name);if(c==null)return;if(current!=c||force){current=c;time=0;}}
        private void LateUpdate()
        {
            if(visual==null||motor==null||body==null)return;
            var s=GameSession.Instance;if(s==null||s.State.IsPaused)return;
            float dt=Time.deltaTime;
            if(Mathf.Abs(body.velocity.x)>.08f)facingLeft=body.velocity.x<0;
            if(motor.IsGrounded&&!wasGrounded)landedTime=.20f;
            wasGrounded=motor.IsGrounded;landedTime=Mathf.Max(0,landedTime-dt);
            if(gesture!=null)
            {
                Set(gesture);time+=dt;
                if(current==null||time>=current.frames.Length/Mathf.Max(1,current.fps))gesture=null;
            }
            if(gesture==null)
            {
                if(s.State.SubState==PlayingSubState.AdjustingBoundary){Set("Interact");time=.42f;}
                else if(motor.IsClimbingLadder)
                {
                    Set("Climb");if(Mathf.Abs(body.velocity.y)>.05f)time+=dt;
                }
                else if(!motor.IsGrounded)
                {
                    Set("JumpLand");
                    time=(body.velocity.y>1f?5:body.velocity.y<-.8f?8:6)/Mathf.Max(1,current.fps);
                }
                else if(landedTime>0){Set("JumpLand");time=(10+(1-landedTime/.20f))/Mathf.Max(1,current.fps);}
                else {Set(Mathf.Abs(body.velocity.x)>.1f?"Walk":"Idle");time+=dt;}
            }
            if(current==null||current.frames==null||current.frames.Length==0)return;
            int index=(int)(time*current.fps);
            index=current.loop?index%current.frames.Length:Mathf.Min(index,current.frames.Length-1);
            visual.sprite=current.frames[Mathf.Max(0,index)];visual.flipX=facingLeft;visual.color=Color.white;
        }
    }
}
