using System;
using System.IO;
using System.Linq;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Level;
using BorrowTomorrow.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class L01BDailyMemoryInstaller
    {
        private const string Folder="Assets/Game/Data/Dialogue/L01B_V6/";
        [Serializable] private class ScriptData { public EventData[] events; }
        [Serializable] private class EventData { public string id,flag;public LineData[] lines; }
        [Serializable] private class LineData { public string id,speaker,source,text,image,voice;public bool auto;public float delay; }
        private static ScriptData ReadScript()=>JsonUtility.FromJson<ScriptData>(File.ReadAllText("Assets/Game/Data/Narrative/l01b_v6.json"));
        private static void UpdateAssets()
        {
            foreach(var story in ReadScript().events)
            {
                var prior=AssetDatabase.LoadAssetAtPath<DialogueEntry>(Folder+story.lines[0].id+".asset");
                Texture2D picture=prior!=null?prior.illustration:null;
                for(int i=0;i<story.lines.Length;i++)
                {
                    var line=story.lines[i];string path=Folder+line.id+".asset";
                    var entry=AssetDatabase.LoadAssetAtPath<DialogueEntry>(path);
                    if(entry==null){entry=ScriptableObject.CreateInstance<DialogueEntry>();AssetDatabase.CreateAsset(entry,path);}
                    if(entry.subtitle!=line.text)entry.voiceClip=null;
                    if(!string.IsNullOrEmpty(line.voice)){entry.voiceClip=AssetDatabase.LoadAssetAtPath<AudioClip>(line.voice);if(entry.voiceClip==null)throw new InvalidOperationException("Missing voice: "+line.voice);}
                    entry.entryId=line.id;entry.name=line.id;entry.speakerLabel=line.speaker;entry.subtitle=line.text;
                    entry.source=line.source;entry.eventId=story.id;
                    entry.illustration=string.IsNullOrEmpty(line.image)?picture:AssetDatabase.LoadAssetAtPath<Texture2D>(line.image);
                    if(!string.IsNullOrEmpty(line.image)&&entry.illustration==null)throw new InvalidOperationException("Missing memory CG: "+line.image);
                    entry.blackBackground=false;
                    entry.nextId=i+1<story.lines.Length?story.lines[i+1].id:string.Empty;
                    entry.requiredFlags=i==0?new[]{"!"+story.flag}:Array.Empty<string>();
                    entry.setFlags=Array.Empty<string>();entry.completionFlags=i==0?new[]{story.flag}:Array.Empty<string>();
                    entry.holdForConfirm=!line.auto;entry.minDurationSeconds=Mathf.Clamp(1.8f+line.text.Length*.07f,2.2f,4.5f);
                    entry.delayBefore=line.delay;entry.sfxId=i==0?"sfx_tape_click":string.Empty;
                    entry.objectiveAfter=string.Empty;entry.centeredTitle=false;entry.endHoldSeconds=0;
                    EditorUtility.SetDirty(entry);
                }
            }
            AssetDatabase.SaveAssets();
        }
        private static void SyncLibrary(DialogueLibrary library)
        {
            var revised=ReadScript().events.SelectMany(e=>e.lines).Select(l=>AssetDatabase.LoadAssetAtPath<DialogueEntry>(Folder+l.id+".asset")).ToArray();
            if(revised.Any(e=>e==null)||revised.Select(e=>e.entryId).Distinct().Count()!=revised.Length)throw new InvalidOperationException("Missing/duplicate memory entries");
            var ids=revised.Select(e=>e.entryId).ToArray();
            var all=library.Entries.Where(e=>e!=null&&!ids.Contains(e.entryId)).Concat(revised).ToArray();
            var so=new SerializedObject(library);var entries=so.FindProperty("entries");entries.arraySize=all.Length;
            for(int i=0;i<all.Length;i++)entries.GetArrayElementAtIndex(i).objectReferenceValue=all[i];
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(library);
            foreach(var story in ReadScript().events)
            {
                var entry=library.Find(story.lines[0].id);
                foreach(var line in story.lines)
                {
                    if(entry==null||entry.entryId!=line.id||entry.subtitle!=line.text)throw new InvalidOperationException("Broken memory chain: "+line.id);
                    entry=library.Find(entry.nextId);
                }
                if(entry!=null)throw new InvalidOperationException("Unexpected trailing line");
            }
        }
        [MenuItem("BorrowTomorrow/桂花树/接入晾衣与量身高回忆")]
        public static void Install()
        {
            AssetDatabase.Refresh();
            foreach(var path in ReadScript().events.SelectMany(e=>e.lines).Select(l=>l.image).Where(p=>!string.IsNullOrEmpty(p)).Distinct())
            {
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null)throw new InvalidOperationException("Missing CG file: "+path);
                importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;
                importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            }
            UpdateAssets();
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L01_Courtyard.scene");
            var room=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LevelRoot>(true)).Single(r=>r.RoomId=="L01B");
            L01BWallMemoryInstaller.Configure(room);
            var peg=room.GetComponentsInChildren<NarrativeInteractable>(true).Single(n=>n.name=="ClothMemory_v7");
            var cloth=AssetDatabase.LoadAssetAtPath<DialogueEntry>(Folder+"V4_0101.asset");
            peg.EditorConfigure(cloth,DialoguePriority.Story,true);
            LevelLayouts.SetField(peg,"replayEntry",cloth);LevelLayouts.SetField(peg,"replayWholeChain",true);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            var bootstrap=EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
            var library=bootstrap.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DialogueLibrary>(true)).Single();
            SyncLibrary(library);EditorSceneManager.MarkSceneDirty(bootstrap);EditorSceneManager.SaveScene(bootstrap);
            AssetDatabase.SaveAssets();Debug.Log("[DailyMemories] Installed 9 laundry lines and 8 wall/tree lines; full read-only replays; existing art retained.");
        }
    }
}
