#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using CarRapide.Vehicle;

namespace CarRapide.EditorTools
{
    // Integration review through the same public commands and real PhysX contacts as gameplay.
    public static class VehicleRealismReview
    {
        const string Folder="Library/VehicleReview/Realism";
        static VehicleDriverExperience experience;
        static Keyboard keyboard;
        static KeyboardState input;
        static int stage,frame;
        static float since,start,nextCapture,maxSeatError;
        static bool active,background,refusedExit;
        static Vector3 origin,impactOrigin;
        static Rigidbody body;
        static VehicleController controller;
        [InitializeOnLoadMethod]
        static void RegisterCleanup() {AssemblyReloadEvents.beforeAssemblyReload+=End;}
        [MenuItem("Car Rapide/Vehicle/Review passenger journey and physical impact")]
        public static void Begin()
        {
            experience=UnityEngine.Object.FindAnyObjectByType<VehicleDriverExperience>();
            if(active || !EditorApplication.isPlaying || !experience || !experience.Ready || experience.Driver.IsSeated || experience.Passenger.IsBusy)
                throw new InvalidOperationException("Realism review requires a fresh Play Mode session.");
            var sourceModel=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Vehicles/CarRapide/Car rapide.fbx");
            var sourceFilters=sourceModel.GetComponentsInChildren<MeshFilter>();
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/checks.txt","Unity "+Application.unityVersion+"\n");
            background=Application.runInBackground;Application.runInBackground=true;
            body=experience.GetComponent<Rigidbody>();controller=experience.GetComponent<VehicleController>();
            keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.onBeforeUpdate+=Input;
            stage=frame=0;start=since=Time.time;nextCapture=0;maxSeatError=0;refusedExit=false;active=true;
            Check(experience.Passenger.PhysicsBody.IsSolid,"Waiting pedestrian has solid body colliders");
            foreach(var filter in experience.transform.Find("Car rapide").GetComponentsInChildren<MeshFilter>())
            {
                if(filter.name!="Porte_avant_gauche" && filter.name!="Porte_avant_droit")continue;
                // Door pivots reparent the meshes in Play Mode, removing their prefab connection.
                var source=Array.Find(sourceFilters,item=>item.name==filter.name).sharedMesh;
                var materials=filter.GetComponent<Renderer>().sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                    Check(materials[i].name=="CabinGlass" || materials[i].name=="Parebrise" ? filter.sharedMesh.GetTriangles(i).Length==0 : filter.sharedMesh.GetTriangles(i).Length==source.GetTriangles(i).Length,
                        filter.name+" / "+materials[i].name+": open glass and preserved metal");
            }
            experience.Passenger.RequestBoard();experience.Passenger.RequestAlight();experience.Driver.RequestBoard();experience.Engine.RequestStart();
            Check(experience.Passenger.IsBusy && !experience.Driver.IsBoarding && !experience.Engine.IsStarting,"Repeated/conflicting commands do not interrupt boarding");
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=StateChanged;
        }
        static void Input()
        {
            if(active && InputState.currentUpdateType==InputUpdateType.Dynamic) {keyboard.MakeCurrent();InputState.Change(keyboard,input);}
        }
        static void Advance(int value) {stage=value;since=Time.time;}
        static void Tick()
        {
            if(!active || !EditorApplication.isPlaying) {End();return;}
            var passenger=experience.Passenger;
            float elapsed=Time.time-since;
            if(Time.time-start>135) {Check(false,"Review timed out at stage "+stage+" / "+passenger.Status);End();return;}
            if(Time.time>=nextCapture) {nextCapture=Time.time+1;Capture();}
            switch(stage)
            {
                case 0:
                    if(passenger.IsSeated && !passenger.IsBusy) Advance(1);
                    break;
                case 1:
                    if(elapsed<3) break;
                    Check(passenger.IsSeated && !passenger.IsBusy,"Passenger stays seated until an explicit alight request");
                    Check(!passenger.PhysicsBody.IsSolid,"Body collisions disabled while seated");
                    experience.Driver.RequestBoard();Advance(2);break;
                case 2:
                    if(experience.Driver.IsSeated) {experience.Engine.RequestStart();Advance(3);}break;
                case 3:
                    if(experience.Engine.IsRunning) {origin=body.position;input=new KeyboardState(Key.W,Key.D);Advance(4);}break;
                case 4:
                    var hips=passenger.Rig.Animator.GetBoneTransform(HumanBodyBones.Hips);
                    maxSeatError=Mathf.Max(maxSeatError,Vector3.Distance(hips.position,experience.GetComponent<VehicleInteractionPoints>().passengerSeat.position));
                    if(controller.SpeedKmh>3) {passenger.RequestAlight();refusedExit|=passenger.IsSeated&&!passenger.IsBusy;}
                    if(elapsed<2) break;
                    Check(Vector3.Distance(origin,body.position)>2 && maxSeatError<.06f,"Seated passenger follows the moving and turning car (error="+maxSeatError.ToString("F3")+")");
                    Check(refusedExit,"Alighting is refused while driving");
                    input=new KeyboardState(Key.Space);Advance(5);break;
                case 5:
                    if(controller.SpeedKmh>.1f) break;
                    input=new KeyboardState();passenger.RequestAlight();Advance(6);break;
                case 6:
                    if(passenger.IsBusy) {CheckOnceLocked();break;}
                    Check(!passenger.IsSeated && passenger.PhysicsBody.IsSolid,"Passenger alights at the new stop and restores physical collisions");
                    Check(!passenger.PhysicsBody.IsFallen,"Passenger does not collide with the car while entering or seated");
                    passenger.RequestBoard();Advance(7);break;
                case 7:
                    if(passenger.IsBusy) break;
                    Check(passenger.IsSeated,"Passenger can board again after the car changes heading");
                    passenger.RequestAlight();Advance(8);break;
                case 8:
                    if(passenger.IsBusy) break;
                    Check(!passenger.IsSeated && passenger.PhysicsBody.IsSolid,"Second alighting completes");
                    controller.enabled=false;experience.Engine.enabled=false;
                    impactOrigin=passenger.Rig.Animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    var position=impactOrigin+experience.transform.forward*2.97f;position.y=body.position.y;
                    body.position=position;body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
                    Camera.main.GetComponent<VehicleCameraFollow>().SnapView(new Vector3(-3,2,-6.9f),new Vector3(-.4f,.4f,-3.2f));Advance(9);break;
                case 9:
                    body.linearVelocity=-experience.transform.forward*.3f;
                    if(elapsed<2) break;
                    Check(!passenger.PhysicsBody.IsFallen,"Slow contact remains solid without throwing the pedestrian");
                    Check(Vector3.Dot(body.position-impactOrigin,experience.transform.forward)>2.55f,"Slow contact does not pass through the standing pedestrian");
                    Advance(10);break;
                case 10:
                    body.linearVelocity=-experience.transform.forward*4;
                    if(!passenger.PhysicsBody.IsFallen && elapsed<3) break;
                    Check(passenger.PhysicsBody.IsFallen,"Real vehicle collision releases the ragdoll at 14.4 km/h");
                    body.linearVelocity=Vector3.zero;body.isKinematic=true;Advance(11);break;
                case 11:
                    if(elapsed<3) break;
                    var fallen=passenger.Rig.Animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    Check(fallen.y<impactOrigin.y-.3f && fallen.y>-.1f,"Struck pedestrian falls onto the ground without passing through it");
                    Check(!passenger.Rig.enabled && !passenger.Rig.Animator.enabled,"Animation stops overriding the physical fall");
                    passenger.RequestBoard();Check(!passenger.IsBusy,"A fallen passenger cannot start a boarding animation");
                    Capture();End();break;
            }
        }
        static bool checkedLock;
        static void CheckOnceLocked()
        {
            if(checkedLock)return;checkedLock=true;
            Check(!controller.CanDrive,"Driving locks while passenger alights with engine running");
        }
        static void Check(bool passed,string message) => File.AppendAllText(Folder+"/checks.txt",(passed?"PASS ":"FAIL ")+message+"\n");
        static void Capture()
        {
            var camera=Camera.main;var previous=camera.targetTexture;var activeTarget=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Folder+"/stage-"+stage+"-"+(frame++).ToString("D3")+".png",texture.EncodeToPNG());}
            finally {camera.targetTexture=previous;RenderTexture.active=activeTarget;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(texture);}
        }
        static void StateChanged(PlayModeStateChange state) {if(state==PlayModeStateChange.ExitingPlayMode)End();}
        static void End()
        {
            if(!active)return;active=false;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=StateChanged;
            InputSystem.onBeforeUpdate-=Input;if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            keyboard=null;input=new KeyboardState();checkedLock=false;Application.runInBackground=background;
            // The collision review ends in a test-only frozen state; exit Play Mode to reset it.
        }
    }
}
#endif
