using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    // All frames contain the complete character. No rig, limbs or gameplay edits.
    public static class JumpFrameInstaller
    {
        const string Root="Assets/Game/Art/Characters/JumpFrames_v4";
        [Serializable] class Manifest {public float fps;public Frame[] frames;public Phase[] phases;}
        [Serializable] class Frame {public string texture;public float[] rect,pivot;public float ppu,poseLift;}
        [Serializable] class Phase {public string name;public int start,count;}
        static T Save<T>(T value,string path)where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;
        }
        static AnimationClip MakeClip(string name,Manifest m,Sprite[] sprites,int start,int count)
        {
            var clip=new AnimationClip{name=name,frameRate=m.fps};var keys=new ObjectReferenceKeyframe[count+1];var lift=new AnimationCurve();
            for(int i=0;i<=count;i++)
            {
                int index=start+Mathf.Min(i,count-1);float time=i/m.fps;
                keys[i]=new ObjectReferenceKeyframe{time=time,value=sprites[index]};
                lift.AddKey(time,m.frames[index].poseLift);
                AnimationUtility.SetKeyLeftTangentMode(lift,i,AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(lift,i,AnimationUtility.TangentMode.Constant);
            }
            AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="Visual",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);
            // Move the whole sprite to keep the hip at its natural height when
            // the feet tuck. Grounded frames retain the original floor contact.
            clip.SetCurve("Visual",typeof(Transform),"localPosition.y",lift);
            return Save(clip,Root+"/"+name+".anim");
        }
        [MenuItem("BorrowTomorrow/制作完整人物24帧跳跃候选（不接入游戏）")]
        public static void Create()
        {
            AssetDatabase.Refresh();var m=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/manifest.json"));
            if(m.frames.Length!=24||m.fps!=24)throw new InvalidOperationException("Expected24 whole-character frames at24fps.");
            foreach(string filename in m.frames.Select(f=>f.texture).Distinct())
            {
                var imp=(TextureImporter)AssetImporter.GetAtPath(Root+"/"+filename);imp.textureType=TextureImporterType.Default;
                imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.maxTextureSize=4096;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.filterMode=FilterMode.Bilinear;imp.SaveAndReimport();
            }
            var sprites=m.frames.Select((f,i)=>
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+f.texture);
                return Save(Sprite.Create(texture,new Rect(f.rect[0],f.rect[1],f.rect[2],f.rect[3]),new Vector2(f.pivot[0],f.pivot[1]),f.ppu,0,SpriteMeshType.FullRect),Root+"/Frame_"+i.ToString("00")+".asset");
            }).ToArray();
            var full=MakeClip("Jump_Full_24",m,sprites,0,sprites.Length);
            foreach(var phase in m.phases)MakeClip("Jump_"+phase.name,m,sprites,phase.start,phase.count);
            string ctrlPath=Root+"/Jump_Review.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if(controller==null)
            {
                controller=AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);var state=controller.layers[0].stateMachine.AddState("Jump");state.motion=full;controller.layers[0].stateMachine.defaultState=state;
            }
            var actor=new GameObject("Jump_24_WholeCharacter_ReviewOnly");
            try
            {
                var visual=new GameObject("Visual");visual.transform.SetParent(actor.transform,false);visual.AddComponent<SpriteRenderer>().sprite=sprites[0];
                actor.AddComponent<Animator>().runtimeAnimatorController=controller;
                PrefabUtility.SaveAsPrefabAsset(actor,Root+"/Jump_24_ReviewOnly.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(actor);}
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();var output=Environment.GetEnvironmentVariable("JUMP_FRAMES_OUTPUT");
            if(!string.IsNullOrEmpty(output))AssetDatabase.ExportPackage(Root,Path.Combine(output,"JumpFrames_v4_24fps.unitypackage"),ExportPackageOptions.Recurse);
            Debug.Log("[JumpFrames] Exported24 complete-person Sprite assets, 24fps full AnimationClip and five phase clips. Gameplay unchanged.");
        }
    }
}
