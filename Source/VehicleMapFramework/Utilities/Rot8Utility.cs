﻿using HarmonyLib;
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

    //Rot4の変数に入れたRot8を無理やり回転させるためのもの。Rot4.RotateとTranspilerで簡単に置き換えられるようにしてある
    public static void Rotate(ref Rot4 rot, RotationDirection rotDir)
    {
        if (rot.AsInt < 0 || rot.AsInt > 7)
        {
            return;
        }
        var rot2 = new Rot8(rot.AsInt);
        int num = rot2.AsIntClockwise;
        if (rotDir == RotationDirection.Clockwise)
        {
            num += 2;
        }
        if (rotDir == RotationDirection.Counterclockwise)
        {
            num -= 2;
        }
        if (rotDir == RotationDirection.Opposite)
        {
            num += 4;
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
