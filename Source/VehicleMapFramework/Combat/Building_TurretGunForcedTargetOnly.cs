using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class Building_TurretGunForcedTargetOnly : Building_TurretGun
{
    public override Vector3 DrawPos
    {
        get
        {
            if (VehiclePawnWithMapCache.cacheModeGlobal)
            {
                return base.DrawPos;
            }
            return base.DrawPos + (gun?.def.graphicData?.DrawOffsetForRot(this.BaseRotationVehicleDraw()) ?? Vector3.zero);
        }
    }

    protected override bool CanSetForcedTarget => true;

    public override LocalTargetInfo TryFindNewTarget()
    {
        return LocalTargetInfo.Invalid;
    }
}
