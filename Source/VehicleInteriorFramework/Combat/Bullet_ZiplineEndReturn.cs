using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class Bullet_ZiplineEndReturn : Bullet, IZiplineEnd
    {
        private float ArcHeightFactor
        {
            get
            {
                float num = this.def.projectile.arcHeightFactor;
                float num2 = (this.destination - this.origin).MagnitudeHorizontalSquared();
                if (num * num > num2 * 0.2f * 0.2f)
                {
                    num = Mathf.Sqrt(num2) * 0.2f;
                }
                return num;
            }
        }

        public override Quaternion ExactRotation => Quaternion.LookRotation((origin - destination).Yto0());

        protected override void Tick()
        {
            if (launchVerb.caster?.Spawned ?? false)
            {
                destination = launchVerb.caster.TrueCenter() + (Vector3.forward * ZiplineEnd.LauncherOffset).RotatedBy(ExactRotation.eulerAngles.y);

                //戻ってる間砲塔をこっちに向けとくためにOrderAttackしとく
                if (launchVerb.caster is Building_Turret building_Turret)
                {
                    var forcedTarget = building_Turret.ForcedTarget;
                    var originCell = origin.ToIntVec3();
                    if (TargetMapManager.HasTargetMap(launchVerb.caster, out var destMap) && destMap.IsVehicleMapOf(out var vehicle))
                    {
                        originCell = originCell.ToVehicleMapCoord(vehicle);
                    }
                    if (forcedTarget != originCell)
                    {
                        tmpTarget = forcedTarget;
                        building_Turret.OrderAttack(originCell);
                    }
                }
            }
            base.Tick();
        }
        protected override void ImpactSomething()
        {
            Impact(null, false);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (blockedByShield) return;

            Destroy(DestroyMode.Vanish);
            launchVerb.ZiplineEnd = null;
            if (launchVerb.caster is Building_Turret building_Turret)
            {
                building_Turret.OrderAttack(tmpTarget);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            this.DrawZipline(drawLoc);
        }

        public void DrawZipline(Vector3 drawLoc)
        {
            if (launchVerb.caster?.Spawned ?? false)
            {
                var drawPosA = drawLoc + (Vector3.back * ZiplineEnd.ZiplineEndOffset).RotatedBy(ExactRotation.eulerAngles.y);
                var launcherPos = launcher.DrawPos;
                var drawPosB = launcherPos + (Vector3.forward * ZiplineEnd.LauncherOffset).RotatedBy((drawPosA - launcherPos).AngleFlat());
                var y = Mathf.Max(drawPosA.y, drawPosB.y);
                GenDraw.DrawLineBetween(drawPosA.WithY(y), drawPosB.WithY(y), ZiplineEnd.ZiplineLayer, ZiplineEnd.ZiplineMat, ZiplineEnd.ZiplineWidth);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref launchVerb, "launchVerb");
            Scribe_TargetInfo.Look(ref tmpTarget, "tmpTarget");
        }

        public Verb_LaunchZipline launchVerb;

        private LocalTargetInfo tmpTarget = LocalTargetInfo.Invalid;
    }
}
