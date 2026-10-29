using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BorrowTomorrow.Level;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.TimeBoundary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>
    /// 2026-10-18 交付美术接入器（窄入口）：只改画面层、相机取景和少量与画面配套的碰撞，
    /// 不运行脚手架/剧情重建，不改动对白文本与事件配置。可重复执行（幂等）。
    /// 素材已从 ArtDelivery_2026-10-18 复制到 Assets/Game/Art/Delivery，原始交付目录保持不动。
    /// </summary>
    public static class ArtDeliveryInstaller
    {
        private const string D = "Assets/Game/Art/Delivery/";
        private const float Aspect = 16f / 9f;
        private static readonly Dictionary<string, RectInt> boundsCache = new Dictionary<string, RectInt>();

        // ---------------------------------------------------------------- 入口

        [MenuItem("BorrowTomorrow/接入 2026-10-18 交付美术（房间、道具、角色）")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Open scene has unsaved edits.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                ImportAssets();
                var handlers = new Dictionary<string, Action<LevelRoot>>
                {
                    {"L00", L00}, {"L01A", L01A}, {"L01B", L01B}, {"L02", L02}, {"L02Indoor", L02Indoor},
                    {"L03", L03}, {"L04", L04}, {"Epilogue", EpilogueRoom},
                };
                foreach (string sceneName in new[] { "L00_Gate", "L01_Courtyard", "L01_TreeCourtyard", "L02_Sewing", "L03_Corridor", "L04_Attic", "Epilogue" })
                {
                    if (sceneName == "L01_TreeCourtyard" && !System.IO.File.Exists(L01TreeSceneInstaller.ScenePath)) continue;
                    var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/" + sceneName + ".scene", OpenSceneMode.Single);
                    bool touched = false;
                    foreach (var room in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelRoot>(true)))
                    {
                        if (!handlers.TryGetValue(room.RoomId, out var handler)) continue;
                        handler(room);
                        touched = true;
                    }
                    if (touched) EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtDelivery] installed");
        }

        // ---------------------------------------------------------------- 导入

        private static void ImportAssets()
        {
            foreach (string path in Directory.GetFiles(D, "*.png", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) continue;
                bool dog = path.Contains("/AHuang/");
                bool background = path.Contains("/Backgrounds");
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = dog ? 200 : 100;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = dog ? new Vector2(.5f, BottomPivot(path)) : new Vector2(.5f, .5f);
                importer.SetTextureSettings(settings);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = path.Contains("PROP_L04_Tree") ? 4096 : 2560;
                importer.textureCompression = background ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        /// <summary>透明精灵的脚底 pivot：按 alpha 下沿（略留 3px），保证各帧脚底落在同一水平线。</summary>
        private static float BottomPivot(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            var px = tex.GetPixels32();
            int bottom = -1;
            for (int y = 0; y < tex.height && bottom < 0; y++)
                for (int x = 0; x < tex.width; x++)
                    if (px[y * tex.width + x].a > 40) { bottom = y; break; }
            float pivot = Mathf.Max(0, bottom - 3) / (float)tex.height;
            UnityEngine.Object.DestroyImmediate(tex);
            return pivot;
        }

        // ---------------------------------------------------------------- 通用工具

        internal static Sprite Load(string folder, string prefix)
        {
            string[] files = Directory.GetFiles(D + folder, "*.png").Select(p => p.Replace('\\', '/')).ToArray();
            string match = files.FirstOrDefault(p => Path.GetFileName(p).StartsWith(prefix, StringComparison.Ordinal));
            if (match == null) throw new FileNotFoundException("Missing delivery art " + folder + "/" + prefix);
            return AssetDatabase.LoadAssetAtPath<Sprite>(match);
        }

        /// <summary>精灵可见像素包围盒（单位：导入后纹理像素；支持子矩形精灵与导入缩小）。</summary>
        private static Rect AlphaBounds(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite.texture);
            if (!boundsCache.TryGetValue(path + sprite.rect, out var cached))
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(path));
                float f = (float)tex.width / sprite.texture.width;
                Rect r = sprite.rect;
                int x0 = Mathf.FloorToInt(r.x * f), y0 = Mathf.FloorToInt(r.y * f);
                int x1 = Mathf.Min(tex.width, Mathf.CeilToInt(r.xMax * f)), y1 = Mathf.Min(tex.height, Mathf.CeilToInt(r.yMax * f));
                var px = tex.GetPixels32();
                int minX = x1, minY = y1, maxX = -1, maxY = -1;
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                        if (px[y * tex.width + x].a > 40)
                        {
                            if (x < minX) minX = x; if (x > maxX) maxX = x;
                            if (y < minY) minY = y; if (y > maxY) maxY = y;
                        }
                UnityEngine.Object.DestroyImmediate(tex);
                cached = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                boundsCache[path + sprite.rect] = cached;
                boundsScale[path + sprite.rect] = f;
            }
            float k = boundsScale[path + sprite.rect];
            return new Rect(cached.x / k, cached.y / k, cached.width / k, cached.height / k);
        }
        private static readonly Dictionary<string, float> boundsScale = new Dictionary<string, float>();

        private static void RemoveChild(Transform parent, string name)
        {
            Transform old = parent.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }

        /// <summary>按透明包围盒摆放精灵：包围盒宽度=worldWidth，底边中点落在 anchor。</summary>
        internal static SpriteRenderer PutByBounds(Transform parent, string name, Sprite sprite, Vector2 anchorBottomCenter,
            float worldWidth, string layer = "EraPlatform", int order = 2)
        {
            RemoveChild(parent, name);
            Rect box = AlphaBounds(sprite);
            float ppu = sprite.pixelsPerUnit;
            float k = worldWidth / (box.width / ppu);
            float bx = (box.x + box.width * .5f - sprite.rect.x - sprite.pivot.x) / ppu;
            float by = (box.y - sprite.rect.y - sprite.pivot.y) / ppu;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(anchorBottomCenter.x - k * bx, anchorBottomCenter.y - k * by, 0f);
            go.transform.localScale = new Vector3(k, k, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sortingLayerName = layer; sr.sortingOrder = order;
            return sr;
        }

        internal static SpriteRenderer Put(Transform parent, string name, Sprite sprite, Vector2 center, Vector2 size,
            string layer = "EraPlatform", int order = 2)
        {
            RemoveChild(parent, name);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            go.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sortingLayerName = layer; sr.sortingOrder = order;
            return sr;
        }

        /// <summary>
        /// 背景统一等比铺满 16:9 取景；地平线(ff，距图顶比例)落在世界 gy，屏幕位置 tf（距屏顶比例）。
        /// 相机包围盒高度恰为宽/16:9，使取景只由宽度决定，各比例下都完整显示房间宽度。
        /// </summary>
        // 2026-10-18 二次生成背景：以主角身高为基准重画（门≈人物 1.35 倍），16:9 与取景等比铺满。
        private const string V2 = "Backgrounds_v2";

        private static float Backdrop(LevelRoot room, string pastBg, string presentBg, float width, float cx, float gy, float ff, float tf, string folder = "Backgrounds")
        {
            float viewH = width / Aspect + 0.7f;
            float viewW = viewH * Aspect;
            float cy = gy + viewH * (tf - .5f);
            var so = new SerializedObject(room);
            so.FindProperty("cameraBounds").rectValue = new Rect(cx - width * .5f, cy - width / Aspect * .5f, width, width / Aspect);
            so.ApplyModifiedPropertiesWithoutUndo();
            float bgCenterY = gy + viewH * (ff - .5f);
            var backdrop = room.GetComponentInChildren<EraArtBackdrop>(true);
            foreach (var pair in new[] { (backdrop.past, pastBg), (backdrop.present, presentBg) })
            {
                var sprite = Load(folder, pair.Item2);
                var r = pair.Item1;
                r.drawMode = SpriteDrawMode.Simple;
                r.sprite = sprite;
                r.transform.position = new Vector3(cx, bgCenterY, 0f);
                float scale = viewW / sprite.bounds.size.x;
                r.transform.localScale = new Vector3(scale, scale, 1f);
                r.color = Color.white;
                EditorUtility.SetDirty(r);
            }
            return viewW;
        }

        private static void MoveExit(LevelRoot room, float x, bool removeArtDoor = true)
        {
            var exit = room.GetComponentsInChildren<ExitDoor>(true).First();
            exit.transform.position = new Vector3(x, exit.transform.position.y, 0f);
            if (removeArtDoor) RemoveChild(exit.transform, "ArtDoor");
            EditorUtility.SetDirty(exit.transform);
        }

        private static void SetSkinEnabled(LevelRoot room, string parentPath, bool enabled)
        {
            var t = room.transform.Find(parentPath + "/ArtSkin");
            if (t != null) t.GetComponent<SpriteRenderer>().enabled = enabled;
        }

        // ---------------------------------------------------------------- L00 院门

        private static void L00(LevelRoot room)
        {
            float viewW = Backdrop(room, "BG_L00_Gate_1994_v2", "BG_L00_Gate_2024_v2", 26f, 13f, 0f, .81f, .81f, V2);
            // 背景里的门洞就是出口（门框中心约在图宽 70.4% 处），不再另画一扇小门。
            MoveExit(room, 13f - viewW * .5f + .704f * viewW);

            // 过去临时堆放的木箱堵住巷道，现在已清走；正面院门只负责出口转场。
            L00ObstacleInstaller.ConfigureRoom(room);
            var dog = room.GetComponentInChildren<NarrativeDogEcho>(true);
            if (dog != null) ConfigureDog(dog);
            EditorUtility.SetDirty(room);
        }

        // ---------------------------------------------------------------- L01A 水渠

        private static void L01A(LevelRoot room)
        {
            // v2 背景已在出图后按碰撞缺口（世界 x 10–14）横向配准，岸顶在图高 69.6%。
            float viewW = Backdrop(room, "BG_L01A_Canal_1994_v2", "BG_L01A_Canal_2024_v2", 20f, 10f, 0f, .696f, .696f, V2);
            SetSkinEnabled(room, "Content/BankL", false);
            SetSkinEnabled(room, "Content/BankR", false);
            MoveExit(room, 10f - viewW * .5f + .907f * viewW);

            // 新背景自带断岸与下沉水面，不再叠加裁切的缺口补丁。
            RemoveChild(room.transform.Find("Content"), "CanalGap");
            // 过去态的盖板：用侧视木板条代替被压扁的 3/4 视角整块图。
            var plank = room.GetComponentsInChildren<TemporalObject>(true).First(t => t.ObjectId == "L01A_plank_bridge");
            Reskin(plank.transform.Find("Past/PlankPast"), Strip("Strip_Wood"));
            CanalMechanismInstaller.Configure(room);
            EditorUtility.SetDirty(room);
        }

        /// <summary>阿黄：交付的透明逐帧（趴睡/抬头/摇尾）与现在态空狗窝食盆。</summary>
        public static void ConfigureDog(NarrativeDogEcho dog)
        {
            Sprite[] Frames(string prefix) => Directory.GetFiles(D + "Characters/AHuang", prefix + "*.png").OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/'))).ToArray();
            dog.sleep = Frames("AHuang_Sleep_0").Take(4).ToArray();
            dog.alert = Frames("AHuang_Alert");
            dog.wag = Frames("AHuang_Wag");
            // 与木箱之间留出明显间距，狗窝不再压住箱体。
            dog.transform.position = new Vector3(16f, 0f, 0f);
            dog.anchorX = 16f;
            var sr = dog.GetComponent<SpriteRenderer>(); if (sr == null) sr = dog.gameObject.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "EraPlatform"; sr.sortingOrder = 5; sr.sprite = dog.sleep[0];
            // 材质为空时 SpriteRenderer 不出图（阿黄曾因此在过去态“消失”），透明逐帧用默认精灵材质。
            sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            var kennel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Data/Art/Sprites/DECOR_DogBowlAndKennel_2024.asset");
            if (kennel != null)
            {
                var r = PutByBounds(dog.transform, "PresentKennel", kennel, new Vector2(16f, 0f), 1.5f, "EraPlatform", 4);
                dog.presentProps = r;
            }
            EditorUtility.SetDirty(dog);
        }

        private static Sprite Strip(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(D + "Fixed/" + name + ".png");
        private static Sprite Fixed(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(D + "Fixed/" + name + ".png");

        /// <summary>用一张贴图按碰撞盒尺寸重做某个平台的可见层（原 SpriteRenderer 灰盒保持禁用）。</summary>
        private static void Reskin(Transform host, Sprite sprite, int order = 2)
        {
            var col = host.GetComponent<BoxCollider2D>();
            RemoveChild(host, "ArtSkin");
            var go = new GameObject("ArtSkin");
            go.transform.SetParent(host, false);
            go.transform.localPosition = col.offset;
            go.transform.localScale = new Vector3(col.size.x / sprite.bounds.size.x, col.size.y / sprite.bounds.size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sortingLayerName = "EraPlatform"; sr.sortingOrder = order;
            var own = host.GetComponent<SpriteRenderer>();
            if (own != null) own.enabled = false;
        }

        private static void FixLadders(LevelRoot room)
        {
            foreach (var t in room.GetComponentsInChildren<TemporalObject>(true).Where(t => t.ObjectId.Contains("ladder")))
            {
                SwapSprite(t.transform.Find("Past/ArtSkin"), "PROP_LadderTall_1994_Fixed");
                SwapSprite(t.transform.Find("Present/ArtSkin"), "PROP_LadderTall_2024_Fixed");
            }
        }

        private static void SwapSprite(Transform t, string fixedName)
        {
            if (t == null) return;
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = Fixed(fixedName);
        }

        private static void SetRenderer(LevelRoot room, string path, bool enabled)
        {
            var t = room.transform.Find(path);
            var sr = t != null ? t.GetComponent<SpriteRenderer>() : null;
            if (sr != null) sr.enabled = enabled;
        }

        private static void L01B(LevelRoot room) { FixLadders(room); }

        // ---------------------------------------------------------------- L02 外台与室内

        private static void L02(LevelRoot room)
        {
            // v2 外台背景：二层楼板木梁（图高 65.2%）对齐外台平台顶面 y=3，二层左门即出口。
            float viewW = Backdrop(room, "BG_L02_Yard_1994_v2", "BG_L02_Yard_2024_v2", 26f, 13f, 3f, .652f, .652f, V2);
            MoveExit(room, 13f - viewW * .5f + .098f * viewW);
            SetSkinEnabled(room, "Content/Ground", true);
            FixLadders(room);
            var maintenance = room.GetComponentsInChildren<TemporalObject>(true).First(t => t.ObjectId == "L02_maintenance");
            Reskin(maintenance.transform.Find("Present/Platform"), Strip("Strip_Metal"));
        }

        private static void L02Indoor(LevelRoot room)
        {
            float viewW = Backdrop(room, "BG_L02_Room_1994_v2", "BG_L02_Room_2024_v2", 18f, 49f, 0f, .795f, .795f, V2);
            var art = room.transform.Find("ArtRoot");
            RemoveChild(art, "NearRoomWall"); RemoveChild(art, "WallTopBeam");
            SetSkinEnabled(room, "NarrativeContent/IndoorFloor", false);
            SetRenderer(room, "NarrativeContent/IndoorWallL", false);
            SetRenderer(room, "NarrativeContent/IndoorWallR", false);
            // 缝纫桌：木板条做桌面，缝纫机落在地上，校服、花布、账本摆到桌面。
            var wood = Strip("Strip_Wood");
            foreach (var name in new[] { "CraftTableTop", "CraftTableLegL", "CraftTableLegR" })
            {
                var t = art.Find(name);
                if (t != null) t.GetComponent<SpriteRenderer>().sprite = wood;
            }
            var table = art.Find("CraftTableTop");
            table.position = new Vector3(49.8f, .62f, 0f);
            table.localScale = new Vector3(3.4f / wood.bounds.size.x, .16f / wood.bounds.size.y, 1f);
            foreach (var name in new[] { "CraftTableLegL", "CraftTableLegR" })
            {
                var leg = art.Find(name);
                leg.position = new Vector3(name.EndsWith("L") ? 48.3f : 51.3f, .27f, 0f);
                leg.localScale = new Vector3(.12f / wood.bounds.size.x, .54f / wood.bounds.size.y, 1f);
            }
            var content = room.transform.Find("NarrativeContent");
            content.Find("SewingMachine").position = new Vector3(45.8f, 1.0f, 0f);
            var fabrics = content.Find("Fabrics"); fabrics.position = new Vector3(50.1f, .95f, 0f);
            var uniform = content.Find("Narrative_uniform"); uniform.position = new Vector3(48.9f, .9f, 0f);
            var ledger = content.Find("Narrative_ledger"); ledger.position = new Vector3(51.1f, .9f, 0f);
            RemoveChild(uniform, "ArtSkin");
            var blueWhite = Load("Props", "DECOR_Uniform_BlueWhite");
            var skin = PutByBounds(uniform, "ArtSkin", blueWhite, new Vector2(48.9f, .70f), 1.15f, "EraPlatform", 3);
            skin.transform.SetParent(uniform, true);
            var ledgerSkin = ledger.Find("ArtSkin");
            if (ledgerSkin != null) ledgerSkin.position = new Vector3(51.1f, .78f, 0f);
            // 出口：v2 背景右侧木门（图宽 86.2%）即通往楼上回廊的门。
            MoveExit(room, 49f - viewW * .5f + .862f * viewW);
        }

        // ---------------------------------------------------------------- L03 回廊

        private static void L03(LevelRoot room)
        {
            float viewW = Backdrop(room, "BG_L03_Corridor_1994_v2", "BG_L03_Corridor_2024_v2", 31f, 15.5f, 3f, .80f, .80f, V2);
            MoveExit(room, 15.5f - viewW * .5f + .945f * viewW);
            // 缺失地板的下方是深坑：覆盖背景画出的连续地梁，避免把缺口读成可走路面。
            var content = room.transform.Find("Content");
            RemoveChild(content, "PitFill");
            var pit = new GameObject("PitFill");
            pit.transform.SetParent(content, false);
            var white = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Props/Constant/WhiteRect.png");
            if (white == null) white = AssetDatabase.FindAssets("WhiteRect t:Sprite").Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(sp => sp != null);
            var pitSr = Put(pit.transform, "Pit", white, new Vector2(11.25f, 1.2f), new Vector2(13.5f, 3.2f), "FarBuildings", -40);
            pitSr.drawMode = SpriteDrawMode.Sliced; pitSr.size = new Vector2(13.5f, 3.2f);
            pitSr.transform.localScale = Vector3.one;
            // 半透明压暗：仍读作“缺地板的深坑”，但透出背景楼下的墙与梁，不再是一块死黑色块。
            pitSr.color = new Color(.12f, .09f, .07f, .82f);
            var floor = room.GetComponentsInChildren<TemporalObject>(true).First(t => t.ObjectId == "L03_floor");
            Reskin(floor.transform.Find("Past/Plank"), Strip("Strip_Wood"));
            var walkway = room.GetComponentsInChildren<TemporalObject>(true).First(t => t.ObjectId == "L03_walkway");
            Reskin(walkway.transform.Find("Present/Plate"), Strip("Strip_Metal"));
            Reskin(content.Find("FloorToPhoto"), Strip("Strip_Wood"));
            var photo = room.transform.Find("NarrativeContent/Narrative_photo");
            RemoveChild(photo, "ArtSkin");
            PutByBounds(photo, "ArtSkin", Load("Props", "PROP_Photo_CanonicalInFrame"), new Vector2(28.3f, 3.28f), 1.1f, "EraPlatform", 3);
            CorridorRubbleInstaller.Configure(room);
        }

        // ---------------------------------------------------------------- L04 阁楼窗外

        private static void L04(LevelRoot room) => L04FinaleInstaller.RebuildRoom(room);

        // ---------------------------------------------------------------- 尾声病房

        private static void EpilogueRoom(LevelRoot room)
        {
        }
    }
}
