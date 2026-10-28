using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Level;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.TimeBoundary;
using BorrowTomorrow.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowTomorrow.EditorTools
{
    public static class NarrativeInstaller
    {
        [Serializable] private class Script { public Event[] events; }
        [Serializable] private class Event { public string id; public string[] flags; public string objective; public Line[] lines; public ActionSpec action; }
        [Serializable] private class Line { public string id; public string speaker; public string source; public string text; public string mode; public string voice; public string next; }
        [Serializable] private class ActionSpec
        {
            public string id, before, confirmBefore, confirmAfter, sfx, gesture, image, beforeImage;
            public float seconds, zoom;
            public float[] cover; public Color coverColor; public Vector2 slide, focus;
        }
        private const string Source = "Assets/Game/Data/Narrative/story_v13.json";
        private const string Target = "Assets/Game/Data/Dialogue/StoryV13";
        private const string ActionTarget = "Assets/Game/Data/Narrative/Actions";
        private static Script Load() => JsonUtility.FromJson<Script>("{\"events\":" + File.ReadAllText(Source) + "}");
        public static Dictionary<string,string> ImportDialogue()
        {
            Directory.CreateDirectory(Target);
            AssetDatabase.Refresh();
            var map = new Dictionary<string,string>();
            foreach (Event e in Load().events)
            {
                for(int i=0;i<e.lines.Length;i++)
                {
                    Line l=e.lines[i];string path=Target+"/"+l.id+".asset";
                    DialogueEntry entry=AssetDatabase.LoadAssetAtPath<DialogueEntry>(path);
                    if(entry==null) { entry=ScriptableObject.CreateInstance<DialogueEntry>();AssetDatabase.CreateAsset(entry,path); }
                    entry.entryId=l.id;entry.speakerLabel=l.speaker;entry.subtitle=l.text;entry.source=l.source;entry.eventId=e.id;
                    entry.nextId=l.next;entry.holdForConfirm=l.mode!="自动";
                    entry.minDurationSeconds=l.source=="Title"?2.5f:l.mode=="自动"?3f:.2f;
                    entry.requiredFlags=i==0?Required(e.id):Array.Empty<string>();
                    // Event completion is committed once from the first line's config; per-line setFlags stay empty (spec 1.4 §4.1).
                    entry.completionFlags=i==0?e.flags:Array.Empty<string>();
                    entry.setFlags=Array.Empty<string>();
                    entry.objectiveAfter=i==0?Objective(e.objective):null;
                    entry.voiceClip=string.IsNullOrEmpty(l.voice)?null:AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Voice/"+l.voice+".mp3");
                    entry.illustration=Illustration(l.id);entry.blackBackground=l.id=="DLG_L00_INTRO_01"||l.id=="DLG_L00_INTRO_02";
                    entry.centeredTitle=l.source=="Title";
                    entry.delayBefore=l.id=="DLG_L02_UNIFORM_04"?1.2f:0f;
                    entry.endHoldSeconds=l.id=="DLG_EPI_08"?3f:0f;
                    entry.sfxId=l.source=="Note"?"sfx_cloth_paper":l.source=="Tape"?"sfx_tape_click":null;
                    if(l.id=="DLG_EPI_02")entry.sfxId="sfx_tin_box";
                    EditorUtility.SetDirty(entry);map[l.id]=path;
                }
            }
            AssetDatabase.SaveAssets();
            return map;
        }
        public static NarrativeAction[] ImportActions()
        {
            Directory.CreateDirectory(ActionTarget);
            AssetDatabase.Refresh();
            var result=new List<NarrativeAction>();
            foreach(Event e in Load().events)
            {
                ActionSpec s=e.action;
                if(s==null||string.IsNullOrEmpty(s.id))continue;
                if(!e.lines.Any(l=>l.id==s.before))throw new InvalidOperationException(e.id+" action targets a line outside its event: "+s.before);
                string path=ActionTarget+"/"+s.id+".asset";
                var a=AssetDatabase.LoadAssetAtPath<NarrativeAction>(path);
                if(a==null){a=ScriptableObject.CreateInstance<NarrativeAction>();AssetDatabase.CreateAsset(a,path);}
                a.actionId=s.id;a.beforeEntryId=s.before;a.confirmBefore=s.confirmBefore;a.confirmAfter=s.confirmAfter;
                a.seconds=s.seconds>0?s.seconds:1.2f;a.sfxId=s.sfx;a.gesture=s.gesture;
                // Keep the staged quilt close-ups aligned with its red-sleeved hand layers.
                bool stagedSmooth=s.id=="ACT_EPI_SMOOTH" && a.stageBackground!=null;
                if(!stagedSmooth){a.afterImage=IllustrationByKey(s.image);a.beforeImage=IllustrationByKey(s.beforeImage);}
                if(a.afterImage==null)throw new InvalidOperationException("Missing action illustration: "+s.image);
                a.coverRect=s.cover!=null&&s.cover.Length==4?new Rect(s.cover[0],s.cover[1],s.cover[2],s.cover[3]):default;a.coverColor=s.coverColor;a.coverSlide=s.slide;
                a.zoom=s.zoom>0?s.zoom:1f;a.focus=s.focus==Vector2.zero?new Vector2(.5f,.5f):s.focus;
                EditorUtility.SetDirty(a);result.Add(a);
            }
            AssetDatabase.SaveAssets();
            return result.ToArray();
        }
        private static string[] Required(string id)
        {
            switch(id)
            {
                case "EVT_L00_INTRO":return new[]{"!IntroPlayed"};
                case "EVT_L00_DOG_ECHO":return new[]{"IntroPlayed","!DogEchoPlayed"};
                case "EVT_L01B_HEIGHT_MARKS":return new[]{"!HeightMarksSeen"};
                case "EVT_L02_UNIFORM":return new[]{"!UniformRead"};
                case "EVT_L03_PHOTO_REVEAL":return new[]{"UniformRead","!IdentityKnown"};
                case "EVT_L04_LOOK_NICHE":return new[]{"IdentityKnown","!CompartmentSeen"};
                case "EVT_L04_TREE_GROWN":return new[]{"IdentityKnown","CompartmentSeen","!TreeGrownL04"};
                case "EVT_L04_OPEN_NICHE":return new[]{"IdentityKnown","CompartmentSeen","!BoxLocated"};
                case "EVT_EPILOGUE_MAIN":return new[]{"BoxLocated","!EpilogueSeen"};
                default:return Array.Empty<string>();
            }
        }
        private static string Objective(string key)
        {
            switch(key)
            {
                case "OBJ_L00_FIND_BOX":return Objectives.ReachAtticFindBox;
                case "OBJ_L01B_CLIMB":return "借木阶梯与长高的树枝，登上缝纫间外廊。";
                case "OBJ_L02_TO_CORRIDOR":return "穿过楼上回廊，前往阁楼门前。";
                case "OBJ_L03_TELL_XIAOXIAO":return Objectives.TellXiaoxaoBoxLocation;
                case "OBJ_L04_APPROACH":return "靠近窗边，确认铁盒藏处。";
                case "OBJ_COMPLETE":return "已告知晓晓铁盒的位置。";
                case "隐藏目标":return "HIDE";
                default:return null;
            }
        }
        private static Texture2D Illustration(string id)
        {
            string key=null;
            if(id.StartsWith("DLG_L02_UNIFORM"))key="ILL_UNIFORM_CUFF";
            if(id=="DLG_L03_REVEAL_01")key="ILL_PHOTO_BACK";
            if(id=="DLG_L03_REVEAL_02")key="ILL_PHOTO_FRONT";
            if(id=="DLG_L03_REVEAL_03")key="ILL_HANDS_OLD";
            if(id.StartsWith("DLG_L03_REVEAL") && int.Parse(id.Substring(id.Length-2))>=4)key="ILL_HOSPITAL_HOLD";
            // The niche itself is shown by the board action; the spoken location happens at the hospital bed.
            if(id.StartsWith("DLG_L04_BOX"))key="ILL_HOSPITAL_HOLD";
            if(id=="DLG_EPI_02")key="ILL_EPILOGUE_BOX_OPEN";
            if(id=="DLG_EPI_03"||id=="DLG_EPI_04")key="ILL_EPILOGUE_LETTER";
            if(id.StartsWith("DLG_EPI_") && int.Parse(id.Substring(id.Length-2))>=5)
            {
                var smooth=AssetDatabase.LoadAssetAtPath<NarrativeAction>(ActionTarget+"/ACT_EPI_SMOOTH.asset");
                if(smooth!=null && smooth.stageBackground!=null)
                    return int.Parse(id.Substring(id.Length-2))<7?smooth.beforeImage:smooth.afterImage;
                key="ILL_EPILOGUE_QUILT_LAP";
            }
            return IllustrationByKey(key);
        }
        private static Texture2D IllustrationByKey(string key)
        {
            if(string.IsNullOrEmpty(key))return null;
            return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Illustrations/"+key+(key=="ILL_EPILOGUE_QUILT_LAP"?".png":".jpg"));
        }
        [MenuItem("BorrowTomorrow/接入完整剧情 v1.4（保留现有关卡）")]
        public static void Install()
        {
            var map=ImportDialogue();
            foreach(string name in new[]{"Bootstrap","L00_Gate","L01_Courtyard","L01_TreeCourtyard","L02_Sewing","L03_Corridor","L04_Attic","Epilogue"})
            {
                if (name == "L01_TreeCourtyard" && !System.IO.File.Exists(L01TreeSceneInstaller.ScenePath)) continue;
                Scene scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/"+name+".scene");
                InstallScene(scene,map);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
            if(File.Exists("Assets/Game/Data/Art/enabled.json"))ArtInstaller.Install();
            L02IndoorSceneInstaller.Install();
            L01TreeSceneInstaller.Install();
            Debug.Log("[NarrativeInstaller] Full v1.4 narrative installed without rebuilding existing puzzle geometry.");
        }
        public static void InstallAndBuild()
        {
            Install();
            string dest=Environment.GetEnvironmentVariable("BORROWTOMORROW_NARRATIVE_OUTPUT");
            if(string.IsNullOrEmpty(dest))dest=Path.GetFullPath("Builds/Narrative/BorrowTomorrow.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=EditorBuildSettings.scenes.Where(x=>x.enabled).Select(x=>x.path).ToArray(),
                target=BuildTarget.StandaloneWindows64,locationPathName=dest,options=BuildOptions.Development });
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Narrative build failed: "+result.summary.result);
            Debug.Log("[NarrativeInstaller] BUILD SUCCEEDED: "+dest);
        }
        private static IEnumerable<T> All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true));
        private static DialogueEntry Entry(Dictionary<string,string> map,string id)=>AssetDatabase.LoadAssetAtPath<DialogueEntry>(map[id]);
        private static void Objects(UnityEngine.Object obj,string field,UnityEngine.Object[] values)
        {
            var so=new SerializedObject(obj);var p=so.FindProperty(field);p.arraySize=values.Length;
            for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        public static void InstallScene(Scene scene,Dictionary<string,string> map)
        {
            if(scene.name=="Bootstrap")
            {
                var library=All<DialogueLibrary>(scene).First();
                Objects(library,"entries",map.Values.Select(AssetDatabase.LoadAssetAtPath<DialogueEntry>).Cast<UnityEngine.Object>().ToArray());
                var actions=ImportActions();
                Objects(library,"actions",actions.Cast<UnityEngine.Object>().ToArray());
                var ui=All<HudService>(scene).First();
                var presenter=ui.GetComponent<NarrativePresentation>()??ui.gameObject.AddComponent<NarrativePresentation>();
                presenter.stagedArt=actions.Where(a=>a.stageBackground!=null).ToArray();
                presenter.clothPatchMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Data/Art/SampleClothPatch.mat");
                if(presenter.clothPatchMaterial==null)
                {
                    var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Art/SampleV1/SoftClothPatch.shader");
                    if(shader!=null)
                    {
                        presenter.clothPatchMaterial=new Material(shader);
                        AssetDatabase.CreateAsset(presenter.clothPatchMaterial,"Assets/Game/Data/Art/SampleClothPatch.mat");
                    }
                }
                presenter.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Art/UI/Fonts/PlaceholderChinese.ttf");
                var journal=ui.GetComponent<NarrativeJournal>()??ui.gameObject.AddComponent<NarrativeJournal>();journal.font=presenter.font;
                if(ui.GetComponent<StoryRoomFlow>()==null)ui.gameObject.AddComponent<StoryRoomFlow>();
                ConfigureAudio(scene);
                return;
            }
            // Remove only the old story placeholders. Keep all puzzle colliders/mechanisms untouched.
            foreach(var old in All<StoryTriggerVolume>(scene).ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach(var old in All<NarrativeInteractable>(scene).ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach(var old in All<NarrativeAutoEvent>(scene).ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach(var room in All<LevelRoot>(scene).ToArray())
            {
                Transform prior=room.transform.Find("NarrativeContent");if(prior!=null)UnityEngine.Object.DestroyImmediate(prior.gameObject);
            }
            var rooms=All<LevelRoot>(scene).ToArray();
            var main=rooms.First(r=>r.RoomId!="L02Indoor");
            var ctx=Context(main,map);
            switch(scene.name)
            {
                case "L00_Gate":
                    Auto(ctx,main,"DLG_L00_INTRO_01");
                    var dogEcho=Auto(ctx,main,"DLG_L00_DOG_01",13.5f,false);
                    var dogObject=new GameObject("AHuangEcho");dogObject.transform.SetParent(ctx.Content,false);dogObject.transform.position=new Vector3(14,.12f,0);
                    var dog=dogObject.AddComponent<NarrativeDogEcho>();ArtDeliveryInstaller.ConfigureDog(dog);
                    dogEcho.requiredVisibleDog=dog;dogEcho.maximumX=dog.anchorX+2f;
                    var notice=Investigate(ctx,"notice","查看通知",new Vector2(12.5f,.9f),"DLG_L00_NOTICE_01",true);
                    notice.gameObject.AddComponent<PresentOnlyInvestigation>().anchorX=12.5f;
                    break;
                case "L01_Courtyard":
                case "L01_TreeCourtyard":
                    var treeRoom=rooms.FirstOrDefault(r=>r.RoomId=="L01B");
                    if(treeRoom==null) break;var treeCtx=Context(treeRoom,map);
                    Auto(treeCtx,treeRoom,"DLG_L01B_TREE_01",44f);
                    var marks=Investigate(treeCtx,"height_marks","查看墙面",new Vector2(44.5f,1f),"DLG_L01B_TREE_01",true);
                    LevelLayouts.SetObject(marks,"replayEntry",Entry(map,"DLG_L01B_TREE_03"));
                    Decor(treeCtx,"HeightMarks",new Vector2(44.8f,1.5f),new Vector2(1.8f,2.4f),"DECOR_HeightMarksWall");
                    break;
                case "L02_Sewing":InstallIndoor(scene,main,map);break;
                case "L03_Corridor":
                    var photo=Investigate(ctx,"photo","查看照片",new Vector2(28.3f,3.7f),"DLG_L03_REVEAL_01",true);
                    LevelLayouts.SetObject(photo,"replayEntry",Entry(map,"DLG_L03_REVEAL_02"));
                    foreach(var door in All<ExitDoor>(scene))LevelLayouts.SetObject(door,"lockedDialogue",Entry(map,"DLG_LOCK_L03_01"));
                    break;
                case "L04_Attic":
                    // Auto-play on the window sill and the E investigation share one completion flag (spec 3.11).
                    Auto(ctx,main,"DLG_L04_LOOK_01");
                    Investigate(ctx,"window_boards","查看窗边木板",new Vector2(20.9f,L04FinaleInstaller.Sill.z+.8f),"DLG_L04_LOOK_01",true);
                    var grown=Auto(ctx,main,"DLG_L04_TREE_01",-10000,false);grown.presentMechanism="L04_tree";grown.minimumY=L04FinaleInstaller.Corbel.z-1.6f;
                    Investigate(ctx,"niche","拨开木板",L04FinaleInstaller.Niche,"DLG_L04_BOX_01",false);
                    LevelLayouts.SetField(main.GetComponent<LevelDirector>(),"completeLevelOnFlag","BoxLocated");
                    break;
                case "Epilogue":
                    foreach(var door in All<ExitDoor>(scene).ToArray())UnityEngine.Object.DestroyImmediate(door.gameObject);
                    Auto(ctx,main,"DLG_EPI_01");
                    break;
            }
        }
        private static LevelLayouts.RoomContext Context(LevelRoot room,Dictionary<string,string> map)
        {
            var go=new GameObject("NarrativeContent");go.transform.SetParent(room.transform,false);
            return new LevelLayouts.RoomContext{Content=go.transform,White=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/UI/WhiteRect.png"),DialoguePaths=map,PlayerMaskValue=1<<LayerMask.NameToLayer("Player")};
        }
        private static NarrativeAutoEvent Auto(LevelLayouts.RoomContext ctx,LevelRoot room,string id,float min=-10000,bool blocking=true)
        {
            var go=new GameObject("Auto_"+id);go.transform.SetParent(ctx.Content,false);var a=go.AddComponent<NarrativeAutoEvent>();
            a.entry=ctx.FindDialogue(id);a.roomId=room.RoomId;a.minimumX=min;a.blocking=blocking;return a;
        }
        private static NarrativeInteractable Investigate(LevelLayouts.RoomContext ctx,string id,string prompt,Vector2 pos,string line,bool repeat)
        {
            var n=LevelLayouts.Narrative(ctx,id,prompt,pos,new Vector2(.65f,.85f),line,DialoguePriority.Story,repeat);
            return n;
        }
        private static void Decor(LevelLayouts.RoomContext ctx,string name,Vector2 pos,Vector2 size,string sprite)
        {
            string path="Assets/Game/Art/Props/Decor/"+sprite+".png";
            var s=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(s==null)return;
            var go=new GameObject(name);go.transform.SetParent(ctx.Content,false);go.transform.position=pos;
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=s;renderer.sortingLayerName="EraPlatform";
            go.transform.localScale=new Vector3(size.x/s.bounds.size.x,size.y/s.bounds.size.y,1);
        }
        private static void ConfigureAudio(Scene scene)
        {
            var audio=All<BorrowTomorrow.Audio.AudioService>(scene).First();
            BoundaryAudioInstaller.Configure(audio.gameObject);
            foreach(var pair in new[]{("musicPast","BGM_Past_1994"),("musicPresent","BGM_Present_2024"),("musicEpilogue","BGM_Epilogue")})
                LevelLayouts.SetObject(audio,pair.Item1,AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Music/"+pair.Item2+".wav"));
            string hissPath=AssetDatabase.FindAssets("SFX_staticloop01 t:AudioClip").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>Path.GetFileNameWithoutExtension(p)=="SFX_staticloop01");
            if(hissPath!=null)LevelLayouts.SetObject(All<DialoguePlayer>(scene).First(),"tapeHiss",AssetDatabase.LoadAssetAtPath<AudioClip>(hissPath));
            var lib=All<BorrowTomorrow.Audio.SfxLibrary>(scene).First();
            LevelLayouts.SetObject(audio,"sfxLibrary",lib);
            var so=new SerializedObject(lib);var entries=so.FindProperty("entries");
            var sounds=new[]{("sfx_tape_click","Interactions_TapePlayer_Start_01"),("sfx_tape_hiss","SFX_staticloop01"),
                ("sfx_cloth_paper","FABRIC_Flap_01_mono"),("sfx_tin_box","ACTION_Open_Metal_Hatch_01_mono"),("sfx_dog_soft","dog_bark_small_01"),
                ("door_open","SFX_DoorSqueakOldDoorWoodSlowOpening"),("player_footstep","SFX_FootstepFemaleSlipperWoodNoSqueakv1"),
                ("player_land","Footsteps_Jump_Land_01"),("player_jump","Footsteps_Jump_Start_01")};
            foreach(var pair in sounds)
            {
                string path=AssetDatabase.FindAssets(pair.Item2+" t:AudioClip").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>Path.GetFileNameWithoutExtension(p)==pair.Item2);
                if(path==null)continue;
                int index=-1;for(int i=0;i<entries.arraySize;i++)if(entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue==pair.Item1)index=i;
                if(index<0){index=entries.arraySize;entries.arraySize++;}
                var e=entries.GetArrayElementAtIndex(index);e.FindPropertyRelative("id").stringValue=pair.Item1;
                e.FindPropertyRelative("clip").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>(path);e.FindPropertyRelative("volume").floatValue=pair.Item1=="sfx_dog_soft"?.18f:.3f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void InstallIndoor(Scene scene,LevelRoot outside,Dictionary<string,string> map)
        {
            var old=All<LevelRoot>(scene).FirstOrDefault(r=>r.RoomId=="L02Indoor");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            // A separate safe interior follows the existing external puzzle, so the reveal cannot happen on the intermediate brick platform.
            var go=new GameObject("RoomRoot_L02Indoor");var room=go.AddComponent<LevelRoot>();go.AddComponent<TemporalObjectRegistry>();
            var boundary=go.AddComponent<BoundaryController>();go.AddComponent<LevelDirector>();
            LevelLayouts.SetObject(room,"definition",outside.Definition);LevelLayouts.SetField(room,"roomId","L02Indoor");
            LevelLayouts.SetField(room,"initiallyActive",false);LevelLayouts.SetField(room,"overrideBoundaryRange",true);
            LevelLayouts.SetField(room,"boundaryMin",40f);LevelLayouts.SetField(room,"boundaryMax",58f);LevelLayouts.SetField(room,"initialBoundary",58f);
            LevelLayouts.SetRect(room,"cameraBounds",new Rect(40,-1,18,8));LevelLayouts.SetObject(room,"boundaryController",boundary);
            var outsideSo=new SerializedObject(outside);LevelLayouts.SetObject(room,"playerPrefab",outsideSo.FindProperty("playerPrefab").objectReferenceValue);
            var ctx=Context(room,map);
            LevelLayouts.Box(ctx,"IndoorFloor",new Vector2(49,-.5f),new Vector2(18,1),new Color(.55f,.42f,.3f),"ConstantStructure","Ground",true);
            LevelLayouts.Box(ctx,"IndoorWallL",new Vector2(40.5f,2),new Vector2(.6f,5),Color.gray,"ConstantStructure","Ground",true);
            LevelLayouts.Box(ctx,"IndoorWallR",new Vector2(57.5f,2),new Vector2(.6f,5),Color.gray,"ConstantStructure","Ground",true);
            var spawn=LevelLayouts.Spawn(ctx,"spawn_sewing_indoor",new Vector2(43,.05f));
            var checkpoint=LevelLayouts.Spawn(ctx,"cp_L02_indoor",new Vector2(43,.05f));Objects(room,"spawnPoints",new UnityEngine.Object[]{spawn,checkpoint});
            LevelLayouts.Checkpoint(ctx,"cp_L02_indoor",new Vector2(43,.8f));
            foreach(var door in All<ExitDoor>(scene).Where(d=>d.GetComponentInParent<LevelRoot>()==outside))
            {
                LevelLayouts.SetField(door,"mode",(int)ExitDoor.TransitionMode.SameSceneRoom);LevelLayouts.SetObject(door,"targetRoom",room);
                LevelLayouts.SetField(door,"targetSpawnId","spawn_sewing_indoor");LevelLayouts.SetField(door,"ignoreLevelConditions",true);
            }
            Investigate(ctx,"uniform","翻开校服袖口",new Vector2(47,.9f),"DLG_L02_UNIFORM_01",true);
            Investigate(ctx,"ledger","查看账本",new Vector2(50.5f,.9f),"DLG_L02_LEDGER_01",true);
            Decor(ctx,"SewingMachine",new Vector2(47,1),new Vector2(2.3f,1.8f),"DECOR_SewingMachine");
            Decor(ctx,"Fabrics",new Vector2(48.5f,.75f),new Vector2(.8f,.6f),"DECOR_Fabrics");
            var tidyGo=new GameObject("TidyClothes");tidyGo.transform.SetParent(ctx.Content,false);tidyGo.transform.position=new Vector2(53,.4f);tidyGo.layer=LayerMask.NameToLayer("Interactable");
            var col=tidyGo.AddComponent<BoxCollider2D>();col.isTrigger=true;col.size=new Vector2(.8f,.8f);
            var tidy=tidyGo.AddComponent<ClothTidyInteractable>();
            Decor(ctx,"Clothes",new Vector2(53,.35f),new Vector2(1,.6f),"DECOR_ClothesOnFloor");tidy.cloth=ctx.Content.Find("Clothes");tidy.chairPosition=new Vector3(54,1.0f,0);
            var exit=LevelLayouts.Door(ctx,"前往楼上回廊",new Vector2(56,1.2f),ExitDoor.TransitionMode.NextLevel,null,null);
            LevelLayouts.SetObject(exit,"lockedDialogue",ctx.FindDialogue("DLG_LOCK_L02_01"));
        }
    }
}
