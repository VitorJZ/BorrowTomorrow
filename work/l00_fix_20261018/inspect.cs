using System.Linq;
using UnityEngine;
using UnityEditor;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.TimeBoundary;
using BorrowTomorrow.Player;
var notice=UnityEngine.Object.FindObjectsOfType<PresentOnlyInvestigation>(true).FirstOrDefault();
var fence=UnityEngine.Object.FindObjectsOfType<TemporalObject>(true).First(t=>t.ObjectId=="L00_fence_gate");
var motor=UnityEngine.Object.FindObjectOfType<PlayerMotor2D>();
var state=new {
 playing=EditorApplication.isPlaying,boundary=ServiceRegistry.Get<IBoundaryContext>().CommittedBoundary,
 phase=GameSession.Instance.State.Phase.ToString(),subState=GameSession.Instance.State.SubState.ToString(),
 player=motor.transform.position.ToString(),capsule=motor.GetComponent<CapsuleCollider2D>().bounds.ToString(),
 notice=notice==null?null:new {notice.anchorX,active=notice.gameObject.activeInHierarchy,target=notice.GetComponent<NarrativeInteractable>().enabled,can=notice.GetComponent<NarrativeInteractable>().CanInteract,
 renderers=notice.GetComponentsInChildren<Renderer>(true).Select(r=>new{r.name,r.enabled,active=r.gameObject.activeInHierarchy}).ToArray(),
 colliders=notice.GetComponentsInChildren<Collider2D>(true).Select(c=>new{c.name,c.enabled,c.isTrigger}).ToArray()},
 fenceEra=fence.CurrentEra.ToString(),colliders=fence.GetComponentsInChildren<Collider2D>(true).Select(c=>new{c.name,c.enabled,active=c.gameObject.activeInHierarchy,c.isTrigger,bounds=c.bounds.ToString()}).ToArray()
};
return Codely.Newtonsoft.Json.JsonConvert.SerializeObject(state);
