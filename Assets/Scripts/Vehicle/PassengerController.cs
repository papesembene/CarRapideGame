using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pose = CarRapide.Vehicle.CharacterContactRig.Pose;

namespace CarRapide.Vehicle
{
    /// <summary>Player-controlled boarding and alighting with a persistent seated state.</summary>
    public sealed class PassengerController : MonoBehaviour
    {
        // Upper aisle surface measured from the original Carosserie triangles.
        const float CabinFloor = .336f;
        VehicleDriverExperience experience;
        VehicleInteractionPoints points;
        DriverAnimationController motion;
        Transform walkingFrame;
        Quaternion rearClosed;
        Vector3 rearAxis;
        Vector3 innerGripLocal;
        readonly List<Pose> route=new List<Pose>();
        PedestrianPhysics pedestrian;
        public PedestrianPhysics PhysicsBody => pedestrian;
        public string Status { get; private set; } = "En attente";
        public string Feedback { get; private set; }
        float feedbackUntil;
        public bool HasFeedback => Time.time<feedbackUntil;
        public bool IsBusy { get; private set; }
        public bool IsSeated { get; private set; }
        public float DoorAngle { get; private set; }
        public CharacterContactRig Rig => motion ? motion.Rig : null;

        public void Initialize(VehicleDriverExperience owner, VehicleInteractionPoints layout)
        {
            experience = owner;
            points = layout;
            rearClosed = points.rearDoorMesh.localRotation;
            rearAxis = points.rearDoorMesh.parent.InverseTransformDirection(layout.transform.up);
            innerGripLocal=points.rearDoorMesh.InverseTransformPoint(layout.transform.TransformPoint(new Vector3(.28f,1.19f,-2.25f)));
            points.passengerDoorGrip.SetParent(points.rearDoorMesh, true);
            points.rearWindowMesh.SetParent(points.rearDoorMesh, true);
            var character = owner.CreateCharacter("Passager", "Black_M_2_Casual", 1.70f);
            if (!character) return;
            walkingFrame = new GameObject("PassengerWaitingArea").transform;
            walkingFrame.SetPositionAndRotation(layout.transform.position, layout.transform.rotation);
            character.transform.SetParent(walkingFrame, false);
            motion = character.AddComponent<DriverAnimationController>();
            motion.Initialize(walkingFrame);
            var p = DriverController.Standing(points.ToLocal(points.passengerOutside), 0);
            p.pelvis.y=points.ToLocal(points.passengerOutside).y+Rig.StandingHeight-.004f;
            DriverController.RelaxHands(ref p);
            Rig.SetPose(p);
            motion.Play("Client_Idle", 0);
            Rig.ApplyPose();
            pedestrian=character.AddComponent<PedestrianPhysics>(); pedestrian.Initialize(Rig,this,owner.GetComponent<Rigidbody>());
        }

