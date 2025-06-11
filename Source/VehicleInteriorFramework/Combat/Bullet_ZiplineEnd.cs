using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.Sound;

namespace VehicleInteriors
{
    public class Bullet_ZiplineEnd : Bullet, IZiplineEnd
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

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            this.origin += (Vector3.forward * ZiplineEnd.LauncherOffset).RotatedBy(ExactRotation.eulerAngles.y);
        }
        protected override void Tick()
        {
            base.Tick();
            destination = destMap != null ? intendedTarget.Cell.ToVector3Shifted().ToBaseMapCoord(destMap) : intendedTarget.Cell.ToVector3Shifted();
        }

        protected override void ImpactSomething()
        {
            Impact(null, false);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (blockedByShield) return;

            var ziplineEnd = (ZiplineEnd)ThingMaker.MakeThing(VMF_DefOf.VMF_ZiplineEnd);
            ziplineEnd.launchVerb = launchVerb;
            ziplineEnd.rotation = ExactRotation.eulerAngles.y;
            launchVerb.ZiplineEnd = ziplineEnd;

            if (destMap != null)
            {
                if (destMap.IsVehicleMapOf(out var vehicle))
                {
                    vehicle.PlayImpactSound(new VehicleComponent.DamageResult
                    {
                        penetration = VehicleComponent.Penetration.Penetrated,
                        cell = intendedTarget.Cell.ToHitCell(vehicle)
                    });
                    ziplineEnd.rotation += vehicle.Angle;
                }
                else
                {
                    SoundDefOf.BulletImpact_Ground.PlayOneShot(intendedTarget.ToTargetInfo(destMap));
                }
                GenSpawn.Spawn(ziplineEnd, intendedTarget.Cell, destMap);
            }
            else
            {
                SoundDefOf.BulletImpact_Ground.PlayOneShot(intendedTarget.ToTargetInfo(Map));
                GenSpawn.Spawn(ziplineEnd, intendedTarget.Cell, Map);
            }
            TargetMapManager.TargetMap.Remove(launchVerb.caster);

            base.Destroy(DestroyMode.Vanish);

            if (launchVerb.CasterIsPawn)
            {
                launchVerb.OrderForceTarget(ziplineEnd);
            }
            else if (launchVerb.caster is Building_Turret building_Turret)
            {
                building_Turret.OrderAttack(ziplineEnd);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            this.DrawZipline(drawLoc);
        }

        public void DrawZipline(Vector3 drawLoc)
        {
            if (launcher != null && launcher.Spawned)
            {
                float num = ArcHeightFactor * GenMath.InverseParabola(DistanceCoveredFractionArc);
                var drawPosA = drawLoc + Vector3.forward * num + (Vector3.back * ZiplineEnd.ZiplineEndOffset).RotatedBy(ExactRotation.eulerAngles.y);
                var launcherPos = launcher.DrawPos;
                var drawPosB = launcherPos + (Vector3.forward * ZiplineEnd.LauncherOffset).RotatedBy((drawPosA - launcherPos).AngleFlat());
                var y = Mathf.Max(drawPosA.y, drawPosB.y);
                GenDraw.DrawLineBetween(drawPosA.WithY(y), drawPosB.WithY(y), ZiplineEnd.ZiplineLayer, ZiplineEnd.ZiplineMat, ZiplineEnd.ZiplineWidth);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref launchVerb, "LaunchVerb");
            Scribe_References.Look(ref destMap, "destMap");
        }

        public Verb_LaunchZipline launchVerb;

        public Map destMap;
    }
}
