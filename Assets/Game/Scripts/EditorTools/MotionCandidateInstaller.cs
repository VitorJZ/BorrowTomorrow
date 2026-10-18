using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    // Review-only assets. No reference to gameplay prefabs or scenes.
    public static class MotionCandidateInstaller
    {
        const string Root="Assets/Game/Art/Characters/MotionCandidates_v3";
        [Serializable] class Manifest { public float fps;public Frame[] frames; }
        [Serializable] class Frame {public float[] rect,pivot;public float ppu;}
        [Serializable] class Rig {public Part[] parts;public Pose[] poses;public float duration,ppu;}
        [Serializable] class Part {public string name;public float[] start,end;public float[][] polygon;}
        [Serializable] class Pose {public float time;public Placement[] parts;}
        [Serializable] class Placement {public float[] position;public float angle,length;}
        // JsonUtility cannot deserialize jagged arrays: use a flat polygon field.
        [Serializable] class RigFlat {public PartFlat[] parts;public Pose[] poses;public float duration,ppu;}
        [Serializable] class PartFlat {public string name;public float[] start,end,vertices;}
        static T Save<T>(T value,string path)where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;
        }
        static Texture2D Texture(string dir)
        {
            string path=dir+"/Source.png";var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType=TextureImporterType.Default;imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.maxTextureSize=4096;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static AnimationClip SpriteClip(string dir,string name,Sprite[] frames,float fps)
        {
            var clip=new AnimationClip{frameRate=fps,name=name};
            var keys=frames.Select((s,i)=>new ObjectReferenceKeyframe{time=i/fps,value=s}).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);
            return Save(clip,dir+"/"+name+".anim");
        }
        static void MakeSpriteCandidate(string name)
        {
            string dir=Root+"/"+name;var tex=Texture(dir);var m=JsonUtility.FromJson<Manifest>(File.ReadAllText(dir+"/manifest.json"));
            var frames=m.frames.Select((f,i)=>Save(Sprite.Create(tex,new Rect(f.rect[0],f.rect[1],f.rect[2],f.rect[3]),new Vector2(f.pivot[0],f.pivot[1]),f.ppu,0,SpriteMeshType.FullRect),dir+"/Frame_"+i.ToString("00")+".asset")).ToArray();
            SpriteClip(dir,name,frames,m.fps);
            if(name=="Jump")
            {
                SpriteClip(dir,"Jump_Prepare",frames.Take(4).ToArray(),m.fps);
                SpriteClip(dir,"Jump_Rise",frames.Skip(4).Take(2).ToArray(),m.fps);
                SpriteClip(dir,"Jump_Apex",frames.Skip(6).Take(1).ToArray(),m.fps);
                SpriteClip(dir,"Jump_Fall",frames.Skip(7).Take(2).ToArray(),m.fps);
                SpriteClip(dir,"Jump_Land",frames.Skip(9).ToArray(),m.fps);
            }
        }
        static AnimationCurve Curve(RigFlat rig,int part,Func<Placement,float> select)
        {
            var curve=new AnimationCurve(rig.poses.Select(p=>new Keyframe(p.time,select(p.parts[part]))).ToArray());
            for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}return curve;
        }
        public static void Create()
        {
            AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string output=Environment.GetEnvironmentVariable("MOTION_CANDIDATE_OUTPUT");
            MakeSpriteCandidate("Jump");AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            AssetDatabase.ExportPackage(Root,Path.Combine(output,"MotionCandidates_v3.unitypackage"),ExportPackageOptions.Recurse);
            Debug.Log("[MotionCandidates] Independent jump clips exported. Gameplay unchanged.");
        }
    }
}
