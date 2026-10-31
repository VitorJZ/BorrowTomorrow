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
 const string EchoFolder="Assets/Game/Data/Dialogue/L03_Identity_v11/";
 [Serializable]class Line{public string entryId,speakerLabel,subtitle,source,illustration,voice;}
 [Serializable]class Document{public Line[] lines;}
 static void SetArray(UnityEngine.Object obj,string name,UnityEngine.Object[] values){var so=new SerializedObject(obj);var a=so.FindProperty(name);a.arraySize=values.Length;for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
 static NarrativeAction Action(string id,string before,Texture2D front,Texture2D after,bool target){string path=Folder+id+".asset";var a=AssetDatabase.LoadAssetAtPath<NarrativeAction>(path);if(a==null){a=ScriptableObject.CreateInstance<NarrativeAction>();AssetDatabase.CreateAsset(a,path);}a.actionId=id;a.beforeEntryId=before;a.beforeImage=front;a.afterImage=after;a.seconds=.9f;a.turnPhoto=true;a.turnCaption=target?"翻看照片背面":"翻回照片正面";a.requireTargetClick=target;a.targetRegion=new Rect(.405f,.11f,.18f,.64f);a.confirmBefore=target?"点击红衣的人，翻看照片背面":"";a.sfxId="sfx_cloth_paper";EditorUtility.SetDirty(a);return a;}
 static void Link(DialogueEntry entry,string nextId){entry.nextId=nextId;EditorUtility.SetDirty(entry);}
 static DialogueEntry[] BuildMemoryEchoEntries(){
 Directory.CreateDirectory(EchoFolder);AssetDatabase.Refresh();
 foreach(string art in new[]{"Assets/Game/Art/Story/L03_Identity_v11/ILL_L03_Ledger_Note.png","Assets/Game/Art/Story/L03_Identity_v11/ILL_L03_CUFF_HOSPITAL.png"}){var importer=AssetImporter.GetAtPath(art) as TextureImporter;if(importer!=null){importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}}
 var specs=new (string id,string speaker,string text,string source,string art,string voice,float seconds,float delay)[]{
  ("V11_L03_ECHO_HEIGHT_01","磁带·年轻外婆","脚跟放下，踮着不算。","Tape","Assets/Game/Art/Story/HeightMemory_v1/ILL_HEIGHT_TIPTOE.png","Assets/Game/Audio/Voice/Revision_v8/vo_v4_0105.mp3",.5f,.2f),
  ("V11_L03_ECHO_HEIGHT_02","磁带·童年晓晓","我没踮！","Tape","Assets/Game/Art/Story/HeightMemory_v1/ILL_HEIGHT_TIPTOE.png","Assets/Game/Audio/Voice/Revision_v8/vo_v4_0106.mp3",.5f,.2f),
  ("V11_L03_ECHO_CUFF_01","磁带·童年林晓","外婆，你怎么知道我明年长多高？","Tape","Assets/Game/Art/Illustrations/ILL_UNIFORM_CUFF.jpg","Assets/Game/Audio/Voice/vo_l02_uniform_02.mp3",.5f,.2f),
  ("V11_L03_ECHO_CUFF_02","磁带·年轻外婆","不知道呀。先替你留一点。","Tape","Assets/Game/Art/Illustrations/ILL_UNIFORM_CUFF.jpg","Assets/Game/Audio/Voice/vo_l02_uniform_03.mp3",.5f,.2f),
  ("V11_L03_ECHO_LEDGER_01","缝补账本","九月十五：晓晓校服，不记钱。","Note","Assets/Game/Art/Story/L03_Identity_v11/ILL_L03_Ledger_Note.png","",3f,.3f),
  ("V11_L03_CUFF_HOSPITAL_01","林晓（现实）","外婆，你又在摸袖口了。是我，晓晓。我都三十七了，不用再给我留袖口啦。","Reality","Assets/Game/Art/Story/L03_Identity_v11/ILL_L03_CUFF_HOSPITAL.png","Assets/Game/Audio/Voice/vo_l03_reveal_03.mp3",.1f,0f)
 };
 var result=new DialogueEntry[specs.Length];
 for(int i=0;i<specs.Length;i++){
  var s=specs[i];string path=EchoFolder+s.id+".asset";var e=AssetDatabase.LoadAssetAtPath<DialogueEntry>(path);if(e==null){e=ScriptableObject.CreateInstance<DialogueEntry>();AssetDatabase.CreateAsset(e,path);}
  e.entryId=s.id;e.speakerLabel=s.speaker;e.subtitle=s.text;e.source=s.source;
  e.illustration=string.IsNullOrEmpty(s.art)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(s.art);if(!string.IsNullOrEmpty(s.art)&&e.illustration==null)throw new Exception("Missing CG "+s.art);
  e.voiceClip=string.IsNullOrEmpty(s.voice)?null:AssetDatabase.LoadAssetAtPath<AudioClip>(s.voice);if(!string.IsNullOrEmpty(s.voice)&&e.voiceClip==null)throw new Exception("Missing voice "+s.voice);
  bool echo=i<specs.Length-1;e.autoAdvance=echo;e.autoAdvanceDelay=s.delay;e.holdForConfirm=!echo;e.minDurationSeconds=s.seconds;e.memoryEcho=echo;
  e.delayBefore=0;e.endHoldSeconds=0;e.eventId="EVT_L03_PHOTO_REVEAL_V11";
  e.sfxId=i==0?"sfx_tape_click":i==4?"sfx_cloth_paper":"";
  e.requiredFlags=Array.Empty<string>();e.setFlags=Array.Empty<string>();e.completionFlags=Array.Empty<string>();e.objectiveAfter="";
  e.nextId="";EditorUtility.SetDirty(e);result[i]=e;
 }
 return result;
 }
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
 var reality=entries.Single(e=>e.entryId=="V5_L03_005");
 reality.sceneLeadHold=.35f;reality.sceneBlendSeconds=1.45f;reality.sceneImageHold=1.2f;reality.scenePhotoRect=new Rect(.19f,.265f,.625f,.605f);reality.scenePreviousPhotoCrop=new Rect(.06f,.05f,.885f,.85f);reality.scenePhotoRotation=1.4f;EditorUtility.SetDirty(reality);
 var byId=entries.ToDictionary(e=>e.entryId);var echo=BuildMemoryEchoEntries();
 Link(byId["V5_L03_003"],echo[0].entryId);
 for(int i=0;i<4;i++)Link(echo[i],echo[i+1].entryId);
 Link(echo[4],byId["V5_L03_004"].entryId);
 Link(byId["V5_L03_009"],echo[5].entryId);
 Link(echo[5],byId["V5_L03_012"].entryId);
 byId["V5_L03_012"].illustration=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Illustrations/ILL_L00_NOTICE.png");
 byId["V5_L03_013"].illustration=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Props/DryingRack_v2/ILL_ClothMemory.png");
 if(byId["V5_L03_012"].illustration==null||byId["V5_L03_013"].illustration==null)throw new Exception("Missing L03 callback CG");
 EditorUtility.SetDirty(byId["V5_L03_012"]);EditorUtility.SetDirty(byId["V5_L03_013"]);
 var back=entries.Single(e=>e.entryId=="V5_L03_002");var recognition=entries.Single(e=>e.entryId=="V5_L03_003");
 var actions=new[]{Action("ACT_V10_L03_ClickGrandmother",back.entryId,entries[0].illustration,back.illustration,true),Action("ACT_V5_L03_FlipFront",recognition.entryId,back.illustration,recognition.illustration,false)};
 var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L03_Corridor.scene");var photo=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NarrativeInteractable>(true)).Single(n=>n.InteractId=="photo");photo.EditorConfigure(entries[0],DialoguePriority.Story,false);LevelLayouts.SetObject(photo,"replayEntry",null);LevelLayouts.SetField(photo,"replayWholeChain",false);PhotoReconstructionInstaller.Configure(photo);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 var boot=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");var lib=boot.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DialogueLibrary>(true)).Single();
 var allEntries=entries.Concat(echo).ToArray();
 SetArray(lib,"entries",lib.Entries.Where(e=>e!=null&&!allEntries.Any(n=>n.entryId==e.entryId)).Concat(allEntries).Cast<UnityEngine.Object>().ToArray());
 var serialized=new SerializedObject(lib);var old=serialized.FindProperty("actions");var all=Enumerable.Range(0,old.arraySize).Select(i=>old.GetArrayElementAtIndex(i).objectReferenceValue as NarrativeAction).Where(a=>a!=null&&a.actionId!="ACT_V5_L03_ClickChild"&&!actions.Any(n=>n.actionId==a.actionId)).Concat(actions).Cast<UnityEngine.Object>().ToArray();SetArray(lib,"actions",all);
 string matPath=art+"PhotoChildFocus.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(mat==null){var shader=AssetDatabase.LoadAssetAtPath<Shader>(art+"PhotoChildFocus.shader");if(shader==null)throw new Exception("Focus shader missing");mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}
 var view=boot.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NarrativePresentation>(true)).Single();view.photoFocusMaterial=mat;EditorUtility.SetDirty(view);
 EditorSceneManager.MarkSceneDirty(boot);EditorSceneManager.SaveScene(boot);AssetDatabase.SaveAssets();Debug.Log("[L03IdentityV11] Photo reconstruction before story; red-cardigan observation, height/cuff/ledger memory echo, hospital cuff callback and explicit identity; 22-line chain.");
 }
}
}
