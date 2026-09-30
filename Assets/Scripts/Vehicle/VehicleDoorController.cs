using UnityEngine;

namespace CarRapide.Vehicle
{
    public sealed class VehicleDoorController : MonoBehaviour
    {
        Transform door, mirror, handle;
        Quaternion closed;
        Vector3 hingeAxis;
        VehicleCharacterClearance clearance;
        Transform frame;
        public float OpenAngle { get; private set; }
        public bool IsClosed => Mathf.Abs(OpenAngle) < 0.5f;
        public bool IsBlocked { get; private set; }

        public void Initialize(VehicleInteractionPoints points)
        {
            door = points.driverDoorMesh;
            frame=points.transform;
            clearance=points.GetComponent<VehicleDriverExperience>().Clearance;
            mirror = points.driverMirror;
            handle = points.doorHandle;
            closed = door.localRotation;
            hingeAxis = door.parent.InverseTransformDirection(points.transform.up);
            // The FBX already has a hinge at its front edge. The .001 object is the RIGHT mirror.
            // Preserve world transforms when attaching the actual LEFT mirror and handle.
            mirror.SetParent(door, true);
            handle.SetParent(door, true);
            points.doorPull.SetParent(door,true);
        }

        public void SetOpening(float degrees)
        {
            OpenAngle = Mathf.Clamp(degrees, 0, 78);
            door.localRotation = Quaternion.AngleAxis(OpenAngle, hingeAxis) * closed;
        }

        // Sweep in small angular increments even during a long frame. A blocked door keeps
        // its last safe angle; it never pushes the character through the bodywork.
        public bool TryCloseTo(float degrees, CharacterContactRig occupant, Transform grip = null)
        {
            degrees=Mathf.Clamp(degrees,0,78); IsBlocked=false;
            if(degrees>=OpenAngle) { SetOpening(degrees); return true; }
            while(OpenAngle>degrees+.001f)
            {
                float previous=OpenAngle;
                var previousPose=occupant.CurrentPose;
                SetOpening(Mathf.Max(degrees,previous-1));
                if(grip)
                {
                    var p=previousPose; p.leftHand=frame.InverseTransformPoint(grip.position);
                    occupant.SetPose(p);
                }
                occupant.ApplyPose();
                if(clearance.Measure(occupant.Animator,true)>.01f)
                {
                    SetOpening(previous); occupant.SetPose(previousPose); occupant.ApplyPose();
                    IsBlocked=true; return false;
                }
            }
            return true;
        }
    }
}