        public void RequestBoard()
        {
            if(!CanInteract() || IsSeated) return;
            if(Vector3.Distance(Rig.Animator.GetBoneTransform(HumanBodyBones.Hips).position,points.passengerOutside.position)>4)
            {Notify("Approcher le car du passager");return;}
            // Rebase the coordinate frame without moving the waiting person.
            var p=Rig.CurrentPose;
            var old=walkingFrame.localToWorldMatrix;
            float oldYaw=walkingFrame.eulerAngles.y;
            walkingFrame.SetPositionAndRotation(points.transform.position,points.transform.rotation);
            Vector3 Convert(Vector3 v)=>walkingFrame.InverseTransformPoint(old.MultiplyPoint3x4(v));
            p.pelvis=Convert(p.pelvis);p.leftFoot=Convert(p.leftFoot);p.rightFoot=Convert(p.rightFoot);p.leftHand=Convert(p.leftHand);p.rightHand=Convert(p.rightHand);
            float delta=oldYaw-walkingFrame.eulerAngles.y;p.yaw+=delta;p.leftFootYaw+=delta;p.rightFootYaw+=delta;Rig.SetPose(p);
            IsBusy=true;experience.Engine.PassengerBusy=true;pedestrian.SetInteracting(true);Status="Le passager monte";
            StartCoroutine(Board());
        }
        public void RequestAlight()
        {
            if(!CanInteract() || !IsSeated) return;
            IsBusy=true;experience.Engine.PassengerBusy=true;Status="Le passager descend";
            StartCoroutine(Alight());
        }
        bool CanInteract()
        {
            if(!motion || IsBusy || pedestrian.IsFallen || experience.Driver.IsBoarding || experience.Engine.IsStarting) return false;
            if(experience.GetComponent<VehicleController>().SpeedKmh>.5f) {Notify("Arrêter le car pour le passager");return false;}
            return true;
        }
        void Notify(string message) {Feedback=message;feedbackUntil=Time.time+3;}
        public void OnVehicleImpact()
        {
            StopAllCoroutines();IsBusy=false;IsSeated=false;experience.Engine.PassengerBusy=false;Status="Passager renversé";
        }

        Vector3 DoorGrip => walkingFrame.InverseTransformPoint(points.passengerDoorGrip.position);
        Vector3 InnerGrip => walkingFrame.InverseTransformPoint(points.rearDoorMesh.TransformPoint(innerGripLocal));
        void Relax(ref Pose p, bool holdDoor = false)
        {
            DriverController.RelaxHands(ref p);
            if (holdDoor) p.rightHand = InnerGrip;
        }

