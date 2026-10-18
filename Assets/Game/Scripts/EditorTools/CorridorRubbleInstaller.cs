using System;
using System.Linq;
using BorrowTomorrow.Level;
using BorrowTomorrow.TimeBoundary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class CorridorRubbleInstaller
    {
        private const string ArtPath="Assets/Game/Art/Props/Temporal/PROP_Corridor_CollapsedBrickwork_2024.png";

        public static void Configure(LevelRoot room)
        {
            // Preserve the stable ID so existing saves still restore the same obstacle.
            var obstacle=room.GetComponentsInChildren<TemporalObject>(true).Single(o=>o.ObjectId=="L03_cabinet");
            var present=obstacle.transform.Find("Present");
            foreach(string name in new[]{"RubbleArt","RubbleCollisions"})
            {var old=present.Find(name);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);}
            foreach(var renderer in present.GetComponentsInChildren<SpriteRenderer>(true))renderer.enabled=false;
            foreach(var collider in present.GetComponentsInChildren<Collider2D>(true))collider.enabled=false;
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            if(sprite==null)throw new InvalidOperationException("Corridor rubble sprite missing: "+ArtPath);
            var art=new GameObject("RubbleArt");art.transform.SetParent(present,false);art.transform.position=new Vector3(24,3,0);
            var sr=art.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingLayerName="EraPlatform";sr.sortingOrder=4;
            sr.sharedMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            var collisions=new GameObject("RubbleCollisions");collisions.transform.SetParent(present,false);
            // Boxes match the high central masonry. Loose low fragments are visual dressing;
            // keeping boxes also preserves inactive-era overlap safety via SAT checks.
            void Block(string name,Vector2 center,Vector2 size)
            {
                var go=new GameObject(name);go.layer=LayerMask.NameToLayer("Ground");
                go.transform.SetParent(collisions.transform,false);go.transform.position=center;
                go.AddComponent<BoxCollider2D>().size=size;
            }
            Block("MasonryCore",new Vector2(24.10f,3.825f),new Vector2(2.10f,1.65f));
            Block("LeftBrokenPier",new Vector2(23.40f,4.825f),new Vector2(.60f,.35f));
            Block("RightBrokenPier",new Vector2(24.75f,5.025f),new Vector2(.80f,.75f));
            foreach(var hint in room.Definition.hintGroups)
                if(hint.stageKey=="HintPassed_L03_Back")
                {
                    hint.hintLevel1="前方的回廊被塌落砖墙堵住了。";
                    hint.hintLevel2="石门槛不会变化，可以在这里调整时间。";
                    hint.hintLevel3="站在石门槛上，把边界推到砖墙右侧，恢复过去尚未坍塌的通道。";
                }
            EditorUtility.SetDirty(room.Definition);
        }

        [MenuItem("BorrowTomorrow/关卡/回廊柜子替换为塌落砖墙")]
        public static void Install()
        {
            AssetDatabase.Refresh();
            var importer=(TextureImporter)AssetImporter.GetAtPath(ArtPath);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=364.8f;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;
            settings.spritePivot=new Vector2(.5f,1f-1074f/1185f);importer.SetTextureSettings(settings);
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;
            importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048;importer.SaveAndReimport();
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L03_Corridor.scene");
            var room=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<LevelRoot>(true)).Single(r=>r.RoomId=="L03");
            Configure(room);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("[CorridorRubble] Replaced the cabinet with broken masonry; past is clear; stable ID, photo and door preserved.");
        }
        public static void InstallAndBuild(){Install();JumpRecoveryInstaller.BuildCurrent();}
    }
}
