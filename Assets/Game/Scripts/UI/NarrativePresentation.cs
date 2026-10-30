using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowTomorrow.UI
{
    // Builds its own responsive overlay; the existing gameplay HUD remains intact.
    public sealed class NarrativePresentation : MonoBehaviour, INarrativePresentation, INarrativeActionView, INarrativeTargetView
    {
        private static readonly Vector2 PictureCenter = new Vector2(0, 110);
        private static readonly Vector2 PictureMax = new Vector2(1380, 720);
        private RectMask2D frame;
        private RectTransform FrameRect => (RectTransform)frame.transform;
        private Image cover;
        private RectTransform layerRoot;
        private Material patchMaterial;
        private readonly System.Collections.Generic.List<RawImage> layerImages = new System.Collections.Generic.List<RawImage>();
        public Material clothPatchMaterial;
        public NarrativeAction[] stagedArt = new NarrativeAction[0];
        private Text actionPrompt;
        private NarrativeAction heldAction;
        public Font font;
        public Sprite subtitleSkin;
        public Sprite buttonSkin;
        public Sprite nameTagSkin;
        public TutorialHintSkin tutorialHintSkin;
        public Texture2D creditsBackground;
        private Canvas canvas;
        private GameObject panel;
        private Image backdrop;
        private RawImage picture;
        private RawImage focusGlow;
        private Material focusMaterial;
        public Material photoFocusMaterial;
        private RectTransform dock;
        private GameObject readingPanel;
        private Image speakerTag;
        private Text speaker;
        private Text body;
        private Text title;
        private Text help;
        private Text documentHint;
        private GameObject next;
        private GameObject skip;
        private bool blocking;
        private bool credits;
        private bool wasPaused;
        private DialogueEntry displayed;
        // 记忆回放：黑底上叠暖色旧照调、暗角、胶片颗粒与扫描线，画面轻微呼吸。
        private RawImage echoPaper;
        private RawImage echoVignette;
        private RawImage echoGrain;
        private RawImage echoScanlines;
        private Text echoTag;
        private Texture2D echoVignetteTexture;
        private Texture2D echoGrainTexture;
        private Texture2D echoScanlineTexture;
        private Texture2D echoPaperTexture;
        private float echoAmount;
        private readonly System.Random echoRandom = new System.Random(20261030);
        private void Awake()
        {
            ServiceRegistry.Register<INarrativePresentation>(this);
            var go = new GameObject("NarrativeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // Own a root canvas: a nested screen-space canvas retains a 100x100 rect under the HUD.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            panel = new GameObject("Story", typeof(RectTransform));
            panel.transform.SetParent(go.transform, false);
            RectTransform rt = panel.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            backdrop = Node<Image>("Backdrop", panel.transform, Vector2.zero, Vector2.zero, true);
            // The frame masks action push-ins so a zoomed illustration never covers the reading panel.
            frame = Node<RectMask2D>("IllustrationFrame", panel.transform, PictureCenter, PictureMax);
            picture = Node<RawImage>("Illustration", frame.transform, Vector2.zero, Vector2.zero, true);
            picture.raycastTarget = false;
            focusGlow=Node<RawImage>("PhotoPersonFocus",frame.transform,Vector2.zero,Vector2.zero,true);focusGlow.raycastTarget=false;
            if(photoFocusMaterial!=null){focusMaterial=new Material(photoFocusMaterial);focusGlow.material=focusMaterial;}
            focusGlow.gameObject.SetActive(false);
            var layersGo = new GameObject("ActionLayers", typeof(RectTransform));
            layersGo.transform.SetParent(frame.transform, false);
            layerRoot = (RectTransform)layersGo.transform;
            layerRoot.anchorMin = Vector2.zero; layerRoot.anchorMax = Vector2.one;
            layerRoot.offsetMin = layerRoot.offsetMax = Vector2.zero;
            cover = Node<Image>("ActionCover", picture.transform, Vector2.zero, Vector2.zero);
            cover.raycastTarget = false;
            cover.gameObject.SetActive(false);
            echoPaper = Node<RawImage>("EchoPaper", frame.transform, Vector2.zero, Vector2.zero, true);
            echoPaper.texture = echoPaperTexture = MakePaperTexture();
            echoPaper.raycastTarget = false;
            echoPaper.gameObject.SetActive(false);
            echoVignette = Node<RawImage>("EchoVignette", frame.transform, Vector2.zero, Vector2.zero, true);
            echoVignette.texture = echoVignetteTexture = MakeVignetteTexture();
            echoVignette.raycastTarget = false;
            echoGrain = Node<RawImage>("EchoGrain", frame.transform, Vector2.zero, Vector2.zero, true);
            echoGrain.texture = echoGrainTexture = MakeGrainTexture();
            echoGrain.raycastTarget = false;
            echoScanlines = Node<RawImage>("EchoScanlines", frame.transform, Vector2.zero, Vector2.zero, true);
            echoScanlines.texture = echoScanlineTexture = MakeScanlineTexture();
            echoScanlines.raycastTarget = false;
            echoVignette.gameObject.SetActive(false);
            echoGrain.gameObject.SetActive(false);
            echoScanlines.gameObject.SetActive(false);
            echoTag = Label("EchoTag", panel.transform, new Vector2(-820, 500), new Vector2(220, 36), 22, TextAnchor.MiddleLeft);
            echoTag.text = "记忆回放";
            echoTag.color = new Color(1f, .93f, .8f, 0f);
            echoTag.gameObject.SetActive(false);
            title = Label("Title",panel.transform,Vector2.zero,new Vector2(1200,160),52,TextAnchor.MiddleCenter);
            // The dock keeps 1080p-relative coordinates but sticks to the bottom edge on taller screens.
            dock = (RectTransform)new GameObject("DialogDock", typeof(RectTransform)).transform;
            dock.SetParent(panel.transform, false);
            dock.anchorMin = dock.anchorMax = new Vector2(.5f, 0); dock.anchoredPosition = new Vector2(0, 540); dock.sizeDelta = new Vector2(1920, 1080);
            // Plank interior: x -770..770, text top -354, button row centred on -480.
            var box = Node<Image>("ReadingPanel", dock, new Vector2(0,-414), new Vector2(1640,228));
            box.color = new Color(0.075f,0.065f,0.055f,0.96f); box.raycastTarget = false;
            if (subtitleSkin != null) Skin(box, subtitleSkin, true);
            readingPanel = box.gameObject;
            speakerTag = Node<Image>("SpeakerTag", dock, new Vector2(-626,-296), new Vector2(300,84));
            speakerTag.color = new Color(.93f,.88f,.78f); speakerTag.raycastTarget = false;
            if (nameTagSkin != null) Skin(speakerTag, nameTagSkin, false);
            // Text sits right of the tag's eyelet so it reads centred on the clean paper.
            speaker = Label("Speaker",speakerTag.transform,new Vector2(22,1),new Vector2(220,60),26,TextAnchor.MiddleCenter);
            body = Label("Subtitle",dock,new Vector2(0,-396),new Vector2(1540,84),32,TextAnchor.UpperLeft);
            body.lineSpacing = 1.1f; body.color = new Color(.98f,.94f,.86f); Shade(body);
            help = Label("ContinueHint",dock,new Vector2(-460,-480),new Vector2(620,36),20,TextAnchor.MiddleLeft);
            help.color = new Color(.94f,.87f,.74f,.9f); Shade(help);
            documentHint = Label("DocumentHint",dock,new Vector2(0,-500),new Vector2(900,44),24,TextAnchor.MiddleCenter);
            documentHint.color = new Color(.97f,.93f,.85f,.95f); Shade(documentHint);
            documentHint.gameObject.SetActive(false);
            actionPrompt = Label("ActionPrompt",dock,new Vector2(0,-414),new Vector2(1400,80),34,TextAnchor.MiddleCenter);
            Shade(actionPrompt);
            actionPrompt.gameObject.SetActive(false);
            next = Button("继续",new Vector2(558,-480),() => { if (ServiceRegistry.TryGet<IDialogueService>(out var d)) d.Advance(); });
            skip = Button("跳过本段",new Vector2(704,-480),() => { if (ServiceRegistry.TryGet<IDialogueService>(out var d)) d.SkipCurrent(); });
            panel.SetActive(false);
        }
        private void OnDestroy()
        {
            ServiceRegistry.Unregister<INarrativePresentation>(this);
            if (canvas != null) Destroy(canvas.gameObject);
            if (patchMaterial != null) Destroy(patchMaterial);
            if(focusMaterial!=null)Destroy(focusMaterial);
            if (echoVignetteTexture != null) Destroy(echoVignetteTexture);
            if (echoGrainTexture != null) Destroy(echoGrainTexture);
            if (echoScanlineTexture != null) Destroy(echoScanlineTexture);
            if (echoPaperTexture != null) Destroy(echoPaperTexture);
        }
        private void Update()
        {
            var session = GameSession.Instance;
            if (session == null) return;
            if(focusMaterial!=null && focusGlow.gameObject.activeSelf)focusMaterial.SetFloat("_Pulse",.35f+.3f*(.5f+.5f*Mathf.Sin(Time.unscaledTime*3f)));
            canvas.enabled = !session.State.IsPaused;
            if (session.State.IsPaused) wasPaused = true;
            else if (wasPaused) { wasPaused = false; ApplySubtitleSettings(); }
            if (session.State.Phase == GamePhase.Credits && !credits)
            {
                credits = true;
                readingPanel.SetActive(false);
                panel.SetActive(true); backdrop.color = Color.black; backdrop.raycastTarget = true;
                frame.gameObject.SetActive(false); ShowSpeaker(false); body.gameObject.SetActive(false);
                if (creditsBackground != null)
                {
                    ResetActionView();
                    picture.texture = creditsBackground; picture.color = new Color(.55f,.55f,.55f,1);
                    FrameRect.anchoredPosition = Vector2.zero; FrameRect.sizeDelta = new Vector2(1920,1080);
                    frame.gameObject.SetActive(true);
                }
                actionPrompt.gameObject.SetActive(false);
                next.SetActive(false); skip.SetActive(false); help.gameObject.SetActive(false); title.gameObject.SetActive(true);
                title.text = "借一点明天\n\n谢谢你陪外婆走过这段回忆。";
            }
            else if (credits && session.State.Phase != GamePhase.Credits) { credits = false; Close(); }
            UpdateMemoryEcho();
        }
        /// <summary>
        /// 记忆回放层的显隐与质感。黑底和暖色让它与病房现实明确分开，
        /// 颗粒/扫描线/轻微呼吸只做很轻的量，避免压过手绘插画。
        /// </summary>
        private void UpdateMemoryEcho()
        {
            if (credits)
            {
                echoAmount = 0f;
                echoVignette.gameObject.SetActive(false);
                echoGrain.gameObject.SetActive(false);
                echoScanlines.gameObject.SetActive(false);
                echoTag.gameObject.SetActive(false);
                return;
            }
            bool show = displayed != null && displayed.memoryEcho;
            echoAmount = Mathf.MoveTowards(echoAmount, show ? 1f : 0f, Time.unscaledDeltaTime * 3.2f);
            bool active = echoAmount > .002f;
            echoVignette.gameObject.SetActive(active);
            echoGrain.gameObject.SetActive(active);
            echoScanlines.gameObject.SetActive(active);
            echoTag.gameObject.SetActive(active);
            picture.color = Color.Lerp(Color.white, new Color(1f, .93f, .8f), echoAmount * .9f);
            if (!active)
            {
                if (displayed == null)
                {
                    FrameRect.anchoredPosition = PictureCenter;
                    FrameRect.localRotation = Quaternion.identity;
                }
                return;
            }
            float a = echoAmount;
            echoVignette.color = new Color(1f, 1f, 1f, a);
            echoGrain.color = new Color(1f, 1f, 1f, a * .28f);
            echoScanlines.color = new Color(1f, 1f, 1f, a * .4f);
            echoTag.color = new Color(.96f, .87f, .7f, a * .62f);
            if (show)
            {
                echoTag.text = (displayed.source == "Note" ? "记忆 · 账本" : "记忆回放")
                    + (Mathf.Repeat(Time.unscaledTime, 1.15f) < .62f ? " |" : "  ");
            }
            echoGrain.uvRect = new Rect((float)echoRandom.NextDouble() * 12f, (float)echoRandom.NextDouble() * 7f, 12f, 7f);
            echoScanlines.uvRect = new Rect(0f, (Time.unscaledTime * .045f) % 1f, 1f, Mathf.Max(1f, FrameRect.rect.height / 4f));
            if (displayed != null && displayed.memoryEcho && frame.gameObject.activeSelf)
            {
                float t = Time.unscaledTime;
                float sx = (Mathf.PerlinNoise(t * 2.3f, .17f) - .5f) * 2.6f * a;
                float sy = (Mathf.PerlinNoise(.41f, t * 2.9f) - .5f) * 1.8f * a;
                FrameRect.anchoredPosition = PictureCenter + new Vector2(sx, sy);
                FrameRect.localRotation = Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t * 1.7f, 1.31f) - .5f) * .14f * a);
            }
            else
            {
                FrameRect.anchoredPosition = PictureCenter;
                FrameRect.localRotation = Quaternion.identity;
            }
        }
        public void Present(DialogueEntry entry, string text, bool locksControl, int page, int pages)
        {
            focusGlow.gameObject.SetActive(false);
            documentHint.gameObject.SetActive(false);
            displayed = entry;
            blocking = locksControl;
            panel.SetActive(true);
            bool image = entry.illustration != null;
            // 账本句没有配图，用一张程序生成的旧纸承接颗粒与暗角，避免回声段中间出现纯黑空屏。
            bool echoPaperLine = entry.memoryEcho && !image;
            backdrop.color = entry.memoryEcho
                ? new Color(.012f, .01f, .008f, 1f)
                : new Color(0.025f,0.022f,0.02f,(image || entry.blackBackground || entry.centeredTitle) ? 1f : 0f);
            // Blocking background clicks also avoids dragging the time handle underneath an illustration.
            backdrop.raycastTarget = locksControl;
            frame.gameObject.SetActive(image || echoPaperLine);
            echoPaper.gameObject.SetActive(echoPaperLine);
            if (echoPaperLine)
            {
                FrameRect.anchoredPosition = PictureCenter;
                FrameRect.localRotation = Quaternion.identity;
                FrameRect.sizeDelta = new Vector2(900, 580);
                picture.texture = null;
                picture.color = Color.clear;
            }
            if (entry.documentMode && image)
            {
                // 整页文书：只显示居中的通知图片和一行收起提示，不套对话框。
                heldAction = null;
                ResetActionView();
                ShowDocument(entry.illustration);
                backdrop.color = new Color(0.02f, 0.018f, 0.016f, .78f);
                backdrop.raycastTarget = locksControl;
                actionPrompt.gameObject.SetActive(false);
                title.gameObject.SetActive(false);
                ShowSpeaker(false);
                body.gameObject.SetActive(false);
                readingPanel.SetActive(false);
                help.gameObject.SetActive(false);
                next.SetActive(false);
                skip.SetActive(false);
                documentHint.gameObject.SetActive(true);
                documentHint.text = locksControl ? "E / 鼠标左键 收起通知" : "E / 收起通知";
                return;
            }
            // The line right after an action keeps its close-up; any later line returns to the full picture.
            bool keepClose = heldAction != null && heldAction.afterImage == entry.illustration;
            if (!keepClose) { heldAction = null; ResetActionView(); }
            if (image) ShowPicture(entry.illustration);
            if (keepClose && heldAction.layers != null && heldAction.layers.Length > 0) RenderLayers(heldAction, 1);
            else if (image) foreach (var art in stagedArt)
            {
                // Standalone after-lines also need the anchored sleeves and resting hand.
                if (art != null && art.stageBackground != null &&
                    (art.beforeImage == entry.illustration || art.afterImage == entry.illustration))
                { RenderLayers(art, art.afterImage == entry.illustration ? 1 : 0); break; }
            }
            actionPrompt.gameObject.SetActive(false);
            bool isTitle = entry.centeredTitle;
            // 黑屏居中开场：不显示对话框、按钮或继续提示，只留屏幕中央的文字。
            bool cinematic = isTitle && entry.blackBackground;
            title.gameObject.SetActive(isTitle); title.text = isTitle ? text : string.Empty;
            if (isTitle)
            {
                title.fontSize = cinematic ? 44 : 52;
                title.rectTransform.sizeDelta = cinematic ? new Vector2(1520f, 320f) : new Vector2(1200f, 160f);
                title.color = cinematic ? new Color(.98f, .95f, .88f) : Color.white;
            }
            speaker.text = entry.speakerLabel;
            speaker.color = entry.memoryEcho ? new Color(.52f,.38f,.19f)
                : entry.source == "Reality" ? new Color(.1f,.25f,.29f) : new Color(.3f,.17f,.07f);
            ShowSpeaker(!isTitle); body.gameObject.SetActive(!isTitle);
            readingPanel.SetActive(!isTitle);
            body.text = text;
            body.color = entry.memoryEcho ? new Color(1f,.95f,.85f) : new Color(.98f,.94f,.86f);
            help.gameObject.SetActive(!cinematic);
            help.text = (locksControl ? "E / 鼠标左键继续" : "E / 继续按钮") + (pages > 1 ? "    " + page + " / " + pages : "");
            next.SetActive(!cinematic);
            skip.SetActive(locksControl && !isTitle);
            ApplySubtitleSettings();
        }
        public void PresentAction(NarrativeAction action, float progress, string prompt, bool locksControl)
        {
            displayed = null;
            documentHint.gameObject.SetActive(false);
            heldAction = action;
            blocking = locksControl;
            panel.SetActive(true);
            echoPaper.gameObject.SetActive(false);
            backdrop.color = new Color(0.025f,0.022f,0.02f,1f);
            backdrop.raycastTarget = locksControl;
            float t = Mathf.SmoothStep(0f, 1f, progress);
            Texture2D texture = action.beforeImage != null && t < 0.5f ? action.beforeImage : action.afterImage;
            if (action.stageBackground != null) texture = action.stageBackground;
            frame.gameObject.SetActive(texture != null);
            if (texture != null) ShowPicture(texture);
            RenderLayers(action, progress);
            picture.rectTransform.pivot = action.focus;
            picture.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, Mathf.Max(1f, action.zoom), t);
            bool turning=action.turnPhoto&&string.IsNullOrEmpty(prompt);
            if(turning)
            {
                float edge=Mathf.Max(.025f,Mathf.Abs(Mathf.Cos(t*Mathf.PI)));
                picture.rectTransform.localScale=new Vector3(edge,1-.035f*Mathf.Sin(t*Mathf.PI),1);
                picture.rectTransform.localRotation=Quaternion.Euler(0,0,3*Mathf.Sin(t*Mathf.PI));
                float shade=1-.3f*Mathf.Sin(t*Mathf.PI);picture.color=new Color(shade,shade,shade,1);
            }
            else picture.rectTransform.localRotation=Quaternion.identity;
            // Placeholder before-state until dedicated art exists: a cover over the changed area moves away.
            bool useCover = action.stageBackground == null && action.beforeImage == null && action.coverRect.width > 0f && t < 1f;
            cover.gameObject.SetActive(useCover);
            if (useCover)
            {
                RectTransform rt = cover.rectTransform;
                rt.anchorMin = action.coverRect.min; rt.anchorMax = action.coverRect.max;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.Scale(action.coverSlide, FrameRect.sizeDelta) * t;
                Color c = action.coverColor; c.a *= 1f - Mathf.SmoothStep(0.55f, 1f, t);
                cover.color = c;
            }
            title.gameObject.SetActive(false);
            readingPanel.SetActive(!string.IsNullOrEmpty(prompt)||turning);
            ShowSpeaker(false); body.gameObject.SetActive(false);
            actionPrompt.gameObject.SetActive(!string.IsNullOrEmpty(prompt)||turning);
            actionPrompt.text = turning ? action.turnCaption : string.IsNullOrEmpty(prompt) ? string.Empty : (action.requireTargetClick?prompt:"[E] " + prompt);
            bool targetVisible=action.requireTargetClick&&!string.IsNullOrEmpty(prompt)&&progress<=0;
            focusGlow.gameObject.SetActive(targetVisible&&focusMaterial!=null);
            if(targetVisible&&focusMaterial!=null){focusGlow.texture=action.beforeImage;focusMaterial.SetVector("_TargetRect",new Vector4(action.targetRegion.x,action.targetRegion.y,action.targetRegion.width,action.targetRegion.height));}
            actionPrompt.color = new Color(.98f,.94f,.86f);
            help.gameObject.SetActive(false);
            next.SetActive(false);
            skip.SetActive(locksControl);
        }
        private void ShowPicture(Texture2D texture)
        {
            picture.texture = texture;
            picture.color = Color.white;
            FrameRect.anchoredPosition = PictureCenter;
            FrameRect.localRotation = Quaternion.identity;
            float ratio = (float)texture.width / texture.height;
            FrameRect.sizeDelta = ratio > PictureMax.x / PictureMax.y
                ? new Vector2(PictureMax.x, PictureMax.x / ratio) : new Vector2(PictureMax.y * ratio, PictureMax.y);
        }
        private void ShowDocument(Texture2D texture)
        {
            picture.texture = texture;
            picture.color = Color.white;
            const float maxWidth = 700f;
            const float maxHeight = 900f;
            float ratio = (float)texture.width / texture.height;
            FrameRect.anchoredPosition = Vector2.zero;
            // 极轻的倾斜，让纸像被放在桌上，而不是一张贴在屏幕上的矩形。
            FrameRect.localRotation = Quaternion.Euler(0f, 0f, -0.7f);
            FrameRect.sizeDelta = ratio > maxWidth / maxHeight
                ? new Vector2(maxWidth, maxWidth / ratio)
                : new Vector2(maxHeight * ratio, maxHeight);
        }
        private void ResetActionView()
        {
            FrameRect.localRotation = Quaternion.identity;
            picture.rectTransform.pivot = new Vector2(.5f,.5f);
            picture.rectTransform.localScale = Vector3.one;
            picture.rectTransform.localRotation=Quaternion.identity;
            cover.gameObject.SetActive(false);
            layerRoot.gameObject.SetActive(false);
        }
        private void RenderLayers(NarrativeAction action, float progress)
        {
            int count = action.layers == null ? 0 : action.layers.Length;
            layerRoot.gameObject.SetActive(count > 0);
            if (count == 0) return;
            ShowPicture(action.stageBackground);
            while (layerImages.Count < count)
                layerImages.Add(Node<RawImage>("Layer_" + layerImages.Count, layerRoot, Vector2.zero, Vector2.zero));
            for (int i=0;i<layerImages.Count;i++)
            {
                RawImage image = layerImages[i]; image.gameObject.SetActive(i < count);
                if (i >= count) continue;
                var layer = action.layers[i];
                float t = Mathf.SmoothStep(0,1,Mathf.InverseLerp(layer.start, layer.end, progress));
                if (layer.frames == null || layer.frames.Length == 0) { image.gameObject.SetActive(false); continue; }
                // Key poses are held briefly at each end, while hand placement interpolates continuously.
                int index = Mathf.Min(layer.frames.Length-1, Mathf.FloorToInt(t * layer.frames.Length));
                image.texture = layer.frames[index]; image.raycastTarget = false;
                image.color = new Color(1,1,1,Mathf.Lerp(layer.alphaBefore,layer.alphaAfter,t));
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
                rt.pivot = layer.pivot;
                Vector2 size = Vector2.Scale(FrameRect.sizeDelta, layer.region.size);
                rt.sizeDelta = size;
                rt.anchoredPosition = Vector2.Scale(FrameRect.sizeDelta,layer.region.center-new Vector2(.5f,.5f))
                    + Vector2.Scale(size,layer.pivot-new Vector2(.5f,.5f))
                    + Vector2.Scale(FrameRect.sizeDelta, Vector2.Lerp(layer.offsetBefore,layer.offsetAfter,t));
                rt.localScale = Vector2.Lerp(layer.scaleBefore,layer.scaleAfter,t);
                rt.localRotation = Quaternion.Euler(layer.rotationAfter*t);
                image.uvRect = layer.region;
                if (layer.region.width < .99f)
                {
                    if (patchMaterial == null)
                    {
                        if (clothPatchMaterial != null) patchMaterial = new Material(clothPatchMaterial);
                    }
                    if (patchMaterial != null)
                    {
                        patchMaterial.SetVector("_Region",new Vector4(layer.region.x,layer.region.y,layer.region.width,layer.region.height));
                        image.material = patchMaterial;
                    }
                }
                else image.material = null;
            }
        }
        private void ApplySubtitleSettings()
        {
            if (displayed == null || displayed.centeredTitle || displayed.documentMode) return;
            bool readableObject = displayed.source == "Note";
            bool enabled = !ServiceRegistry.TryGet<ISaveService>(out var save) || save.LoadSettings().subtitlesEnabled;
            // Object text remains readable even when spoken subtitles are disabled.
            body.gameObject.SetActive(enabled || readableObject);
            ShowSpeaker(enabled || readableObject);
        }
        public bool IsActionTargetHit(NarrativeAction action,Vector2 screenPosition)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(FrameRect,screenPosition,null,out var point))return false;
            Rect rect=FrameRect.rect;Vector2 uv=new Vector2((point.x-rect.xMin)/rect.width,(point.y-rect.yMin)/rect.height);
            return action.targetRegion.Contains(uv);
        }
        public void Close()
        {
            focusGlow.gameObject.SetActive(false);
            displayed = null;
            if (documentHint != null) documentHint.gameObject.SetActive(false);
            if (panel != null) panel.SetActive(false);
        }
        /// <summary>径向暗角：中心透明、四角压暗，让插画像从黑里浮出来。</summary>
        private static Texture2D MakeVignetteTexture()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "EchoVignette" };
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x / (size - 1f) * 2f - 1f;
                float dy = y / (size - 1f) * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * .70710678f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.42f, 1f, d)) * .78f;
                pixels[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
        /// <summary>可平铺的细颗粒，每帧随机偏移形成胶片噪点。</summary>
        private static Texture2D MakeGrainTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "EchoGrain" };
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            var pixels = new Color32[size * size];
            var random = new System.Random(20261030);
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)random.Next(0, 256);
                byte a = (byte)random.Next(30, 90);
                pixels[i] = new Color32(v, v, v, a);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
        /// <summary>四像素一循环的扫描线，缓慢上滚。</summary>
        private static Texture2D MakeScanlineTexture()
        {
            const int height = 4;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false, true) { name = "EchoScanline" };
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            texture.SetPixels32(new[]
            {
                new Color32(0, 0, 0, 96),
                new Color32(0, 0, 0, 0),
                new Color32(0, 0, 0, 0),
                new Color32(0, 0, 0, 0),
            });
            texture.Apply(false, true);
            return texture;
        }
        /// <summary>程序生成的旧账本纸：暗褐底、横向格线与轻微纤维噪点，不含任何文字。</summary>
        private static Texture2D MakePaperTexture()
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "EchoLedgerPaper" };
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            var random = new System.Random(1994);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int n = random.Next(-6, 7);
                int r = 58 + n, g = 49 + n, b = 36 + n;
                if ((y + 8) % 48 < 2) { r = 30; g = 24; b = 16; }
                pixels[y * size + x] = new Color32((byte)r, (byte)g, (byte)b, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
        private T Node<T>(string name, Transform parent, Vector2 position, Vector2 size, bool stretch=false) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent,false);
            var rt = go.GetComponent<RectTransform>();
            if (stretch) { rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero; }
            else { rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size; }
            return go.AddComponent<T>();
        }
        private void ShowSpeaker(bool on) => speakerTag.gameObject.SetActive(on && !string.IsNullOrEmpty(speaker.text));
        private static void Skin(Image image, Sprite sprite, bool sliced)
        {
            image.sprite = sprite; image.color = Color.white;
            if (!sliced || sprite.border == Vector4.zero) return;
            // Keep the plank's end caps at native proportions; only the plain middle stretches.
            image.type = Image.Type.Sliced; image.fillCenter = true;
            image.pixelsPerUnitMultiplier = sprite.rect.height * (100f / sprite.pixelsPerUnit) / image.rectTransform.sizeDelta.y;
        }
        private static void Shade(Text text)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.08f,.04f,.01f,.8f); shadow.effectDistance = new Vector2(1.5f,-1.5f);
        }
        private Text Label(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, TextAnchor alignment)
        {
            Text t=Node<Text>(name,parent,pos,size);t.font=font;t.fontSize=fontSize;t.alignment=alignment;t.color=Color.white;t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;
            return t;
        }
        private GameObject Button(string label, Vector2 pos, UnityEngine.Events.UnityAction action)
        {
            Image image=Node<Image>(label,dock,pos,new Vector2(132,50)); image.color=new Color(.93f,.88f,.78f,1);
            if (buttonSkin != null) Skin(image, buttonSkin, false);
            var b=image.gameObject.AddComponent<Button>();b.onClick.AddListener(action);
            var colors=b.colors;colors.normalColor=new Color(.94f,.94f,.94f);colors.highlightedColor=Color.white;colors.selectedColor=new Color(.94f,.94f,.94f);colors.pressedColor=new Color(.78f,.72f,.64f);colors.fadeDuration=.08f;b.colors=colors;
            var t=Label("Label",image.transform,new Vector2(0,1),new Vector2(120,40),22,TextAnchor.MiddleCenter);t.text=label;t.color=new Color(.24f,.12f,.04f);
            t.horizontalOverflow=HorizontalWrapMode.Overflow;
            return image.gameObject;
        }
    }
}