        IEnumerator Board()
        {
            experience.CameraDirector.PassengerView();
            motion.Play("Client_Walk");
            if (Mathf.Abs(Mathf.DeltaAngle(Rig.CurrentPose.yaw, 0)) > 1) yield return Turn(0);
            var approach=points.ToLocal(points.passengerOutside);
            // Stay behind the rear bumper while approaching the entry corridor.
            var feet=(Rig.CurrentPose.leftFoot+Rig.CurrentPose.rightFoot)*.5f;
            if(feet.z>approach.z+1.02f)
                yield return motion.WalkTo(new Vector3(feet.x,feet.y,approach.z+1.02f),.65f);
            yield return motion.WalkTo(new Vector3(approach.x,Rig.CurrentPose.leftFoot.y,approach.z+1.02f),.65f);
            if(Mathf.Abs(Mathf.DeltaAngle(Rig.CurrentPose.yaw,0))>1) yield return Turn(0);


            var p = Rig.CurrentPose;
            p.lean = 27;
            Relax(ref p); p.rightHand=DoorGrip;
            yield return motion.Transition(p, .75f);
            yield return OpenRearDoor();
            // Approach the aisle only after the door has cleared the waiting position.
            p=Rig.CurrentPose; p.lean=0; Relax(ref p);
            yield return motion.Transition(p,.4f);
            var center=p;
            center.pelvis.x+=.7f; center.leftFoot.x+=.7f; center.rightFoot.x+=.7f; center.lean=20;
            center.pelvis.z+=.15f; center.leftFoot.z+=.15f; center.rightFoot.z+=.15f;
            for(float t=0;t<1.8f;t+=Time.deltaTime)
            { Rig.SetPose(SideStepPose(center,1-t/1.8f)); yield return null; }
            Relax(ref center); Rig.SetPose(center);
            p=center; p.rightElbowBack=-.75f; Relax(ref p,true);
            yield return motion.Transition(p,.6f);
            var ground = Rig.CurrentPose;
            route.Clear(); route.Add(ground);

            motion.Play("Client_StepUp");
            p = ground;
            p.leftFoot = points.ToLocal(points.passengerStep);
            p.pelvis = new Vector3(0, .94f, -2.79f);
            Relax(ref p, true);
            yield return motion.Transition(p, 1.0f, .17f); route.Add(p);

            p.pelvis = new Vector3(0, 1.0f, -2.61f);
            // Keep the toes behind the platform edge before lowering this foot on exit.
            p.rightFoot += new Vector3(0, .20f, .05f);
            p.lean = 25;
            Relax(ref p, true);
            yield return motion.Transition(p, .85f); route.Add(p);

            p.rightFoot = new Vector3(.12f, CabinFloor, -2.05f);
            p.pelvis = new Vector3(0, 1.09f, -2.31f);
            p.lean = 20;
            Relax(ref p);
            yield return motion.Transition(p, 1.05f, 0, .16f); route.Add(p);

            motion.Play("Client_Enter");
            p.leftFoot = new Vector3(-.13f, CabinFloor, -1.83f);
            p.pelvis = new Vector3(0, 1.13f, -2.07f);
            Relax(ref p);
            yield return motion.Transition(p, .9f, .12f); route.Add(p);

            p.rightFoot = new Vector3(.12f, CabinFloor, -1.39f);
            p.pelvis = new Vector3(0, 1.21f, -1.69f);
            p.lean = 5;
            Relax(ref p);
            yield return motion.Transition(p, .9f, 0, .10f); route.Add(p);

            p.leftFoot = new Vector3(.16f, CabinFloor, -1.52f);
            p.leftFootYaw = -60; p.yaw = -40;
            p.pelvis = new Vector3(.12f, 1.20f, -1.40f);
            Relax(ref p);
            yield return motion.Transition(p, .85f, .09f); route.Add(p);

            p.rightFoot = new Vector3(.18f, CabinFloor, -1.21f);
            p.leftFootYaw = p.rightFootYaw = p.yaw = -90;
            p.pelvis = new Vector3(.36f, 1.20f, -1.36f);
            Relax(ref p);
            yield return motion.Transition(p, .85f, 0, .09f); route.Add(p);

            motion.Play("Client_Sit");
            p.pelvis = points.ToLocal(points.passengerSeat);
            p.lean = 2;
            var facing = Quaternion.Euler(0, p.yaw, 0);
            p.leftHand = p.pelvis + facing * new Vector3(-.18f, .08f, .20f);
            p.rightHand = p.pelvis + facing * new Vector3(.18f, .08f, .20f);
            yield return motion.Transition(p, 1.2f);
            IsSeated = true;
            motion.Play("Client_Idle");
            walkingFrame.SetParent(points.transform,true);
            IsBusy=false;experience.Engine.PassengerBusy=false;Status="Passager à bord";
            RestoreCamera();
        }

        IEnumerator Alight()
        {
            experience.CameraDirector.PassengerView();
            var p=Rig.CurrentPose;
            IsSeated = false;
            motion.Play("Client_Walk");
            // Stand, turn in the aisle, then descend facing the vehicle with a hand on the door.
            for (int i = route.Count - 1; i >= 0; i--)
            {
                var target = route[i];
                var current = Rig.CurrentPose;
                bool left = (target.leftFoot - current.leftFoot).sqrMagnitude > .001f;
                bool right = (target.rightFoot - current.rightFoot).sqrMagnitude > .001f;
                yield return motion.Transition(target, .9f, left ? .12f : 0, right ? .12f : 0);
            }
            yield return CloseRearDoor();
            p = Rig.CurrentPose; p.lean = 0; Relax(ref p);
            yield return motion.Transition(p, .6f);
            yield return Turn(180);
            var feet=(Rig.CurrentPose.leftFoot+Rig.CurrentPose.rightFoot)*.5f;
            yield return motion.WalkTo(feet+Vector3.back*1.02f,.65f);
            motion.Play("Client_Idle");
            IsBusy = false;
            experience.Engine.PassengerBusy = false;
            walkingFrame.SetParent(null,true);pedestrian.SetInteracting(false);Status="En attente";
            RestoreCamera();
        }
        void RestoreCamera()
        {
            if(experience.Engine.IsRunning) experience.CameraDirector.DrivingView();
            else if (experience.Driver.IsSeated) experience.CameraDirector.SeatedView();
            else experience.CameraDirector.ExteriorView();
        }

