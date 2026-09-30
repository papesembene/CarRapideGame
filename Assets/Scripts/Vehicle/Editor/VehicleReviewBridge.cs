#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using CarRapide.Vehicle;

// Local development harness. Requests and output stay in the ignored Library directory.
[InitializeOnLoad]
public static class VehicleReviewBridge
{
    const string Folder = "Library/VehicleReview";
    static VehicleReviewBridge()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message,stack,type) => {
            if(type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
            { Directory.CreateDirectory(Folder); File.AppendAllText(Folder+"/unity-log.txt",type+": "+message+"\n"+stack+"\n"); }
        };
    }
    static void Tick()
    {
        if (EditorApplication.isCompiling || !File.Exists(Folder + "/request.txt")) return;
        string command;
        try { command = File.ReadAllText(Folder + "/request.txt").Trim(); File.Delete(Folder + "/request.txt"); }
        catch (IOException) { return; }
        try
        {
            if (command == "inspect") Inspect();
            else if(command=="refresh") AssetDatabase.Refresh();
            else if(command=="calibrate-clearance")
            {
                if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
                var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/Art/Vehicles/CarRapide/Car rapide.fbx");
                if(!importer.isReadable) { importer.isReadable=true; importer.SaveAndReimport(); }
                var points=UnityEngine.Object.FindAnyObjectByType<VehicleInteractionPoints>();
                CarRapide.EditorTools.VehicleExperienceInstaller.CalibrateCabin(points);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(points.gameObject.scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(points.gameObject.scene);
            }
            else if (command == "install") CarRapide.EditorTools.VehicleExperienceInstaller.Install();
            else if (command == "play") EditorApplication.isPlaying = true;
            else if (command == "stop") EditorApplication.isPlaying = false;
            else if (command == "capture") Capture();
            else if (command == "board") UnityEngine.Object.FindAnyObjectByType<VehicleDriverExperience>().Driver.RequestBoard();
            else if (command == "engine") UnityEngine.Object.FindAnyObjectByType<VehicleDriverExperience>().Engine.RequestStart();
            else if (command == "passenger") UnityEngine.Object.FindAnyObjectByType<VehicleDriverExperience>().Passenger.RequestDemo();
            else if (command == "review") CarRapide.EditorTools.VehicleExperienceReview.Begin();
            else if (command == "review-video") CarRapide.EditorTools.VehicleExperienceReview.BeginVideo();
            else if (command == "review-passenger") CarRapide.EditorTools.VehicleExperienceReview.BeginPassenger();
            else if (command == "snapshot") Snapshot();
            else if (command == "probe-poses") ProbePoses();
            else if (command == "bake") CarRapide.EditorTools.VehicleMotionBaker.Bake();
            else if (command == "scale-preview")
            {
                var v = UnityEngine.Object.FindAnyObjectByType<VehicleController>();
                v.transform.Find("Car rapide").localScale = Vector3.one * 2.2f;
                v.transform.position = new Vector3(0,0.03634f,0);
                var box = v.GetComponent<BoxCollider>();
                box.center = new Vector3(0,1.23f,0); box.size = new Vector3(2.14f,2.53268f,5.36f);
                Camera.main.transform.position = new Vector3(-4.5f,2.4f,4.5f);
                Camera.main.transform.LookAt(new Vector3(-0.3f,1.1f,0.5f));
                Capture();
            }
            File.WriteAllText(Folder + "/result.txt", "OK " + command);
        }
        catch (Exception e) { File.WriteAllText(Folder + "/result.txt", e.ToString()); Debug.LogException(e); }
    }
    static void Inspect()
    {
        var vehicle = UnityEngine.Object.FindAnyObjectByType<VehicleController>();
        var s = new StringBuilder();
        s.AppendLine("Vehicle world=" + vehicle.transform.position.ToString("F4") + " scale=" + vehicle.transform.lossyScale);
        foreach (var m in vehicle.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = m.sharedMesh;
            var points = mesh.vertices.Select(v => vehicle.transform.InverseTransformPoint(m.transform.TransformPoint(v))).ToArray();
            var b = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) b.Encapsulate(p);
            s.AppendLine($"{m.name} vertices={points.Length} pivot={vehicle.transform.InverseTransformPoint(m.transform.position).ToString("F4")} min={b.min.ToString("F4")} max={b.max.ToString("F4")}");
            var materials=m.GetComponent<Renderer>().sharedMaterials;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                var indices=mesh.GetTriangles(sub);
                var sb=new Bounds(points[indices[0]],Vector3.zero);
                foreach(int index in indices)sb.Encapsulate(points[index]);
                s.AppendLine($" submesh={sub} triangles={indices.Length/3} material={materials[sub].name} shader={materials[sub].shader.name} color={materials[sub].color} min={sb.min.ToString("F4")} max={sb.max.ToString("F4")}");
            }
            File.WriteAllLines(Folder + "/mesh-" + m.name.Replace("/", "_") + ".csv", points.Select(p => FormattableString.Invariant($"{p.x:R},{p.y:R},{p.z:R}")));
            var indicesAll=mesh.triangles;
            File.WriteAllLines(Folder+"/tri-"+m.name+".csv",Enumerable.Range(0,indicesAll.Length/3).Select(i=>$"{indicesAll[i*3]},{indicesAll[i*3+1]},{indicesAll[i*3+2]}"));
            if(m.name=="Carosserie" || m.name=="Porte_avant_gauche" || m.name=="Porte_avant_droit")
            {
                File.WriteAllLines(Folder+"/uv-"+m.name+".csv",mesh.uv.Select(p=>FormattableString.Invariant($"{p.x:R},{p.y:R}")));
                var tri=mesh.triangles;
                File.WriteAllLines(Folder+"/tri-"+m.name+".csv",Enumerable.Range(0,tri.Length/3).Select(i=>$"{tri[i*3]},{tri[i*3+1]},{tri[i*3+2]}"));
            }
        }
        foreach (var path in new [] { "Black_M_1_Casual", "Black_M_2_Casual" })
        {
            var prefab = Resources.Load<GameObject>("CarRapide/Characters/" + path);
            var instance = UnityEngine.Object.Instantiate(prefab);
            var a = instance.GetComponent<Animator>();
            s.AppendLine($"CHARACTER {path} human={a.isHuman} valid={a.avatar.isValid} scale={instance.transform.localScale}");
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                var t = a.GetBoneTransform(bone);
                if (t != null) s.AppendLine($" {bone} {t.name} position={t.position.ToString("F4")} rotation={t.rotation.eulerAngles.ToString("F2")}");
            }
            foreach (var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                s.AppendLine($" mesh {r.name} verts={r.sharedMesh.vertexCount} bounds={r.bounds} materials={string.Join(",",r.sharedMaterials.Select(x => x.name + ":" + x.shader.name))}");
            UnityEngine.Object.DestroyImmediate(instance);
        }
        File.WriteAllText(Folder + "/geometry.txt", s.ToString());
    }
    static void Capture()
    {
        var c = Camera.main;
        var old = c.targetTexture;
        var rt = RenderTexture.GetTemporary(1440, 900, 24);
        c.targetTexture = rt; c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1440,900,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,1440,900),0,0); tex.Apply();
        File.WriteAllBytes(Folder + "/capture.png",tex.EncodeToPNG());
        c.targetTexture = old; RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(tex);
    }
    static void Snapshot()
    {
        var e=UnityEngine.Object.FindAnyObjectByType<VehicleDriverExperience>();
        var s=new StringBuilder();
        s.AppendLine(e.Clearance.GeometrySummary);
        s.AppendLine($"ready={e.Ready} seated={e.Driver.IsSeated} boarding={e.Driver.IsBoarding} action={e.Driver.CurrentAction} starting={e.Engine.IsStarting} running={e.Engine.IsRunning} canDrive={e.GetComponent<VehicleController>().CanDrive}");
        var controller=e.GetComponent<VehicleController>();
        var body=e.GetComponent<Rigidbody>();
        s.AppendLine($"body velocity={body.linearVelocity} position={body.position} constraints={body.constraints} kinematic={body.isKinematic}");
        foreach(var field in typeof(VehicleController).GetFields(System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance))
            s.AppendLine(field.Name+"="+field.GetValue(controller));
        foreach(var collider in e.GetComponentsInChildren<Collider>()) s.AppendLine("Collider "+collider.name+" "+collider.GetType().Name+" enabled="+collider.enabled);
        foreach(var rig in e.GetComponentsInChildren<CharacterContactRig>())
        {
            s.AppendLine($"{rig.name}: scale={rig.transform.localScale} contactError={rig.MaxContactError} feetHeight={rig.FootHeight}");
            foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                s.AppendLine($" {bone} {e.transform.InverseTransformPoint(rig.Animator.GetBoneTransform(bone).position).ToString("F4")}");
        }
        File.WriteAllText(Folder+"/snapshot.txt",s.ToString());
    }
    [Serializable] public class PoseSample
    {
        public string name;
        public CharacterContactRig.Pose pose;
        public float doorAngle, penetration, contactError;
        public string contact;
        public Vector4 errors;
        public Vector3 head;
        public bool anatomy;
        public Vector3[] bones;
        public Vector3 steeringOffset;
        public bool passenger;
        public float rearDoorAngle;
    }
    [Serializable] public class PoseBatch { public PoseSample[] samples; }
    static void ProbePoses()
    {
        var e=UnityEngine.Object.FindAnyObjectByType<VehicleDriverExperience>();
        if(!EditorApplication.isPlaying || !e.Ready || e.Driver.IsBoarding)
            throw new InvalidOperationException("Pose probes require an idle Play Mode session.");
        var batch=JsonUtility.FromJson<PoseBatch>(File.ReadAllText(Folder+"/poses.json"));
        var rig=batch.samples.Length>0 && batch.samples[0].passenger ? e.Passenger.Rig : e.Driver.Rig;
        var original=rig.CurrentPose; float angle=e.Door.OpenAngle;
        var rear=e.GetComponent<VehicleInteractionPoints>().rearDoorMesh;
        var originalRear=rear.localRotation;
        var rearAxis=rear.parent.InverseTransformDirection(e.transform.up);
        var wheel=e.transform.Find("Car rapide").GetComponentsInChildren<MeshFilter>().First(m=>m.name=="Volant").transform;
        var originalWheel=wheel.position;
        try
        {
            foreach(var sample in batch.samples)
            {
                wheel.position=originalWheel+e.transform.TransformVector(sample.steeringOffset);
                e.Door.SetOpening(sample.doorAngle);
                rear.localRotation=Quaternion.AngleAxis(sample.rearDoorAngle-e.Passenger.DoorAngle,rearAxis)*originalRear;
                rig.SetPose(sample.pose); rig.ApplyPose();
                sample.penetration=e.Clearance.Measure(rig.Animator);
                sample.contact=e.Clearance.WorstContact; sample.contactError=rig.MaxContactError;
                sample.errors=rig.ContactErrors;
                sample.head=e.transform.InverseTransformPoint(rig.Animator.GetBoneTransform(HumanBodyBones.Head).position);
                if(sample.anatomy)
                {
                    sample.bones=new Vector3[23];
                    for(int i=0;i<23;i++)
                    {
                        var bone=rig.Animator.GetBoneTransform((HumanBodyBones)i);
                        if(bone) sample.bones[i]=e.transform.InverseTransformPoint(bone.position);
                    }
                }
            }
            File.WriteAllText(Folder+"/pose-results.json",JsonUtility.ToJson(batch));
        }
        finally {wheel.position=originalWheel; rear.localRotation=originalRear; e.Door.SetOpening(angle); rig.SetPose(original); rig.ApplyPose();}
    }
}
#endif
