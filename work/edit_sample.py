from pathlib import Path
r=Path('Assets/Game/Scripts')
p=r/'TimeBoundary/EraArtBackdrop.cs';s=p.read_text();s=s.replace('properties.SetFloat("_Boundary",value);\n            past?.SetPropertyBlock(properties);present?.SetPropertyBlock(properties);','Apply(past, value); Apply(present, value);');s=s.replace('        }\n    }\n}', '''        }
        private void Apply(SpriteRenderer renderer, float value)
        {
            if (renderer == null || renderer.sprite == null) return;
            // A SpriteRenderer also supplies texture properties. Never replace them with an empty block.
            renderer.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex", renderer.sprite.texture);
            properties.SetFloat("_Boundary", value);
            renderer.SetPropertyBlock(properties);
        }
    }
}''');p.write_text(s)
p=r/'Narrative/NarrativeAction.cs';s=p.read_text();s=s.replace('        [Header("插画前后状态")]', '''        [System.Serializable]
        public sealed class Layer
        {
            public string name;
            public Texture2D[] frames;
            public float start;
            public float end = 1;
            public float alphaBefore = 1;
            public float alphaAfter = 1;
            public Vector2 pivot = new Vector2(.5f,.5f);
            public Vector2 offsetBefore;
            public Vector2 offsetAfter;
            public Vector2 scaleBefore = Vector2.one;
            public Vector2 scaleAfter = Vector2.one;
            public Vector3 rotationAfter;
            // A local patch uses the same UV rectangle; the rest of the composition remains fixed.
            public Rect region = new Rect(0,0,1,1);
        }
        [Header("同构图动作分层（绘制顺序由后向前）")]
        public Texture2D stageBackground;
        public Layer[] layers = new Layer[0];

        [Header("插画前后状态")]''');p.write_text(s)
p=r/'UI/NarrativePresentation.cs';s=p.read_text().replace('        private Image cover;','''        private Image cover;
        private RectTransform layerRoot;
        private readonly System.Collections.Generic.List<RawImage> layerImages = new System.Collections.Generic.List<RawImage>();
        public NarrativeAction[] stagedArt = new NarrativeAction[0];''');s=s.replace('            go.transform.SetParent(transform, false);','''            // Own a root canvas: a nested screen-space canvas retains a 100x100 rect under the HUD.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);''',1)
s=s.replace('            cover = Node<Image>', '''            layerRoot = Node<RectTransform>("ActionLayers", frame.transform, Vector2.zero, Vector2.zero, true);
            cover = Node<Image>''') # AddComponent RectTransform existing => error Node<RectTransform> already exists! use Image root? fix after
s=s.replace('layerRoot = Node<RectTransform>("ActionLayers", frame.transform, Vector2.zero, Vector2.zero, true);','''var layersGo = new GameObject("ActionLayers", typeof(RectTransform));
            layersGo.transform.SetParent(frame.transform, false);
            layerRoot = (RectTransform)layersGo.transform;
            layerRoot.anchorMin = Vector2.zero; layerRoot.anchorMax = Vector2.one;
            layerRoot.offsetMin = layerRoot.offsetMax = Vector2.zero;''')
s=s.replace('scaler.matchWidthOrHeight = 0.5f;', '''scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;''')
s=s.replace('help.rectTransform.anchoredPosition=new Vector2(80,-493);help.rectTransform.sizeDelta=new Vector2(620,34);','help.rectTransform.anchoredPosition=new Vector2(-40,-493);help.rectTransform.sizeDelta=new Vector2(620,34);')
s=s.replace('private void OnDestroy() => ServiceRegistry.Unregister<INarrativePresentation>(this);', '''private void OnDestroy()
        {
            ServiceRegistry.Unregister<INarrativePresentation>(this);
            if (canvas != null) Destroy(canvas.gameObject);
        }''')
s=s.replace('bool keepClose = heldAction != null && heldAction.beforeEntryId == entry.entryId && heldAction.afterImage == entry.illustration;', 'bool keepClose = heldAction != null && heldAction.afterImage == entry.illustration;')
s=s.replace('            if (image) ShowPicture(entry.illustration);','''            if (image) ShowPicture(entry.illustration);
            if (keepClose && heldAction.layers != null && heldAction.layers.Length > 0) RenderLayers(heldAction, 1);
            else if (image) foreach (var art in stagedArt)
            {
                if (art != null && art.beforeImage == entry.illustration && art.stageBackground != null)
                { RenderLayers(art, 0); break; }
            }''')
s=s.replace('            frame.gameObject.SetActive(texture != null);','''            if (action.stageBackground != null) texture = action.stageBackground;
            frame.gameObject.SetActive(texture != null);''')
s=s.replace('            picture.rectTransform.pivot = action.focus;', '''            RenderLayers(action, progress);
            picture.rectTransform.pivot = action.focus;''')
s=s.replace('bool useCover = action.beforeImage == null', 'bool useCover = action.stageBackground == null && action.beforeImage == null')
s=s.replace('actionPrompt.color = subtitleSkin != null ? new Color(.3f,.17f,.07f) : Color.white;','actionPrompt.color = new Color(1f,.95f,.78f);')
s=s.replace('            cover.gameObject.SetActive(false);\n        }\n        private void ApplySubtitleSettings()', '''            cover.gameObject.SetActive(false);
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
            }
        }
        private void ApplySubtitleSettings()''')
s=s.replace('if (buttonSkin != null) { image.sprite = buttonSkin;', 'if (buttonSkin != null) { image.sprite = buttonSkin;')
p.write_text(s)
p=r/'UI/NarrativeJournal.cs';s=p.read_text();s=s.replace('go.transform.SetParent(transform,false);','UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,gameObject.scene);',1).replace('scaler.referenceResolution=new Vector2(1920,1080);','scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;');s=s.replace('content.pivot=new Vector2(.5f,1);','content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(-40,790);content.anchoredPosition=Vector2.zero;');s=s.replace('        private void Update()', '''        private void OnDestroy() { if(canvas!=null) Destroy(canvas.gameObject); }
        private void Update()''');p.write_text(s)
