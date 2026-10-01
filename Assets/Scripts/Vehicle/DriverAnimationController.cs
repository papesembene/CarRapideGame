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
                    float stepWave=Mathf.Sin(Mathf.PI*t);
                    p.pelvis.y+=.009f*stepWave;
                    var facing=Quaternion.Euler(0,p.yaw,0);
                    float swing=(steps[i].leftLift>0?1:-1)*.065f*stepWave;
                    p.leftHand+=facing*new Vector3(0,0,-swing);
                    p.rightHand+=facing*new Vector3(0,0,swing);
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
        public IEnumerator WalkTo(Vector3 target,float speed=.65f)
        {
            var p=Rig.CurrentPose;
            var feet=(p.leftFoot+p.rightFoot)*.5f;
            Vector3 delta=target-feet;delta.y=0;
            if(delta.magnitude<.02f) yield break;
            float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            var direction=delta.normalized;
            int count=Mathf.Max(2,Mathf.CeilToInt(delta.magnitude/.19f)); if(count%2!=0)count++;
            float stride=delta.magnitude/count;
            var steps=new Step[count];
            var facing=Quaternion.Euler(0,yaw,0);
            for(int i=0;i<count;i++)
            {
                bool left=i%2==0;
                p.yaw=p.leftFootYaw=p.rightFootYaw=yaw;
                // Leave room for the supporting leg's diagonal reach during the stride.
                p.pelvis=feet+direction*stride*(i+1)+Vector3.up*(Rig.StandingHeight-.03f);
                var landing=feet+direction*Mathf.Min(delta.magnitude,stride*(i+2));
                if(left)p.leftFoot=landing+facing*Vector3.left*.10f;else p.rightFoot=landing+facing*Vector3.right*.10f;
                p.lean=2; DriverController.RelaxHands(ref p);
                steps[i]=new Step(p,stride/speed,left?.045f:0,left?0:.045f);
            }
            yield return Sequence(steps);
            p.pelvis=target+Vector3.up*(Rig.StandingHeight-.004f);p.lean=0;
            p.leftFoot=target+facing*Vector3.left*.1f;p.rightFoot=target+facing*Vector3.right*.1f;
            DriverController.RelaxHands(ref p);
            yield return Transition(p,.25f,.025f,.025f);
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
