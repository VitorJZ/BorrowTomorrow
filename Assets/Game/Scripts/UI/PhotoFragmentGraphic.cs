using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace BorrowTomorrow.UI
{
    // UV meshes render the existing approved source intact, without re-generating faces.
    // Adjacent pieces use the same slightly irregular fold line on BOTH sides.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PhotoFragmentGraphic : MaskableGraphic
    {
        public Texture2D source;
        public int shape;
        public bool back;
        public override Texture mainTexture => source != null ? source : Texture2D.whiteTexture;
        private static readonly Vector2[][] polygons = MakePolygons();
        private static readonly int[][] triangles = {Triangulate(polygons[0]),Triangulate(polygons[1]),Triangulate(polygons[2]),Triangulate(polygons[3])};
        private static Vector2[] Tear(int seed,params Vector2[] guide)
        {
            var points=new List<Vector2>{guide[0]};int index=0;
            for(int i=1;i<guide.Length;i++)
            {
                Vector2 a=guide[i-1],b=guide[i],delta=b-a,normal=new Vector2(-delta.y,delta.x).normalized;
                int steps=Mathf.Max(2,Mathf.CeilToInt(delta.magnitude*115));
                for(int n=1;n<steps;n++)
                {
                    float t=n/(float)steps;
                    float noise=Mathf.Sin((++index+seed*31)*12.9898f)*43758.5453f;
                    noise=(noise-Mathf.Floor(noise))-.5f;
                    points.Add(Vector2.Lerp(a,b,t)+normal*(noise*.005f*Mathf.Sin(t*Mathf.PI)));
                }
                points.Add(b);
            }
            return points.ToArray();
        }
        private static Vector2[] Reverse(Vector2[] p){var a=(Vector2[])p.Clone();Array.Reverse(a);return a;}
        private static Vector2[] Join(params Vector2[][] lines)
        {
            var a=new List<Vector2>();foreach(var line in lines)foreach(var p in line)if(a.Count==0 || (a[a.Count-1]-p).sqrMagnitude>1e-12f)a.Add(p);
            if(a.Count>1&&(a[0]-a[a.Count-1]).sqrMagnitude<1e-12f)a.RemoveAt(a.Count-1);return a.ToArray();
        }
        private static Vector2[][] MakePolygons()
        {
            // Two staggered junctions, sloping tears and unequal silhouettes, NOT a 2x2 grid.
            // The tear passes between the faces rather than through either person's face.
            var upper=Tear(1,new Vector2(.61f,0),new Vector2(.595f,.08f),new Vector2(.612f,.17f),new Vector2(.584f,.26f),new Vector2(.588f,.34f),new Vector2(.568f,.43f),new Vector2(.594f,.50f),new Vector2(.58f,.57f));
            var middle=Tear(2,new Vector2(.58f,.57f),new Vector2(.614f,.64f),new Vector2(.598f,.69f),new Vector2(.63f,.74f));
            var lower=Tear(3,new Vector2(.63f,.74f),new Vector2(.645f,.82f),new Vector2(.68f,.9f),new Vector2(.71f,1));
            var left=Tear(4,new Vector2(0,.72f),new Vector2(.105f,.70f),new Vector2(.18f,.735f),new Vector2(.29f,.66f),new Vector2(.40f,.67f),new Vector2(.49f,.60f),new Vector2(.58f,.57f));
            var right=Tear(5,new Vector2(.63f,.74f),new Vector2(.73f,.68f),new Vector2(.82f,.695f),new Vector2(.91f,.60f),new Vector2(1,.64f));
            return new[]{
                Join(new[]{Vector2.zero},upper,Reverse(left)),
                Join(new[]{upper[0],new Vector2(1,0)},Reverse(right),Reverse(middle),Reverse(upper)),
                Join(left,middle,lower,new[]{new Vector2(0,1)}),
                Join(right,new[]{Vector2.one},Reverse(lower))
            };
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        private static float SignedArea(Vector2[] p){float a=0;for(int i=0;i<p.Length;i++)a+=Cross(p[i],p[(i+1)%p.Length]);return a*.5f;}
        private static int[] Triangulate(Vector2[] p)
        {
            var remaining=new List<int>();for(int i=0;i<p.Length;i++)remaining.Add(i);
            var result=new List<int>();float sign=Mathf.Sign(SignedArea(p));int guard=p.Length*p.Length;
            while(remaining.Count>3 && guard-->0)
            {
                bool clipped=false;
                for(int i=0;i<remaining.Count;i++)
                {
                    int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];
                    if(Cross(p[b]-p[a],p[c]-p[b])*sign<=1e-10f)continue;
                    bool occupied=false;
                    foreach(int j in remaining)
                    {
                        if(j==a||j==b||j==c)continue;
                        if(Cross(p[b]-p[a],p[j]-p[a])*sign>=-1e-10f && Cross(p[c]-p[b],p[j]-p[b])*sign>=-1e-10f && Cross(p[a]-p[c],p[j]-p[c])*sign>=-1e-10f){occupied=true;break;}
                    }
                    if(occupied)continue;
                    result.Add(a);result.Add(b);result.Add(c);remaining.RemoveAt(i);clipped=true;break;
                }
                if(!clipped)throw new InvalidOperationException("Photo tear polygon could not triangulate");
            }
            result.AddRange(remaining);return result.ToArray();
        }
        private static bool Inside(int part,Vector2 point)
        {
            bool inside=false;var p=polygons[part];
            for(int i=0,j=p.Length-1;i<p.Length;j=i++)
                if((p[i].y>point.y)!=(p[j].y>point.y) && point.x<(p[j].x-p[i].x)*(point.y-p[i].y)/(p[j].y-p[i].y)+p[i].x)inside=!inside;
            return inside;
        }
        public static int PartAt(Vector2 uv){for(int i=0;i<4;i++)if(Inside(i,uv))return i;return -1;}
        public static string ValidateGeometry()
        {
            float total=0;
            for(int i=0;i<4;i++)
            {
                float area=Mathf.Abs(SignedArea(polygons[i])),mesh=0;total+=area;
                for(int n=0;n<triangles[i].Length;n+=3){var p=polygons[i];mesh+=Mathf.Abs(Cross(p[triangles[i][n+1]]-p[triangles[i][n]],p[triangles[i][n+2]]-p[triangles[i][n]]))*.5f;}
                if(Mathf.Abs(mesh-area)>.00001f)throw new Exception("Photo mesh overlap or hole "+i);
            }
            if(Mathf.Abs(total-1)>.00001f)throw new Exception("Photo pieces do not cover the source");
            for(int y=0;y<80;y++)for(int x=0;x<80;x++)
            {int hits=0;for(int i=0;i<4;i++)if(Inside(i,new Vector2((x+.37f)/80,(y+.43f)/80)))hits++;if(hits!=1)throw new Exception("Tear coverage invalid");}
            if(PartAt(new Vector2(.50f,.27f))!=0 || PartAt(new Vector2(.67f,.44f))!=1)throw new Exception("Tear crosses identity focus");
            return "PASS: four unequal ragged polygons; two staggered junctions; 6400 coverage samples; no holes/overlap; both faces intact.";
        }
        public static Rect BoundsFor(int part)
        {
            var p = polygons[part]; Vector2 min=p[0], max=p[0];
            foreach(var v in p) { min=Vector2.Min(min,v); max=Vector2.Max(max,v); }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        public static Vector2 CenterFor(int part) => BoundsFor(part).center;
        public void Set(Texture2D texture, int part, bool reverse)
        {
            source=texture; shape=part; back=reverse; SetAllDirty();
        }
        public bool Contains(Vector2 local)
        {
            Rect b=BoundsFor(shape), r=rectTransform.rect;
            float x=(local.x-r.xMin)/r.width;
            if(back)x=1-x;
            Vector2 point=new Vector2(b.xMin+x*b.width,b.yMin+(r.yMax-local.y)/r.height*b.height);
            return Inside(shape,point);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var p=polygons[shape];Rect b=BoundsFor(shape),r=rectTransform.rect;
            void Add(Vector2 v){float x=(v.x-b.xMin)/b.width;if(back)x=1-x;
                // Crop only the photographed paper's outer neutral margin, not its content.
                float u=back?1-v.x:v.x;
                vh.AddVert(new Vector3(r.xMin+x*r.width,r.yMax-(v.y-b.yMin)/b.height*r.height,0),color,new Vector2(.06f+.88f*u,.06f+.84f*(1-v.y)));}
            foreach(var v in p)Add(v);
            var indices=triangles[shape];for(int i=0;i<indices.Length;i+=3)vh.AddTriangle(indices[i],indices[i+1],indices[i+2]);
        }
    }
}
