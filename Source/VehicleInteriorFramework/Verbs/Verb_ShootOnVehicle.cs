using RimWorld;
using Verse;

namespace VehicleInteriors
{
    public class Verb_ShootOnVehicle : Verb_LaunchProjectileOnVehicle
    {
        protected override int ShotsPerBurst
        {
            get
            {
                return this.verbProps.burstShotCount;
            }
        }

        public override void WarmupComplete()
        {
            base.WarmupComplete();
            Pawn pawn = this.currentTarget.Thing as Pawn;
            if (pawn != null && !pawn.Downed && !pawn.IsColonyMech && this.CasterIsPawn && this.CasterPawn.skills != null)
            {
                float num = pawn.HostileTo(this.caster) ? 170f : 20f;
                float num2 = this.verbProps.AdjustedFullCycleTime(this, this.CasterPawn);
                this.CasterPawn.skills.Learn(SkillDefOf.Shooting, num * num2, false, false);
            }
        }

        protected override bool TryCastShot()
        {
            bool flag = base.TryCastShot();
            if (flag && this.CasterIsPawn)
            {
                this.CasterPawn.records.Increment(RecordDefOf.ShotsFired);
            }
            return flag;
        }
    }
}
