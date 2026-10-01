using System.Collections.Generic;
using UnityEngine;

namespace CarRapide.Vehicle
{
    /// <summary>Solid pedestrian at rest; a vehicle impact releases a jointed, non-graphic ragdoll.</summary>
    public sealed class PedestrianPhysics : MonoBehaviour
    {
        readonly List<Rigidbody> bodies=new List<Rigidbody>();
        readonly List<Collider> colliders=new List<Collider>();
        CharacterContactRig rig;
        PassengerController passenger;
        Rigidbody vehicle;
        bool interacting;
        public bool IsFallen { get; private set; }
        public bool IsSolid => !interacting && colliders.Count>0 && colliders[0].enabled;
        public void Initialize(CharacterContactRig contactRig,PassengerController owner,Rigidbody car)
        {
            rig=contactRig; passenger=owner; vehicle=car;
            var a=rig.Animator;
            var hips=Body(HumanBodyBones.Hips,HumanBodyBones.Spine,.13f,12);
            var chest=Body(HumanBodyBones.Chest,HumanBodyBones.Neck,.16f,20);
            var head=Body(HumanBodyBones.Head,HumanBodyBones.LastBone,.115f,5);
            Connect(chest,hips); Connect(head,chest);
            foreach(bool left in new[]{true,false})
            {
                var thigh=Body(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg,left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg,.075f,8);
                var shin=Body(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg,left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot,.058f,4);
                var arm=Body(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm,left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,.055f,3);
                var forearm=Body(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm,left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand,.043f,2);
                Connect(thigh,hips);Connect(shin,thigh);Connect(arm,chest);Connect(forearm,arm);
            }
            for(int i=0;i<colliders.Count;i++) for(int j=i+1;j<colliders.Count;j++) Physics.IgnoreCollision(colliders[i],colliders[j]);

            Rigidbody Body(HumanBodyBones bone,HumanBodyBones end,float radius,float mass)
            {
                var t=a.GetBoneTransform(bone); var body=t.gameObject.AddComponent<Rigidbody>();
                body.mass=mass;body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                var collider=t.gameObject.AddComponent<CapsuleCollider>();
                Vector3 delta=end==HumanBodyBones.LastBone?Vector3.zero:t.InverseTransformPoint(a.GetBoneTransform(end).position);
                int axis=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?0:1; if(Mathf.Abs(delta.z)>Mathf.Abs(delta[axis]))axis=2;
                collider.direction=axis;collider.center=delta*.5f;collider.radius=radius/t.lossyScale.x;
                collider.height=Mathf.Max(2*collider.radius,delta.magnitude+collider.radius);
                t.gameObject.AddComponent<PedestrianContact>().owner=this;
                bodies.Add(body);colliders.Add(collider);return body;
            }
        }
        static void Connect(Rigidbody body,Rigidbody parent)
        {
            var joint=body.gameObject.AddComponent<CharacterJoint>();joint.connectedBody=parent;
            joint.lowTwistLimit=new SoftJointLimit {limit=-25};joint.highTwistLimit=new SoftJointLimit {limit=25};
            joint.swing1Limit=new SoftJointLimit {limit=55};joint.swing2Limit=new SoftJointLimit {limit=35};
            joint.enableProjection=true;joint.enableCollision=false;
        }
        public void SetInteracting(bool value)
        {
            interacting=value;
            foreach(var collider in colliders) collider.enabled=!value;
        }
        public void Contact(Collision collision)
        {
            if(interacting || IsFallen || collision.rigidbody!=vehicle) return;
            // Physical contacts stop penetration even below the fall threshold.
            float speed=vehicle.linearVelocity.magnitude;
            if(speed<.65f && collision.relativeVelocity.magnitude<.65f) return;
            Fall(vehicle.linearVelocity);
        }
        public void Fall(Vector3 velocity)
        {
            if(IsFallen || interacting) return;
            IsFallen=true; passenger.OnVehicleImpact();
            rig.enabled=false;rig.Animator.enabled=false;
            foreach(var body in bodies)
            {
                body.isKinematic=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity=Vector3.ClampMagnitude(velocity*.55f,9)+Vector3.up*.5f;
            }
        }
    }
    public sealed class PedestrianContact : MonoBehaviour
    {
        public PedestrianPhysics owner;
        void OnCollisionEnter(Collision collision) {if(owner)owner.Contact(collision);}
        void OnCollisionStay(Collision collision) {if(owner)owner.Contact(collision);}
    }
}
