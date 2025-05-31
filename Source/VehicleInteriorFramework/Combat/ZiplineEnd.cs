using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    [StaticConstructorOnStartup]
    public class ZiplineEnd : ThingWithComps, IZiplineEnd
    {
        public override void Tick()
        {
            base.Tick();
            if (!launchVerb.caster?.Spawned ?? false)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }
            if (launchVerb.caster is Pawn pawn && pawn.TargetCurrentlyAimingAt != this ||
                launchVerb.caster is Building_Turret building_Turret && building_Turret.CurrentTarget != this ||
                launchVerb.OutOfRange(launchVerb.caster.PositionOnBaseMap(), this, this.MovedOccupiedRect()) ||
                !GenSightOnVehicle.LineOfSightThingToThing(launchVerb.caster, this))
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (launchVerb.caster?.Spawned ?? false)
            {
                var bullet = (Bullet_ZiplineEndReturn)GenSpawn.Spawn(VMF_DefOf.VMF_Bullet_ZiplineTurretReturn, this.PositionOnBaseMap(), this.BaseMap());
                bullet.launchVerb = launchVerb;
                launchVerb.ZiplineEnd = bullet;
                bullet.Launch(launchVerb.caster, this.TrueCenter(), launchVerb.caster, launchVerb.caster, ProjectileHitFlags.IntendedTarget);
            }
            base.Destroy(mode);
        }

        public override void Print(SectionLayer layer)
        {
            Graphic.Print(layer, this, VehicleMapUtility.PrintExtraRotation(this) + rotation);
            foreach (var comp in AllComps)
            {
                comp.PostPrintOnto(layer);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            this.DrawZipline(drawLoc);
        }

        public void DrawZipline(Vector3 drawLoc)
        {
            if (launchVerb.caster != null && launchVerb.caster.Spawned)
            {
                var rot = rotation;
                if (this.IsOnVehicleMapOf(out var vehicle))
                {
                    rot -= vehicle.Angle;
                }
                var drawPosA = drawLoc + (Vector3.back * ZiplineEndOffset).RotatedBy(rot);
                var launcherPos = launchVerb.caster.DrawPos;
                var drawPosB = launcherPos + (Vector3.forward * LauncherOffset).RotatedBy((drawPosA - launcherPos).AngleFlat());
                var y = Mathf.Max(drawPosA.y, drawPosB.y);
                GenDraw.DrawLineBetween(drawPosA.WithY(y), drawPosB.WithY(y), ZiplineLayer, ZiplineMat, ZiplineWidth);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref launchVerb, "launchVerb");
            Scribe_Values.Look(ref rotation, "rotation");
        }

        public Verb_LaunchZipline launchVerb;

        public float rotation;

        public static Material ZiplineMat = MaterialPool.MatFrom("VehicleInteriors/Things/ZiplineTurret/Zipline");

        public const float ZiplineWidth = 0.135f;

        public const float ZiplineEndOffset = 0.42f;

        public const float ZiplineLayer = 0.03846154f;

        public const float LauncherOffset = 0.85f;
    }
}
