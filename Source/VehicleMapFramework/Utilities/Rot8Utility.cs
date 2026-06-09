using System.Runtime.CompilerServices;
using HarmonyLib;
using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public static class Rot8Utility
{
    public static readonly AccessTools.StructFieldRef<Rot4, byte> rot4Int = AccessTools.StructFieldRefAccess<Rot4, byte>("rotInt");

    public static IntVec3 RighthandCell(ref Rot4 rot)
    {
        Rotate(ref rot, RotationDirection.Clockwise);
        return rot.FacingCell;
    }

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

      public Rot4 AsRot4Force()
      {
        Rot4 rot4 = default;
        rot4Int(ref rot4) = rot.AsByte;
        return rot4;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public Rot8 Rotated(Rot8 other)
      {
        return new Rot8(Rot8.FromIntClockwise((rot.AsIntClockwise + other.AsIntClockwise) % 8));
      }
    }

    //Rot4の変数に入れたRot8を無理やり回転させるためのもの。Rot4.RotateとTranspilerで簡単に置き換えられるようにしてある
    public static void Rotate(ref Rot4 rot, RotationDirection rotDir)
    {
        if (rot.AsInt is < 0 or > 7)
        {
            return;
        }
        var rot2 = new Rot8(rot.AsInt);
        var num = rot2.AsIntClockwise;
        switch (rotDir)
        {
          case RotationDirection.Clockwise:
            num += 2;
            break;
          case RotationDirection.Counterclockwise:
            num -= 2;
            break;
          case RotationDirection.Opposite:
            num += 4;
            break;
          case RotationDirection.None:
          default:
            break;
        }

        rot2.AsInt = Rot8.FromIntClockwise(GenMath.PositiveMod(num, 8));
        rot4Int(ref rot) = rot2.AsByte;
    }

    public static Vector3 ToFundVector3(ref IntVec3 intVec)
    {
        if (intVec.IsCardinal)
        {
            return intVec.ToVector3();
        }
        return intVec.ToVector3() * sin45;
    }

    public static Vector3 AsFundVector2(ref Rot8 rot)
    {
        var vector = rot.AsVector2;
        if (rot.IsDiagonal)
        {
            vector *= sin45;
        }
        return vector;
    }

    private const float sin45 = 0.707106781f;
}
