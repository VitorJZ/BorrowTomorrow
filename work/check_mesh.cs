using UnityEngine;
using System.Linq;
return UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Select(r=>new {r.name,shader=r.sharedMaterial.shader.name,texture=r.sharedMaterial.mainTexture.name,color=r.sharedMaterial.color.ToString()}).ToArray();
