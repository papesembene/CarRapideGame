using System.Collections.Generic;
using UnityEngine;

namespace CarRapide.Vehicle
{
    /// <summary>Query-only collision surfaces: original triangles, including the moving door.
    /// No forces are applied to the car or the seated animation.</summary>
    public sealed class VehicleCharacterClearance : MonoBehaviour
    {
        sealed class Surface
        {
            public Transform transform;
            public string name;
            public Vector3[] vertices;
            public int[] triangles, visited;
            public Bounds bounds;
            public float cell, scale;
            public readonly Dictionary<Vector3Int,List<int>> grid=new Dictionary<Vector3Int,List<int>>();
            int stamp;
            public Surface(MeshFilter filter)
            {
                if(!filter.sharedMesh.isReadable) throw new System.InvalidOperationException("Enable Read/Write on the Car Rapide model for character clearance queries.");
                transform=filter.transform; name=filter.name;
                vertices=filter.sharedMesh.vertices; triangles=filter.sharedMesh.triangles;
                if(vertices.Length==0 || triangles.Length==0) throw new System.InvalidOperationException("Missing collision geometry: "+filter.name);
                scale=Mathf.Abs(transform.lossyScale.x); cell=.25f;
                // Work in metres: the imported FBX uses tiny coordinates and a 220x scale.
                for(int i=0;i<vertices.Length;i++) vertices[i]*=scale;
                bounds=new Bounds(filter.sharedMesh.bounds.center*scale,filter.sharedMesh.bounds.size*scale);
                visited=new int[triangles.Length/3];
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=vertices[triangles[i]]; var b=vertices[triangles[i+1]]; var c=vertices[triangles[i+2]];
                    var min=Cell(Vector3.Min(a,Vector3.Min(b,c))); var max=Cell(Vector3.Max(a,Vector3.Max(b,c)));
                    for(int x=min.x;x<=max.x;x++) for(int y=min.y;y<=max.y;y++) for(int z=min.z;z<=max.z;z++)
                    {
                        var key=new Vector3Int(x,y,z);
                        if(!grid.TryGetValue(key,out var list)) grid[key]=list=new List<int>();
                        list.Add(i);
                    }
                }
            }
            Vector3Int Cell(Vector3 p) => Vector3Int.FloorToInt(p/cell);
            public float Penetration(Vector3 world,float radius)
            {
                Vector3 p=transform.InverseTransformPoint(world)*scale;
                if(bounds.SqrDistance(p)>radius*radius) return 0;
                var min=Cell(p-Vector3.one*radius); var max=Cell(p+Vector3.one*radius);
                float distance=radius*radius; stamp++;
                for(int x=min.x;x<=max.x;x++) for(int y=min.y;y<=max.y;y++) for(int z=min.z;z<=max.z;z++)
                {
                    if(!grid.TryGetValue(new Vector3Int(x,y,z),out var list)) continue;
                    foreach(int i in list)
                    {
                        if(visited[i/3]==stamp) continue; visited[i/3]=stamp;
                        Vector3 q=Closest(p,vertices[triangles[i]],vertices[triangles[i+1]],vertices[triangles[i+2]]);
                        distance=Mathf.Min(distance,(p-q).sqrMagnitude);
                    }
                }
                return radius-Mathf.Sqrt(distance);
            }
        }
        public struct Probe { public Vector3 position; public float radius; public string name; }
        readonly List<Surface> surfaces=new List<Surface>();
        readonly List<Probe> probes=new List<Probe>();
        public IReadOnlyList<Probe> Probes => probes;
        public string WorstContact { get; private set; }
        public string GeometrySummary => string.Join("; ",surfaces.ConvertAll(s=>s.name+": "+s.triangles.Length/3+" triangles"));
        public float Measure(Animator animator,bool doorOnly=false)
        {
            BuildProbes(animator);
            float deepest=0; WorstContact="none";
            foreach(var surface in surfaces)
            {
                if(doorOnly && surface.name!="Porte_avant_gauche") continue;
                foreach(var probe in probes)
                {
                    float depth=surface.Penetration(probe.position,probe.radius);
                    if(depth<=deepest) continue;
                    deepest=depth; WorstContact=probe.name+" / "+surface.name;
                }
            }
            return deepest;
        }
        public void Initialize()
        {
            foreach(var filter in transform.Find("Car rapide").GetComponentsInChildren<MeshFilter>())
                if(filter.name=="Carosserie" || filter.name.StartsWith("Chaises") || filter.name=="Tableau de bord"
                    || filter.name=="Volant" || filter.name=="Porte_avant_gauche" || filter.name=="Porte_arriere")
                    surfaces.Add(new Surface(filter));
        }
        void BuildProbes(Animator a)
        {
            probes.Clear();
            Vector3 Bone(HumanBodyBones b) => a.GetBoneTransform(b).position;
            void Add(Vector3 p,float r,string name) => probes.Add(new Probe {position=p,radius=r,name=name});
            Add(Bone(HumanBodyBones.Hips),.115f,"pelvis");
            Add(Bone(HumanBodyBones.Head)+transform.up*.07f,.105f,"head");
            var left=Bone(HumanBodyBones.LeftUpperArm); var right=Bone(HumanBodyBones.RightUpperArm);
            Add(Vector3.Lerp(left,right,.33f)-transform.up*.10f,.105f,"chest left");
            Add(Vector3.Lerp(left,right,.67f)-transform.up*.10f,.105f,"chest right");
            void Segment(HumanBodyBones from,HumanBodyBones to,float radius,string name,float trim=0)
            {
                Vector3 p=Bone(from), q=Bone(to); q=Vector3.MoveTowards(q,p,trim);
                int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(p,q)/.06f));
                for(int i=0;i<=count;i++) Add(Vector3.Lerp(p,q,(float)i/count),radius,name);
            }
            Segment(HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,.065f,"left thigh");
            Segment(HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,.065f,"right thigh");
            Segment(HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,.045f,"left shin");
            Segment(HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,.045f,"right shin");
            Segment(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,.045f,"left arm");
            Segment(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,.045f,"right arm");
            // Hand contacts are intentional; end the arm envelope before the grip.
            Segment(HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,.032f,"left forearm",.09f);
            Segment(HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,.032f,"right forearm",.09f);
            Add(Bone(HumanBodyBones.LeftToes),.025f,"left toe");
            Add(Bone(HumanBodyBones.RightToes),.025f,"right toe");
        }
        static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector3 ab=b-a, ac=c-a, ap=p-a;
            float d1=Vector3.Dot(ab,ap), d2=Vector3.Dot(ac,ap);
            if(d1<=0 && d2<=0) return a;
            Vector3 bp=p-b; float d3=Vector3.Dot(ab,bp), d4=Vector3.Dot(ac,bp);
            if(d3>=0 && d4<=d3) return b;
            float vc=d1*d4-d3*d2;
            if(vc<=0 && d1>=0 && d3<=0) return a+ab*(d1/(d1-d3));
            Vector3 cp=p-c; float d5=Vector3.Dot(ab,cp), d6=Vector3.Dot(ac,cp);
            if(d6>=0 && d5<=d6) return c;
            float vb=d5*d2-d1*d6;
            if(vb<=0 && d2>=0 && d6<=0) return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;
            if(va<=0 && d4-d3>=0 && d5-d6>=0) return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float sum=va+vb+vc;
            return Mathf.Abs(sum)<1e-20f?a:a+ab*(vb/sum)+ac*(vc/sum);
        }
    }
}
