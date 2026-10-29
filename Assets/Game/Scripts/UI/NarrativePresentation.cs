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
        private GameObject next;
        private GameObject skip;
        private bool blocking;
        private bool credits;
        private bool wasPaused;
        private DialogueEntry displayed;
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
        }
        public void Present(DialogueEntry entry, string text, bool locksControl, int page, int pages)
        {
            focusGlow.gameObject.SetActive(false);
            displayed = entry;
            blocking = locksControl;
            panel.SetActive(true);
            bool image = entry.illustration != null;
            backdrop.color = new Color(0.025f,0.022f,0.02f,(image || entry.blackBackground || entry.centeredTitle) ? 1f : 0f);
            // Blocking background clicks also avoids dragging the time handle underneath an illustration.
            backdrop.raycastTarget = locksControl;
            frame.gameObject.SetActive(image);
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
            title.gameObject.SetActive(isTitle); title.text = isTitle ? text : string.Empty;
            speaker.text = entry.speakerLabel;
            speaker.color = entry.source == "Reality" ? new Color(.1f,.25f,.29f) : new Color(.3f,.17f,.07f);
            ShowSpeaker(!isTitle); body.gameObject.SetActive(!isTitle);
            readingPanel.SetActive(!isTitle);
            body.text = text;
            help.gameObject.SetActive(true);
            help.text = (locksControl ? "E / 鼠标左键继续" : "E / 继续按钮") + (pages > 1 ? "    " + page + " / " + pages : "");
            next.SetActive(true);
            skip.SetActive(locksControl && !isTitle);
            ApplySubtitleSettings();
        }
        public void PresentAction(NarrativeAction action, float progress, string prompt, bool locksControl)
        {
            displayed = null;
            heldAction = action;
            blocking = locksControl;
            panel.SetActive(true);
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
            float ratio = (float)texture.width / texture.height;
            FrameRect.sizeDelta = ratio > PictureMax.x / PictureMax.y
                ? new Vector2(PictureMax.x, PictureMax.x / ratio) : new Vector2(PictureMax.y * ratio, PictureMax.y);
        }
        private void ResetActionView()
        {
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
            if (displayed == null || displayed.centeredTitle) return;
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
        public void Close() { focusGlow.gameObject.SetActive(false);displayed = null; if (panel != null) panel.SetActive(false); }
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
