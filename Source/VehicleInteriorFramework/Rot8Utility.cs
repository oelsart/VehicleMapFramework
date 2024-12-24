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
    }
}
