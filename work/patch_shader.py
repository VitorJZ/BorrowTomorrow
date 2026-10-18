from pathlib import Path
p=Path('Assets/Game/Scripts/UI/NarrativePresentation.cs');s=p.read_text()
s=s.replace('private RectTransform layerRoot;', 'private RectTransform layerRoot;\n        private Material patchMaterial;')
s=s.replace('if (canvas != null) Destroy(canvas.gameObject);','if (canvas != null) Destroy(canvas.gameObject);\n            if (patchMaterial != null) Destroy(patchMaterial);')
s=s.replace('image.uvRect = layer.region;', '''image.uvRect = layer.region;
                if (layer.region.width < .99f)
                {
                    if (patchMaterial == null)
                    {
                        Shader shader = Shader.Find("BorrowTomorrow/SoftClothPatch");
                        if (shader != null) patchMaterial = new Material(shader);
                    }
                    if (patchMaterial != null)
                    {
                        patchMaterial.SetVector("_Region",new Vector4(layer.region.x,layer.region.y,layer.region.width,layer.region.height));
                        image.material = patchMaterial;
                    }
                }
                else image.material = null;''')
p.write_text(s)
