using System.Collections;
using UnityEngine;

namespace CarRapide.Vehicle
{
    public sealed class DriverAnimationController : MonoBehaviour
    {
        public struct Step
        {
            public CharacterContactRig.Pose pose;
            public float seconds, leftLift, rightLift;
            public Step(CharacterContactRig.Pose pose, float seconds, float leftLift = 0, float rightLift = 0)
            { this.pose=pose; this.seconds=seconds; this.leftLift=leftLift; this.rightLift=rightLift; }
        }
        public CharacterContactRig Rig { get; private set; }
        public string ClipName { get; private set; }
        public float ClipStartedAt { get; private set; }
        public void Initialize(Transform frame)
        {
            Rig = gameObject.AddComponent<CharacterContactRig>();
            Rig.Initialize(frame);
        }
        public void Play(string clip, float blend = 0.22f)
        {
            ClipName=clip; ClipStartedAt=Time.time;
            Rig.Animator.CrossFadeInFixedTime(clip,blend);
        }
        public IEnumerator Transition(CharacterContactRig.Pose target, float duration, float leftLift = 0, float rightLift = 0,
            System.Action<float> onProgress = null, System.Func<CharacterContactRig.Pose,float,CharacterContactRig.Pose> shape = null)
        {
            var start = Rig.CurrentPose;
            for (float elapsed=0; elapsed<duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed/duration);
                float u = t*t*t*(t*(6*t-15)+10);
                var p = CharacterContactRig.Pose.Blend(start,target,u);
                // A planted foot has no lift and identical endpoints throughout a support phase.
                p.leftFoot=Swing(start.leftFoot,target.leftFoot,t,leftLift);
                p.rightFoot=Swing(start.rightFoot,target.rightFoot,t,rightLift);
                if(shape!=null) p=shape(p,t);
                Rig.SetPose(p);
                onProgress?.Invoke(u);
                yield return null;
            }
            Rig.SetPose(target); onProgress?.Invoke(1);
        }

        // The pelvis keeps a shared velocity at intermediate contacts; planted feet stay fixed.
        public IEnumerator Sequence(params Step[] steps)
        {
            var initial=Rig.CurrentPose;
            for (int i=0;i<steps.Length;i++)
            {
                var a=i==0?initial:steps[i-1].pose;
                var b=steps[i].pose;
                float duration=steps[i].seconds;
                Vector3 incoming=i==0?Vector3.zero:Tangent(i-1);
                Vector3 outgoing=i==steps.Length-1?Vector3.zero:Tangent(i);
                for(float elapsed=0;elapsed<duration;elapsed+=Time.deltaTime)
                {
                    float t=Mathf.Clamp01(elapsed/duration), u=Smooth(t);
                    var p=CharacterContactRig.Pose.Blend(a,b,u);
                    p.pelvis=(2*t*t*t-3*t*t+1)*a.pelvis+(t*t*t-2*t*t+t)*duration*incoming
                        +(-2*t*t*t+3*t*t)*b.pelvis+(t*t*t-t*t)*duration*outgoing;
                    p.leftFoot=Swing(a.leftFoot,b.leftFoot,t,steps[i].leftLift);
                    p.rightFoot=Swing(a.rightFoot,b.rightFoot,t,steps[i].rightLift);
                    Rig.SetPose(p); yield return null;
                }
                Rig.SetPose(b);
            }
            Vector3 Tangent(int knot)
            {
                Vector3 before=knot==0?initial.pelvis:steps[knot-1].pose.pelvis;
                Vector3 at=steps[knot].pose.pelvis, after=steps[knot+1].pose.pelvis;
                Vector3 v0=(at-before)/steps[knot].seconds, v1=(after-at)/steps[knot+1].seconds;
                // Monotone tangent avoids overshooting the narrow door/seat corridor.
                float Axis(float x,float y) => x*y<=0?0:Mathf.Sign(x)*Mathf.Min(Mathf.Abs(x),Mathf.Abs(y));
                return new Vector3(Axis(v0.x,v1.x),Axis(v0.y,v1.y),Axis(v0.z,v1.z));
            }
        }
        public static float Smooth(float t) { t=Mathf.Clamp01(t); return t*t*t*(t*(6*t-15)+10); }
        public static Vector3 Swing(Vector3 from, Vector3 to, float t, float clearance)
        {
            if(clearance<=0 || (from-to).sqrMagnitude<.000001f) return Vector3.Lerp(from,to,Smooth(t));
            // Lift before crossing the sill; land vertically instead of dragging through its edge.
            float travel=Smooth(Mathf.InverseLerp(.14f,.86f,t));
            var p=Vector3.Lerp(from,to,travel);
            float apex=Mathf.Max(from.y,to.y)+clearance;
            p.y=t<.5f?Mathf.Lerp(from.y,apex,Smooth(t*2)):Mathf.Lerp(apex,to.y,Smooth((t-.5f)*2));
            return p;
        }
    }
}
