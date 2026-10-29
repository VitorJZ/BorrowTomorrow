using System;
using System.IO;
using System.Linq;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class PhotoReconstructionInstaller
    {
        private const string Folder="Assets/Game/Art/Story/L03_PhotoPuzzle_v10/";
        // Also called by the accepted-story installer so scene restoration keeps the puzzle.
        public static void Configure(NarrativeInteractable photo)
        {
            foreach(string path in Directory.GetFiles(Folder,"*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                if(importer==null){AssetDatabase.ImportAsset(path);importer=(TextureImporter)AssetImporter.GetAtPath(path);}
                importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;
                importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
            }
            var puzzle=photo.GetComponent<PhotoReconstructionPuzzle>();
            if(puzzle==null)puzzle=photo.gameObject.AddComponent<PhotoReconstructionPuzzle>();
            puzzle.workbench=Load(Folder+"ILL_PhotoWorkbench.png");
            puzzle.originalFront=Load("Assets/Game/Art/SampleV1/02_剧情插画_Illustrations/ILL_L03_Photo_Canonical_2304x1728.png");
            puzzle.originalBack=Load("Assets/Game/Art/Story/L03_Identity_v5/ILL_PhotoBack_Inscribed.png");
            puzzle.comparisonFront=Load(Folder+"ILL_Photo2001_Front.png");puzzle.comparisonBack=Load(Folder+"ILL_Photo2001_Back.png");
            puzzle.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Art/UI/Fonts/PlaceholderChinese.ttf");
            if(puzzle.font==null)throw new Exception("Chinese font missing");
            EditorUtility.SetDirty(puzzle);
        }
        private static Texture2D Load(string path){var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(t==null)throw new Exception("Missing photo puzzle art "+path);return t;}
        [MenuItem("BorrowTomorrow/剧情/接入双面照片拼合v10（保留对白配音）")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Unsaved scene changes exist");
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/L03_Corridor.scene");
            var photo=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NarrativeInteractable>(true)).Single(n=>n.InteractId=="photo");
            Configure(photo);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("[PhotoPuzzleV10] Installed local five-piece/two-sided photo reconstruction gate; accepted 17-line story, voices, route and camera unchanged.");
        }
    }
}
