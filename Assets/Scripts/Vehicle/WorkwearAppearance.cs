using System.Collections.Generic;
using UnityEngine;

namespace CarRapide.Vehicle
{
    /// <summary>Role-specific clothes; passenger materials stay independent of the receiver.</summary>
    public static class WorkwearAppearance
    {
        public static void Apply(GameObject character,bool receiver)
        {
            var material=Resources.Load<Material>("CarRapide/Characters/Workwear/"+(receiver?"ReceiverWorkwear":"DriverWorkwear"));
            if(!material) return;
            foreach(var renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.sharedMaterial=material;
            if(!receiver) return;
            var animator=character.GetComponent<Animator>();
            var resources=character.AddComponent<WorkwearResources>();
            var cloth=new Material(Shader.Find("Universal Render Pipeline/Lit")) {color=new Color(.105f,.13f,.18f)};
            resources.assets.Add(cloth);
            cloth.SetFloat("_Smoothness",.1f);
            Sleeve(character.transform,animator,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,cloth,resources);
            Sleeve(character.transform,animator,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,cloth,resources);
            var cap=GameObject.CreatePrimitive(PrimitiveType.Sphere); cap.name="BonnetApprenti";
            Object.Destroy(cap.GetComponent<Collider>());
            var head=animator.GetBoneTransform(HumanBodyBones.Head);
            cap.transform.SetParent(character.transform,false);
            cap.transform.position=head.position+character.transform.up*.14f;
            cap.transform.rotation=character.transform.rotation;
            cap.transform.localScale=new Vector3(.225f,.14f,.255f);
            cap.GetComponent<Renderer>().sharedMaterial=cloth;
            cap.transform.SetParent(head,true);
        }
        static void Sleeve(Transform root,Animator animator,HumanBodyBones upperBone,HumanBodyBones lowerBone,HumanBodyBones handBone,Material material,WorkwearResources resources)
        {
            var upper=animator.GetBoneTransform(upperBone); var lower=animator.GetBoneTransform(lowerBone); var hand=animator.GetBoneTransform(handBone);
            var start=Vector3.Lerp(upper.position,lower.position,.18f); var elbow=lower.position; var end=Vector3.Lerp(lower.position,hand.position,.94f);
            var vertices=new List<Vector3>(); var indices=new List<int>(); var weights=new List<BoneWeight>();
            const int sides=20,rings=9;
            for(int j=0;j<rings;j++)
            {
                float t=(float)j/(rings-1),f=t<.4f?t/.4f:(t-.4f)/.6f;
                var p=t<.4f?Vector3.Lerp(start,elbow,f):Vector3.Lerp(elbow,end,f);
                var axis=(end-start).normalized; var a=Vector3.Cross(axis,root.forward).normalized; var b=Vector3.Cross(axis,a);
                float radius=Mathf.Lerp(.081f,.041f,t)*root.lossyScale.x;
                float lowerWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.58f,t));
                for(int i=0;i<sides;i++)
                {
                    float angle=i*2*Mathf.PI/sides;
                    vertices.Add(root.InverseTransformPoint(p+radius*(Mathf.Cos(angle)*a+Mathf.Sin(angle)*b)));
                    weights.Add(new BoneWeight {boneIndex0=0,weight0=1-lowerWeight,boneIndex1=1,weight1=lowerWeight});
                    if(j==0) continue;
                    int v=j*sides+i,n=j*sides+(i+1)%sides;
                    indices.AddRange(new[]{v,v-sides,n,n,v-sides,n-sides});
                }
            }
            var mesh=new Mesh {name="MancheLongue"}; mesh.SetVertices(vertices); mesh.SetTriangles(indices,0); mesh.boneWeights=weights.ToArray();
            resources.assets.Add(mesh);
            mesh.bindposes=new[]{upper.worldToLocalMatrix*root.localToWorldMatrix,lower.worldToLocalMatrix*root.localToWorldMatrix}; mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(upperBone+"_Manche"); go.transform.SetParent(root,false);
            var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=new[]{upper,lower};renderer.rootBone=upper;renderer.sharedMaterial=material;renderer.updateWhenOffscreen=true;
        }
    }
    public sealed class WorkwearResources : MonoBehaviour
    {
        public readonly List<Object> assets=new List<Object>();
        void OnDestroy() {foreach(var asset in assets)if(asset)Destroy(asset);}
    }
}
