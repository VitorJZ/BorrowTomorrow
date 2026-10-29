using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.UI;
namespace BorrowTomorrow.EditorTools {
public static class CorridorIdentityStoryInstaller {
 const string Folder="Assets/Game/Data/Dialogue/L03_Identity_v5/";
 [Serializable]class Line{public string entryId,speakerLabel,subtitle,source,illustration,voice;}
 [Serializable]class Document{public Line[] lines;}
 static void SetArray(UnityEngine.Object obj,string name,UnityEngine.Object[] values){var so=new SerializedObject(obj);var a=so.FindProperty(name);a.arraySize=values.Length;for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
 static NarrativeAction Action(string id,string before,Texture2D front,Texture2D after,bool target){string path=Folder+id+".asset";var a=AssetDatabase.LoadAssetAtPath<NarrativeAction>(path);if(a==null){a=ScriptableObject.CreateInstance<NarrativeAction>();AssetDatabase.CreateAsset(a,path);}a.actionId=id;a.beforeEntryId=before;a.beforeImage=front;a.afterImage=after;a.seconds=.9f;a.turnPhoto=true;a.turnCaption=target?"翻看照片背面":"翻回照片正面";a.requireTargetClick=target;a.targetRegion=new Rect(.405f,.11f,.18f,.64f);a.confirmBefore=target?"点击红衣的人，翻看照片背面":"";a.sfxId="sfx_cloth_paper";EditorUtility.SetDirty(a);return a;}
 [MenuItem("BorrowTomorrow/剧情/接入照片身份揭示v5")]
 public static void Install(){
 if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
 Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
 string art="Assets/Game/Art/Story/L03_Identity_v5/";
 foreach(string path in Directory.GetFiles(art,"*.png")){var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
 var doc=JsonUtility.FromJson<Document>(File.ReadAllText("Assets/Game/Data/Narrative/l03_identity_v5.json"));var entries=new DialogueEntry[doc.lines.Length];
 for(int i=0;i<entries.Length;i++){
 var l=doc.lines[i];string path=Folder+l.entryId+".asset";var e=AssetDatabase.LoadAssetAtPath<DialogueEntry>(path);if(e==null){e=ScriptableObject.CreateInstance<DialogueEntry>();AssetDatabase.CreateAsset(e,path);}
 e.entryId=l.entryId;e.speakerLabel=l.speakerLabel;e.subtitle=l.subtitle;e.source=l.source;e.illustration=AssetDatabase.LoadAssetAtPath<Texture2D>(l.illustration);if(e.illustration==null)throw new Exception("Missing CG "+l.illustration);
 e.nextId=i+1<entries.Length?doc.lines[i+1].entryId:"";e.eventId="EVT_L03_PHOTO_REVEAL_V5";e.holdForConfirm=true;e.delayBefore=0;e.endHoldSeconds=0;e.voiceClip=string.IsNullOrEmpty(l.voice)?null:AssetDatabase.LoadAssetAtPath<AudioClip>(l.voice);if(!string.IsNullOrEmpty(l.voice)&&e.voiceClip==null)throw new Exception("Missing voice "+l.voice);e.minDurationSeconds=.1f;e.requiredFlags=i==0?new[]{"!IdentityKnown"}:Array.Empty<string>();e.completionFlags=i==0?new[]{"PhotoRevealed","IdentityKnown"}:Array.Empty<string>();e.setFlags=i==entries.Length-1?new[]{"PhotoRevealed","IdentityKnown"}:Array.Empty<string>();e.objectiveAfter=i==0?"找到铁盒的位置，告诉晓晓。":"";EditorUtility.SetDirty(e);entries[i]=e;
 }
 var back=entries.Single(e=>e.entryId=="V5_L03_002");var recognition=entries.Single(e=>e.entryId=="V5_L03_003");
 var actions=new[]{Action("ACT_V10_L03_ClickGrandmother",back.entryId,entries[0].illustration,back.illustration,true),Action("ACT_V5_L03_FlipFront",recognition.entryId,back.illustration,recognition.illustration,false)};
 var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L03_Corridor.scene");var photo=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NarrativeInteractable>(true)).Single(n=>n.InteractId=="photo");photo.EditorConfigure(entries[0],DialoguePriority.Story,false);LevelLayouts.SetObject(photo,"replayEntry",null);LevelLayouts.SetField(photo,"replayWholeChain",false);PhotoReconstructionInstaller.Configure(photo);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 var boot=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");var lib=boot.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DialogueLibrary>(true)).Single();
 SetArray(lib,"entries",lib.Entries.Where(e=>e!=null&&!entries.Any(n=>n.entryId==e.entryId)).Concat(entries).Cast<UnityEngine.Object>().ToArray());
 var serialized=new SerializedObject(lib);var old=serialized.FindProperty("actions");var all=Enumerable.Range(0,old.arraySize).Select(i=>old.GetArrayElementAtIndex(i).objectReferenceValue as NarrativeAction).Where(a=>a!=null&&a.actionId!="ACT_V5_L03_ClickChild"&&!actions.Any(n=>n.actionId==a.actionId)).Concat(actions).Cast<UnityEngine.Object>().ToArray();SetArray(lib,"actions",all);
 string matPath=art+"PhotoChildFocus.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(mat==null){var shader=AssetDatabase.LoadAssetAtPath<Shader>(art+"PhotoChildFocus.shader");if(shader==null)throw new Exception("Focus shader missing");mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}
 var view=boot.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NarrativePresentation>(true)).Single();view.photoFocusMaterial=mat;EditorUtility.SetDirty(view);
 EditorSceneManager.MarkSceneDirty(boot);EditorSceneManager.SaveScene(boot);AssetDatabase.SaveAssets();Debug.Log("[L03IdentityV10] Photo reconstruction before story; red-cardigan observation and grandmother focus; 18 manual lines with original voices and explicit identity.");
 }
}
}
