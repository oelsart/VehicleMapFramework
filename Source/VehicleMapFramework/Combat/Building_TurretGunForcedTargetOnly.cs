using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class Building_TurretGunForcedTargetOnly : Building_TurretGun
{
    protected override bool CanSetForcedTarget => true;

    public override LocalTargetInfo TryFindNewTarget()
    {
        return LocalTargetInfo.Invalid;
    }
}
