using UnityEngine;
using UnityEditor;
using System.Linq;
return UnityEngine.Object.FindObjectsOfType<SpriteRenderer>().Where(r=>r.name=="LedgeSupport").Select(r=>new{r.name,sprite=AssetDatabase.GetAssetPath(r.sprite),texture=r.sprite.texture.name,r.sortingOrder,bounds=r.bounds.ToString()}).ToArray();
