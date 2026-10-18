using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using System.Text;
using BorrowTomorrow.Level;
using BorrowTomorrow.TimeBoundary;
var sb=new StringBuilder();
var setup=EditorSceneManager.GetSceneManagerSetup();
foreach(var name in "__SCENES__".Split(','))
{
 var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/"+name+".scene",OpenSceneMode.Single);
 foreach(var room in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LevelRoot>(true)))
 {
  var so=new SerializedObject(room);var cb=so.FindProperty("cameraBounds").rectValue;
  sb.AppendLine("== "+name+" room "+room.RoomId+" camBounds="+cb+" initB="+so.FindProperty("initialBoundary").floatValue+" ovr="+so.FindProperty("overrideBoundaryRange").boolValue);
  foreach(var t in room.GetComponentsInChildren<Transform>(true))
  {
   string path=t.name;var p=t.parent;while(p!=null&&p!=room.transform){path=p.name+"/"+path;p=p.parent;}
   var sr=t.GetComponent<SpriteRenderer>();var col=t.GetComponent<Collider2D>();
   if(sr==null&&col==null)continue;
   string s="  "+path+" pos="+t.position.ToString("F2")+" rot="+t.eulerAngles.z.ToString("F0")+" scl="+t.lossyScale.ToString("F2");
   if(sr!=null)s+=" SR["+(sr.sprite!=null?sr.sprite.name:"null")+" en="+sr.enabled+" "+sr.sortingLayerName+":"+sr.sortingOrder+" "+sr.drawMode+" size="+sr.size.ToString("F2")+" b="+sr.bounds.size.ToString("F1")+"]";
   if(col!=null)s+=" COL["+col.GetType().Name+" trig="+col.isTrigger+" b="+col.bounds.center.ToString("F2")+"/"+col.bounds.size.ToString("F2")+" act="+t.gameObject.activeInHierarchy+"]";
   sb.AppendLine(s);
  }
 }
}
EditorSceneManager.RestoreSceneManagerSetup(setup);
System.IO.File.WriteAllText("__OUT__",sb.ToString());
return sb.Length;