        IEnumerator Turn(float yaw)
        {
            var p = Rig.CurrentPose;
            float halfway = Mathf.LerpAngle(p.yaw, yaw, .5f);
            var center = (p.leftFoot + p.rightFoot) * .5f;
            var facing = Quaternion.Euler(0, yaw, 0);
            p.yaw = halfway; p.leftFootYaw = halfway;
            p.leftFoot = center + facing * new Vector3(-.12f, 0, .035f);
            Relax(ref p);
            yield return motion.Transition(p, .7f, .05f);
            p.yaw = p.leftFootYaw = p.rightFootYaw = yaw;
            p.rightFoot = center + facing * new Vector3(.12f, 0, -.035f);
            Relax(ref p);
            yield return motion.Transition(p, .7f, 0, .05f);
        }

        void SetRearDoor(float angle)
        {
            DoorAngle=angle;
            points.rearDoorMesh.localRotation=Quaternion.AngleAxis(angle,rearAxis)*rearClosed;
        }

        Pose SideStepPose(Pose center,float u)
        {
            u=Mathf.Clamp01(u);
            var p=center;
            float travel=DriverAnimationController.Smooth(u/.45f)+DriverAnimationController.Smooth((u-.45f)/.45f);
            p.pelvis+=new Vector3(-.35f,0,-.075f)*travel;
            p.pelvis.y-=.12f*Mathf.Sin(Mathf.PI*Mathf.Min(1,u/.9f));
            p.lean=20*(1-DriverAnimationController.Smooth(u/.4f));
            var offset=new Vector3(-.7f,0,-.15f);
            p.leftFoot=DriverAnimationController.Swing(center.leftFoot,center.leftFoot+offset,Mathf.Clamp01(u/.45f),.07f);
            p.rightFoot=DriverAnimationController.Swing(center.rightFoot,center.rightFoot+offset,Mathf.Clamp01((u-.45f)/.45f),.07f);
            Relax(ref p); return p;
        }

        IEnumerator OpenRearDoor()
        {
            var start=Rig.CurrentPose;
            for(float t=0;t<1.4f;t+=Time.deltaTime)
            {
                float u=DriverAnimationController.Smooth(t/1.4f);
                SetRearDoor(-105*u);
                var p=start; Relax(ref p);
                p.rightHand=Vector3.Lerp(DoorGrip,p.pelvis+new Vector3(.24f,0,.12f),DriverAnimationController.Smooth((u-.12f)/.18f));
                Rig.SetPose(p);
                yield return null;
            }
            SetRearDoor(-105);
        }

        IEnumerator CloseRearDoor()
        {
            var center=Rig.CurrentPose;
            // Push the inside face, release, then step clear before the door latches.
            for(float t=0;t<2.1f;t+=Time.deltaTime)
            {
                float u=Mathf.Clamp01(t/2.1f);
                SetRearDoor(-105+10*DriverAnimationController.Smooth(u/.2f)+95*DriverAnimationController.Smooth((u-.65f)/.35f));
                var p=SideStepPose(center,u);
                p.rightElbowBack=center.rightElbowBack*(1-DriverAnimationController.Smooth(u/.4f));
                p.rightHand=Vector3.Lerp(InnerGrip,p.pelvis+new Vector3(.24f,0,.12f),DriverAnimationController.Smooth(u/.25f));
                Rig.SetPose(p); yield return null;
            }
            SetRearDoor(0);
            var end=SideStepPose(center,1); end.rightElbowBack=0; Rig.SetPose(end);
        }

        void OnDestroy() { if (walkingFrame) Destroy(walkingFrame.gameObject); }
    }
}
