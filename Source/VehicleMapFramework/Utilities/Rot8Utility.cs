using System.Runtime.CompilerServices;
using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public static class Rot8Utility
{
    public static Quaternion AsQuat(ref Rot8 rot)
    {
        return rot.AsQuat();
    }

    extension(Rot8 rot)
    {
      public Quaternion AsQuat()
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
            Log.Error("ToQuat with Rot = " + rot.AsInt);
            return Quaternion.identity;
        }
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public Rot8 Rotated(Rot8 other)
      {
        return new Rot8(Rot8.FromIntClockwise((rot.AsIntClockwise + other.AsIntClockwise) % 8));
      }
    }

    public static Vector3 ToFundVector3(ref IntVec3 intVec)
    {
        if (intVec.IsCardinal)
        {
            return intVec.ToVector3();
        }
        return intVec.ToVector3() * sin45;
    }

    private const float sin45 = 0.707106781f;
}
