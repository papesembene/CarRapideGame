using System.Collections;
using UnityEngine;
using Pose = CarRapide.Vehicle.CharacterContactRig.Pose;

namespace CarRapide.Vehicle
{
    public sealed class DriverController : MonoBehaviour
    {
        VehicleInteractionPoints points;
        VehicleDoorController door;
        VehicleCameraController cameraDirector;
        DriverAnimationController motion;
        public bool IsSeated { get; private set; }
        public bool IsBoarding { get; private set; }
        public string CurrentAction { get; private set; }
        public CharacterContactRig Rig => motion.Rig;

        public void Initialize(VehicleInteractionPoints layout, VehicleDoorController realDoor, VehicleCameraController camera)
        {
            points=layout; door=realDoor; cameraDirector=camera;
            motion=gameObject.AddComponent<DriverAnimationController>(); motion.Initialize(layout.transform);
            var p = Standing(layout.ToLocal(layout.driverOutside),90);
            motion.Rig.SetPose(p); motion.Play("Driver_IdleOutside",0); motion.Rig.ApplyPose();
        }
        public static Pose Standing(Vector3 feet, float yaw)
        {
            var q=Quaternion.Euler(0,yaw,0);
            var p = new Pose {pelvis=feet+Vector3.up*.968f,yaw=yaw,leftFootYaw=yaw,rightFootYaw=yaw,
                leftFoot=feet+q*new Vector3(-.12f,0,.035f),rightFoot=feet+q*new Vector3(.12f,0,-.035f),leftHandWeight=1,rightHandWeight=1};
            RelaxHands(ref p); return p;
        }
        public static void RelaxHands(ref Pose p)
        {
            var q=Quaternion.Euler(0,p.yaw,0);
            p.leftHand=p.pelvis+q*new Vector3(-.24f,-.015f,.06f);
            p.rightHand=p.pelvis+q*new Vector3(.24f,-.015f,.06f);
        }
        public void RequestBoard()
        {
            if (IsBoarding || IsSeated || points.GetComponent<VehicleDriverExperience>().Engine.PassengerBusy) return;
            IsBoarding=true; StartCoroutine(Board());
        }
        void Phase(string clip,string caption) { CurrentAction=caption; motion.Play(clip); }

