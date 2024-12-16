using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public static class Rot8Utility
    {
        public static Quaternion AsQuat(this Rot8 rot)
        {
            switch (rot.AsInt)
            {
                case 0:
                    return Quaternion.identity;
                case 1:
                    return Quaternion.LookRotation(Vector3.right);
                case 2:
                    return Quaternion.LookRotation(Vector3.back);
                case 3:
                    return Quaternion.LookRotation(Vector3.left);
                case 4:
                    return Quaternion.LookRotation(new Vector3(1f, 0f, 1f));
                case 5:
                    return Quaternion.LookRotation(new Vector3(1f, 0f, -1f));
                case 6:
                    return Quaternion.LookRotation(new Vector3(-1f, 0f, -1f));
                case 7:
                    return Quaternion.LookRotation(new Vector3(-1f, 0f, 1f));
                default:
                    Log.Error("ToQuat with Rot = " + rot.AsInt.ToString());
                    return Quaternion.identity;
            }
        }

        public static Rot8 AsRot8(this float angle)
        {
            var angle2 = angle.ClampAngle();
            switch (angle2)
            {
                case 0f:
                case 360f:
                    return Rot8.North;
                case 45f:
                    return Rot8.NorthEast;
                case 90f:
                    return Rot8.East;
                case 135f:
                    return Rot8.SouthEast;
                case 180f:
                    return Rot8.South;
                case 225f:
                    return Rot8.SouthWest;
                case 270f:
                    return Rot8.West;
                case 315f:
                    return Rot8.NorthWest;
                default:
                    Log.Error("AsRot8 with angle = " + angle);
                    return Rot8.Invalid;
            }
        }
    }
}
