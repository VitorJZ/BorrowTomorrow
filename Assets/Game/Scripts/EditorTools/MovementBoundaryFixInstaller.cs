using System;
using UnityEditor;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class MovementBoundaryFixInstaller
    {
        [MenuItem("BorrowTomorrow/应用碰墙下落与边界操作修正")]
        public static void Apply()
        {
            const string materialPath = "Assets/Game/Data/Physics/PlayerSlide.physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath);
            if (material == null)
            {
                material = new PhysicsMaterial2D("PlayerSlide");
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.friction = 0f;
            material.bounciness = 0f;
            EditorUtility.SetDirty(material);
            const string prefabPath = "Assets/Game/Prefabs/Player_Graybox.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var capsule = root.GetComponent<CapsuleCollider2D>();
                var body = root.GetComponent<Rigidbody2D>();
                if (capsule == null || body == null) throw new InvalidOperationException("Player physics is missing.");
                capsule.sharedMaterial = material;
                body.sharedMaterial = material;
                EditorUtility.SetDirty(capsule);
                EditorUtility.SetDirty(body);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[MovementBoundaryFix] INSTALLED PlayerSlide friction=0 bounce=0; per-box target safety; boundary feedback and offscreen keyboard movement.");
        }

        public static void ApplyAndBuild()
        {
            Apply();
            JumpRecoveryInstaller.BuildCurrent();
        }
    }
}
