using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    // Independent whole-character frame assets; no skeletal layers, no gameplay writes.
    public static class ClimbFrameInstaller
    {
        const string Root="Assets/Game/Art/Characters/ClimbFrames_v4";
        [Serializable] class Manifest {public float fps;public Source[] sources;public Step[] sequence;}
        [Serializable] class Source {public string name;public float[] rect,pivot;public float ppu;}
        [Serializable] class Step {public string source;public bool flip;}
        static T Save<T>(T value,string path)where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;
        }
        public static void Create()
        {
            AssetDatabase.Refresh();var m=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/manifest.json"));
            var frames=m.sources.ToDictionary(s=>s.name,s=>
            {
                string path=Root+"/"+s.name+".png";var imp=(TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType=TextureImporterType.Default;imp.maxTextureSize=4096;imp.mipmapEnabled=false;imp.alphaIsTransparency=true;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                return Save(Sprite.Create(texture,new Rect(s.rect[0],s.rect[1],s.rect[2],s.rect[3]),new Vector2(s.pivot[0],s.pivot[1]),s.ppu,0,SpriteMeshType.FullRect),Root+"/Frame_"+s.name+".asset");
            });
            var clip=new AnimationClip{name="Climb_Rear_Frames",frameRate=m.fps};
            var keys=m.sequence.Select((s,i)=>new ObjectReferenceKeyframe{time=i/m.fps,value=frames[s.source]}).Concat(new[]{new ObjectReferenceKeyframe{time=m.sequence.Length/m.fps,value=frames[m.sequence[0].source]}}).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);
            var flip=new AnimationCurve();for(int i=0;i<=m.sequence.Length;i++){flip.AddKey(i/m.fps,m.sequence[i%m.sequence.Length].flip?1:0);AnimationUtility.SetKeyLeftTangentMode(flip,i,AnimationUtility.TangentMode.Constant);AnimationUtility.SetKeyRightTangentMode(flip,i,AnimationUtility.TangentMode.Constant);}
            clip.SetCurve("",typeof(SpriteRenderer),"m_FlipX",flip);var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            clip=Save(clip,Root+"/Climb_Rear_Frames.anim");
            string ctrlPath=Root+"/Climb_Rear_Frames.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if(controller==null){controller=AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);var state=controller.layers[0].stateMachine.AddState("Climb");state.motion=clip;controller.layers[0].stateMachine.defaultState=state;}
            var actor=new GameObject("Climb_Frames_ReviewOnly");try{actor.AddComponent<SpriteRenderer>().sprite=frames[m.sources[0].name];actor.AddComponent<Animator>().runtimeAnimatorController=controller;PrefabUtility.SaveAsPrefabAsset(actor,Root+"/Climb_Frames_ReviewOnly.prefab");}finally{UnityEngine.Object.DestroyImmediate(actor);}
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();var output=Environment.GetEnvironmentVariable("CLIMB_FRAMES_OUTPUT");if(!string.IsNullOrEmpty(output))AssetDatabase.ExportPackage(Root,Path.Combine(output,"ClimbFrames_v4.unitypackage"),ExportPackageOptions.Recurse);
            Debug.Log("[ClimbFrames] Created whole-character frame AnimationClip, controller and review prefab; not integrated into gameplay.");
        }
    }
}
