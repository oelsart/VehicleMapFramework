using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class Building_TurretGunForcedTargetOnly : Building_TurretGun
    {
        public override Vector3 DrawPos => base.DrawPos + base.def.graphicData.DrawOffsetForRot(this.BaseRotationVehicleDraw());

        protected override bool CanSetForcedTarget => true;

        public override LocalTargetInfo TryFindNewTarget()
        {
            return LocalTargetInfo.Invalid;
        }
    }
}