        IEnumerator Board()
        {
            var p=Rig.CurrentPose;
            Phase("Driver_WalkToDoor","Le chauffeur rejoint la porte…");
            var left=p; left.pelvis+=new Vector3(.13f,0,.05f);
            left.leftFoot+=new Vector3(.26f,0,.10f); RelaxHands(ref left);
            p=left; p.pelvis+=new Vector3(.13f,0,.05f);
            p.rightFoot+=new Vector3(.26f,0,.10f); RelaxHands(ref p);
            yield return motion.Sequence(new DriverAnimationController.Step(left,.52f,.065f),
                new DriverAnimationController.Step(p,.52f,0,.065f));
            cameraDirector.DoorView();
            Phase("Driver_GrabHandle","Main sur la poignée…");
            p.leftHand=points.ToLocal(points.doorHandle); p.lean=14;
            yield return motion.Transition(p,.65f);
            Phase("Driver_OpenDoor","Ouverture de la porte…");
            var hold=p;
            for(float t=0;t<1.15f;t+=Time.deltaTime)
            {
                float u=DriverAnimationController.Smooth(t/1.15f); door.SetOpening(76*u);
                hold=p;
                hold.lean=Mathf.Lerp(p.lean,8,u);
                float retreat=Mathf.Sin(Mathf.PI*u);
                hold.pelvis=p.pelvis+new Vector3(-.10f,-.035f,.13f)*u+new Vector3(-.16f,-.03f,-.08f)*retreat;
                RelaxHands(ref hold);
                // Release after the initial pull, then let the door finish its swing.
                // Keeping the exterior handle beyond this angle traps the forearm behind the panel.
                float release=DriverAnimationController.Smooth((u-.2f)/.3f);
                hold.leftHand=Vector3.Lerp(points.ToLocal(points.doorHandle),hold.pelvis+new Vector3(-.1f,.1f,0),release);
                Rig.SetPose(hold); yield return null;
            }
            door.SetOpening(76); p=Rig.CurrentPose;
            // Move alongside the OPEN doorway first. The front wheel arch is not a step.
            p.rightFoot=new Vector3(-1.52f,-.03634f,1.86f);
            p.pelvis=new Vector3(-1.56f,.86f,1.57f); p.yaw=65; p.rightFootYaw=35; p.lean=8;
            p.leftHand=LeftRest(p); p.leftGrip=0; p.leftElbowBack=0;
            RelaxRight(ref p);
            yield return motion.Transition(p,.65f,0,.10f);
            p.leftFoot=new Vector3(-1.53f,-.03634f,1.74f); p.leftFootYaw=35;
            p.pelvis=new Vector3(-1.52f,.86f,1.68f); p.yaw=35; p.leftKneeYawOffset=40;
            // The right wrist approaches the left rim from outside the cabin.
            p.rightHand=points.ToLocal(points.wheelLeftHand)+Vector3.back*.09f; p.rightGrip=1;
            p.leftHand=LeftRest(p);
            yield return motion.Transition(p,.45f,.07f);
            cameraDirector.BoardingView();
            Phase("Driver_StepUp","Appui sur le marchepied…");
            p.leftFoot=points.ToLocal(points.driverStep); p.leftFootYaw=35;
            p.pelvis=new Vector3(-1.30f,.85f,1.63f); p.yaw=35; p.lean=0;
            p.leftHand=LeftRest(p);
            yield return motion.Transition(p,.75f,.16f);
            Phase("Driver_EnterCabin","Entrée sous le cadre de porte…");
            p.pelvis=new Vector3(-.99f,1.10f,1.54f); p.yaw=-10; p.lean=16; p.leftKneeYawOffset=0;
            p.rightFoot=new Vector3(-.80f,.336f,1.98f); p.rightFootYaw=0;
            // Keep the supporting hand in front of the shoulder, never behind the back.
            p.leftHand=new Vector3(-1.03f,1.26f,1.88f); p.leftGrip=.35f;
            yield return motion.Transition(p,.85f,0,.06f);
            Phase("Driver_TurnToSeat","Pivot au-dessus du siège…");
            p.leftFoot=points.ToLocal(points.driverLeftFoot); p.leftFootYaw=0;
            p.pelvis=points.ToLocal(points.driverSeat); p.yaw=0; p.lean=0;
            p.leftHand=points.ToLocal(points.wheelLeftHand); p.leftGrip=1;
            p.rightHand=points.ToLocal(points.wheelRightHand);
            p.rightFoot=points.ToLocal(points.driverRightFoot); p.rightFootYaw=0;
            // Transfer weight to the cushion and unfold the knees around the steering column.
            // One continuous curve avoids stopping at each intermediate leg position.
            yield return motion.Transition(p,1.6f,.025f,.025f,shape:(value,t)=> {
                float arc=Mathf.Sin(Mathf.PI*t); arc*=arc;
                value.lean=16*(1-DriverAnimationController.Smooth(t*1.5f));
                value.pelvis.y-=.01f*arc;
                value.leftKneeYawOffset=-25*arc; value.rightKneeYawOffset=30*arc;
                return value;
            });
            cameraDirector.SeatedView();
            Phase("Driver_HandsOnWheel","Fermeture de la porte…");
            var seated=p;
            p.pelvis=new Vector3(-.81f,1.08f,1.49f); p.yaw=25; p.lean=0; p.roll=10; p.leftGrip=0;
            p.leftHand=points.ToLocal(points.doorPull);
            yield return motion.Transition(p,.85f);
            var pull=p;
            var closedReach=seated; closedReach.lean=0; closedReach.roll=0; closedReach.leftGrip=0;
            for(float t=0;door.OpenAngle>.001f;)
            {
                float next=Mathf.Min(1.25f,t+Time.deltaTime);
                float u=DriverAnimationController.Smooth(next/1.25f);
                p=Pose.Blend(pull,closedReach,u);
                p.leftHand=points.ToLocal(points.doorPull); Rig.SetPose(p);
                if(door.TryCloseTo(76*(1-u),Rig,points.doorPull)) t=next;
                yield return null;
            }
            p=seated;
            yield return motion.Transition(p,.7f);
            motion.Play("Driver_DrivingIdle"); CurrentAction="Au volant"; IsSeated=true; IsBoarding=false;
        }
        static Vector3 LeftRest(Pose p) => p.pelvis+Quaternion.Euler(0,p.yaw,0)*new Vector3(-.22f,-.015f,.03f);
        static void RelaxRight(ref Pose p)
        {
            p.rightHand=p.pelvis+Quaternion.Euler(0,p.yaw,0)*new Vector3(.24f,.12f,.15f);
            p.rightHandWeight=1;
        }
        public IEnumerator TurnIgnition(float duration, System.Action crank)
        {
            var seated=Rig.CurrentPose;
            var p=seated;
            p.lean=0; p.rightGrip=0;
            motion.Play("Driver_StartEngine"); p.rightHand=points.ToLocal(points.ignition);
            yield return motion.Transition(p,.55f);
            crank();
            yield return new WaitForSeconds(duration);
            p=seated;
            yield return motion.Transition(p,.4f);
            motion.Play("Driver_DrivingIdle");
        }
    }
}
