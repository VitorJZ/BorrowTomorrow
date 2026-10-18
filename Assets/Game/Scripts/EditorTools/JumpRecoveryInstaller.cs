using System;
using System.IO;
using System.Linq;
using BorrowTomorrow.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    // Original whole-person jump keys plus two intact recovery drawings.
    public static class JumpRecoveryInstaller
    {
        const string Root="Assets/Game/Art/Characters/JumpRecovery_v5";
        [Serializable] class Manifest {public float fps;public Frame[] frames;}
        [Serializable] class Frame {public string texture;public float[] rect,pivot;public float ppu,durationMs;}
        static T Save<T>(T asset,string path)where T:UnityEngine.Object
        {
            var existing=AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing==null){AssetDatabase.CreateAsset(asset,path);return asset;}
            EditorUtility.CopySerialized(asset,existing);UnityEngine.Object.DestroyImmediate(asset);EditorUtility.SetDirty(existing);return existing;
        }
        static AnimationClip Clip(string name,Manifest m,Sprite[] sprites,int start,int count,bool loop,float timeScale=1f)
        {
            var clip=new AnimationClip{name=name,frameRate=m.fps};float time=0;
            var keys=new ObjectReferenceKeyframe[count+1];
            for(int i=0;i<count;i++){int index=start+i;keys[i]=new ObjectReferenceKeyframe{time=time,value=sprites[index]};time+=m.frames[index].durationMs/1000f*timeScale;}
            keys[count]=new ObjectReferenceKeyframe{time=time,value=sprites[start+count-1]};
            AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="Visual",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
            return Save(clip,Root+"/"+name+".anim");
        }
        [MenuItem("BorrowTomorrow/制作原跳跃末段补两帧候选（不接入游戏）")]
        public static void Create()
        {
            AssetDatabase.Refresh();var m=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/manifest.json"));
            if(m.frames.Length!=14)throw new InvalidOperationException("Expected original12 plus2 recovery frames.");
            foreach(string filename in m.frames.Select(f=>f.texture).Distinct())
            {
                var imp=(TextureImporter)AssetImporter.GetAtPath(Root+"/"+filename);imp.textureType=TextureImporterType.Default;
                imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.maxTextureSize=4096;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.filterMode=FilterMode.Bilinear;imp.SaveAndReimport();
            }
            var sprites=m.frames.Select((f,i)=>Save(Sprite.Create(AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+f.texture),new Rect(f.rect[0],f.rect[1],f.rect[2],f.rect[3]),new Vector2(f.pivot[0],f.pivot[1]),f.ppu,0,SpriteMeshType.FullRect),Root+"/Frame_"+i.ToString("00")+".asset")).ToArray();
            var full=Clip("Jump_Full_14_Review",m,sprites,0,14,true);
            Clip("Jump_Land_Recovery",m,sprites,9,5,false);
            float prepareScale=.08f/(m.frames.Take(3).Sum(f=>f.durationMs)/1000f);
            Clip("Jump_Prepare_Gameplay",m,sprites,0,3,false,prepareScale);
            Clip("Jump_Rise_Gameplay",m,sprites,3,3,false);
            Clip("Jump_Apex_Gameplay",m,sprites,6,1,false);
            Clip("Jump_Fall_Gameplay",m,sprites,7,2,false);
            string controllerPath=Root+"/Jump_Recovery_Review.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null){controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);var state=controller.layers[0].stateMachine.AddState("Jump");state.motion=full;controller.layers[0].stateMachine.defaultState=state;}
            var actor=new GameObject("Jump_14_Recovery_ReviewOnly");
            try
            {
                var visual=new GameObject("Visual");visual.transform.SetParent(actor.transform,false);visual.AddComponent<SpriteRenderer>().sprite=sprites[0];actor.AddComponent<Animator>().runtimeAnimatorController=controller;
                PrefabUtility.SaveAsPrefabAsset(actor,Root+"/Jump_14_ReviewOnly.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(actor);}
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();var output=Environment.GetEnvironmentVariable("JUMP_RECOVERY_OUTPUT");
            if(!string.IsNullOrEmpty(output))AssetDatabase.ExportPackage(Root,Path.Combine(output,"JumpRecovery_v5.unitypackage"),ExportPackageOptions.Recurse);
            Debug.Log("[JumpRecovery] Original12 plus2 complete-person recovery frames exported; no gameplay modifications.");
        }

        public static CharacterArtAnimator.Clip[] CreateGameplayClips(Sprite referenceIdle)
        {
            Create();
            var m=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/manifest.json"));
            var sprites=Enumerable.Range(0,14).Select(i=>AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Frame_"+i.ToString("00")+".asset")).ToArray();
            if(sprites.Any(s=>s==null))throw new InvalidOperationException("Approved14-frame jump is incomplete.");
            float scale=referenceIdle.bounds.size.y/sprites[13].bounds.size.y;
            Vector2 offset=new Vector2(0,referenceIdle.bounds.min.y-sprites[13].bounds.min.y*scale);
            CharacterArtAnimator.Clip Phase(string name,string asset,int start,int count,float total=0)
            {
                float[] holds=m.frames.Skip(start).Take(count).Select(f=>f.durationMs/1000f).ToArray();
                if(total>0){float factor=total/holds.Sum();for(int i=0;i<holds.Length;i++)holds[i]*=factor;}
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/"+asset+".anim");
                if(clip==null)throw new InvalidOperationException("Missing jump phase: "+asset);
                return new CharacterArtAnimator.Clip{name=name,frames=sprites.Skip(start).Take(count).ToArray(),animation=clip,fps=m.fps,loop=false,frameDurations=holds,visualScale=scale,visualOffset=offset};
            }
            return new[]{Phase("JumpPrepare","Jump_Prepare_Gameplay",0,3,.08f),Phase("JumpRise","Jump_Rise_Gameplay",3,3),Phase("JumpApex","Jump_Apex_Gameplay",6,1),Phase("JumpFall","Jump_Fall_Gameplay",7,2),Phase("JumpRecover","Jump_Land_Recovery",9,5)};
        }

        [MenuItem("BorrowTomorrow/接入已确认的走路与14帧跳跃")]
        public static void Integrate()
        {
            WalkCandidateInstaller.Create();
            const string path="Assets/Game/Prefabs/Player_Graybox.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var art=root.GetComponent<CharacterArtAnimator>();
                if(art==null||art.visual==null)throw new InvalidOperationException("Player art is missing.");
                var idle=art.clips.First(c=>c.name=="Idle").frames[0];
                const string walkRoot="Assets/Game/Art/Characters/WalkCandidate_v3";
                var walk=new CharacterArtAnimator.Clip
                {
                    name="Walk",fps=10,loop=true,
                    frames=Enumerable.Range(0,8).Select(i=>AssetDatabase.LoadAssetAtPath<Sprite>(walkRoot+"/Frames/Walk_"+i.ToString("00")+".asset")).ToArray(),
                    animation=AssetDatabase.LoadAssetAtPath<AnimationClip>(walkRoot+"/Hero_Walk.anim")
                };
                if(walk.frames.Any(s=>s==null)||walk.animation==null)throw new InvalidOperationException("Approved walk assets are incomplete.");
                var jump=CreateGameplayClips(idle);
                art.clips=art.clips.Where(c=>c.name!="Walk"&&!c.name.StartsWith("Jump",StringComparison.Ordinal)).Concat(new[]{walk}).Concat(jump).ToArray();
                var motor=new SerializedObject(root.GetComponent<PlayerMotor2D>());motor.FindProperty("jumpAnticipation").floatValue=.08f;motor.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(art);PrefabUtility.SaveAsPrefabAsset(root,path);
                Debug.Log("[ApprovedMovement] INSTALLED Walk=8 approved frames; Jump=14 complete-person frames,5 physical phases; existing Climb="+art.clips.First(c=>c.name=="Climb").frames.Length+" frames retained.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        public static void IntegrateAndBuild()
        {
            Integrate();
            BuildCurrent();
        }
        public static void BuildCurrent()
        {
            string output=Environment.GetEnvironmentVariable("APPROVED_MOVEMENT_OUTPUT")??Path.GetFullPath("Builds/ApprovedMovement/BorrowTomorrow.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),target=BuildTarget.StandaloneWindows64,locationPathName=output,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Approved movement build failed: "+report.summary.result);
            Debug.Log("[ApprovedMovement] BUILD SUCCEEDED: "+output);
        }
    }
}
