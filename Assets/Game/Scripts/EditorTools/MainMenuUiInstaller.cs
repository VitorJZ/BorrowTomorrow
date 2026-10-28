using System;
using BorrowTomorrow.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>
    /// 把主菜单从“整张底图 + 透明点击区”重建为真正的木牌按钮。
    /// 底图和按钮共同放在固定 16:9 的 MenuStage 中，任意屏幕比例下保持对齐。
    /// </summary>
    public static class MainMenuUiInstaller
    {
        public const string ScenePath = "Assets/Game/Scenes/MainMenu.scene";
        private const string ButtonTexturePath = "Assets/Game/Art/UI/UI_MainMenuButton.png";
        private const string BackgroundSpritePath = "Assets/Game/Data/Art/Sprites/UI_TitleScreen.asset";
        private const string FontPath = "Assets/Game/Art/UI/Fonts/PlaceholderChinese.ttf";
        private const string ClickClipPath = "Assets/AudioLib/ed963ccc1ec9bdfa/8638ef365f0cb6ae/BUTTON_Clean_Tap_mono.wav";
        private static readonly Vector2 DesignSize = new Vector2(1920f, 1080f);

        [MenuItem("BorrowTomorrow/重建主菜单按钮界面")]
        public static void RebuildScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[MainMenuUiInstaller] 主菜单已重建为 16:9 舞台 + 三个木牌按钮。");
        }

        public static void Apply(Scene scene, Sprite backgroundOverride = null)
        {
            EnsureButtonTexture();
            Sprite buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonTexturePath);
            if (buttonSprite == null)
            {
                throw new InvalidOperationException("找不到按钮贴图：" + ButtonTexturePath);
            }
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            AudioClip clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClickClipPath);

            MainMenuUi menu = FindInScene<MainMenuUi>(scene);
            if (menu == null)
            {
                throw new InvalidOperationException("MainMenu 场景里找不到 MainMenuUi。");
            }
            var canvas = (RectTransform)menu.transform;

            // 宽屏/竖屏时露出的区域用底图边缘的暖深色填充，形成电影式画框。
            Transform backdrop = EnsureChild(canvas, "MenuBackdrop");
            backdrop.SetAsFirstSibling();
            var backdropRect = (RectTransform)backdrop;
            Stretch(backdropRect);
            var backdropImage = backdrop.GetComponent<Image>();
            if (backdropImage == null)
            {
                backdropImage = backdrop.gameObject.AddComponent<Image>();
            }
            backdropImage.sprite = null;
            backdropImage.color = new Color(0.13f, 0.115f, 0.095f, 1f);
            backdropImage.raycastTarget = false;

            Transform stage = EnsureChild(canvas, "MenuStage");
            var stageRect = (RectTransform)stage;
            stageRect.anchorMin = stageRect.anchorMax = new Vector2(0.5f, 0.5f);
            stageRect.pivot = new Vector2(0.5f, 0.5f);
            stageRect.anchoredPosition = Vector2.zero;
            stageRect.sizeDelta = DesignSize;
            stageRect.localScale = Vector3.one;
            var stageFitter = stage.GetComponent<MenuCoverStage>();
            if (stageFitter == null)
            {
                stageFitter = stage.gameObject.AddComponent<MenuCoverStage>();
            }
            var stageFitterSerialized = new SerializedObject(stageFitter);
            stageFitterSerialized.FindProperty("fitInside").boolValue = true;
            stageFitterSerialized.ApplyModifiedPropertiesWithoutUndo();
            stage.SetSiblingIndex(1);

            Transform background = FindOrReparent(canvas, stage, "ArtMainMenu");
            var backgroundImage = background.GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = background.gameObject.AddComponent<Image>();
            }
            if (backgroundImage.sprite == null)
            {
                backgroundImage.sprite = backgroundOverride ?? AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath);
            }
            else if (backgroundOverride != null)
            {
                backgroundImage.sprite = backgroundOverride;
            }
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = false;
            backgroundImage.raycastTarget = false;
            Stretch((RectTransform)background);
            background.SetSiblingIndex(0);

            ReparentInactiveText(canvas, stage, "Title", 330f, new Vector2(900f, 120f));
            ReparentInactiveText(canvas, stage, "Subtitle", 250f, new Vector2(600f, 40f));

            var serialized = new SerializedObject(menu);
            SetupButton(serialized.FindProperty("newGameButton").objectReferenceValue as Button,
                stage, "Button_开始新游戏", "开始新游戏", -68f, buttonSprite, font, false, Color.white);
            SetupButton(serialized.FindProperty("continueButton").objectReferenceValue as Button,
                stage, "Button_继续游戏", "继续游戏", -226f, buttonSprite, font, true,
                new Color(0.98f, 0.97f, 0.96f, 1f));
            SetupButton(serialized.FindProperty("quitButton").objectReferenceValue as Button,
                stage, "Button_退出游戏", "退出游戏", -388f, buttonSprite, font, false,
                new Color(0.95f, 0.93f, 0.91f, 1f));

            SerializedProperty clip = serialized.FindProperty("clickClip");
            if (clip != null)
            {
                clip.objectReferenceValue = clickClip;
            }
            SerializedProperty volume = serialized.FindProperty("clickVolume");
            if (volume != null)
            {
                volume.floatValue = 0.45f;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupButton(Button button, Transform parent, string name, string label,
            float y, Sprite sprite, Font font, bool mirror, Color plaqueTint)
        {
            if (button == null)
            {
                return;
            }
            button.gameObject.name = name;
            var root = (RectTransform)button.transform;
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, y);
            root.sizeDelta = new Vector2(500f, 150f);
            root.localScale = Vector3.one;

            // 根节点的 Image 只作为稳定的点击热区：完全透明，但不让 CanvasRenderer
            // 把它剔除，否则 GraphicRaycaster 会跳过它。
            var hitGraphic = button.GetComponent<Image>();
            if (hitGraphic != null)
            {
                hitGraphic.sprite = null;
                hitGraphic.color = new Color(1f, 1f, 1f, 0f);
                hitGraphic.raycastTarget = true;
                var hitRenderer = button.GetComponent<CanvasRenderer>();
                if (hitRenderer != null)
                {
                    hitRenderer.cullTransparentMesh = false;
                }
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
            }

            var visualObject = new GameObject("Visual", typeof(RectTransform));
            var visual = (RectTransform)visualObject.transform;
            visual.SetParent(root, false);
            visual.anchorMin = visual.anchorMax = new Vector2(0.5f, 0.5f);
            visual.pivot = new Vector2(0.5f, 0.5f);
            visual.anchoredPosition = Vector2.zero;
            visual.sizeDelta = new Vector2(480f, 142f);
            var image = visualObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = true;
            image.color = plaqueTint;

            var plaqueShadow = visualObject.AddComponent<Shadow>();
            plaqueShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            plaqueShadow.effectDistance = new Vector2(0f, -5f);
            plaqueShadow.useGraphicAlpha = true;

            var labelObject = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(visual, false);
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, 0f);
            labelRect.sizeDelta = new Vector2(430f, 90f);
            var text = labelObject.AddComponent<Text>();
            text.text = label;
            text.font = font;
            text.fontSize = 46;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.92f, 0.76f, 1f);
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 32;
            text.resizeTextMaxSize = 52;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var labelShadow = labelObject.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0.1f, 0.05f, 0.02f, 0.85f);
            labelShadow.effectDistance = new Vector2(1.5f, -2f);

            if (mirror)
            {
                // 翻转木牌的纹理避免三块按钮一模一样；文字再翻转回来保持可读。
                visual.localScale = new Vector3(-1f, 1f, 1f);
                labelRect.localScale = new Vector3(-1f, 1f, 1f);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.06f, 0.98f, 1f);
            colors.pressedColor = new Color(0.7f, 0.6f, 0.5f, 1f);
            colors.selectedColor = new Color(1.06f, 1.03f, 0.96f, 1f);
            colors.disabledColor = new Color(0.4f, 0.38f, 0.36f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };

            var fx = button.GetComponent<MenuButtonFx>();
            if (fx == null)
            {
                fx = button.gameObject.AddComponent<MenuButtonFx>();
            }
            var fxSerialized = new SerializedObject(fx);
            fxSerialized.FindProperty("visual").objectReferenceValue = visual;
            fxSerialized.FindProperty("label").objectReferenceValue = text;
            fxSerialized.FindProperty("hoverScale").floatValue = 1.02f;
            fxSerialized.FindProperty("pressedScale").floatValue = 0.985f;
            fxSerialized.FindProperty("pressedDrop").floatValue = 3f;
            fxSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ReparentInactiveText(Transform canvas, Transform stage, string name, float y, Vector2 size)
        {
            Transform target = FindOrReparent(canvas, stage, name);
            var rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = size;
            var text = target.GetComponent<Text>();
            if (text != null)
            {
                text.enabled = false;
            }
        }

        private static void EnsureButtonTexture()
        {
            AssetDatabase.ImportAsset(ButtonTexturePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(ButtonTexturePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }
            if (importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && importer.mipmapEnabled == false
                && importer.alphaIsTransparency
                && importer.maxTextureSize >= 2048
                && importer.textureCompression == TextureImporterCompression.Uncompressed)
            {
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing;
            }
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform FindOrReparent(Transform searchRoot, Transform newParent, string name)
        {
            Transform existing = newParent.Find(name);
            if (existing != null)
            {
                return existing;
            }
            existing = searchRoot.Find(name);
            if (existing != null)
            {
                existing.SetParent(newParent, false);
                return existing;
            }
            return EnsureChild(newParent, name);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
