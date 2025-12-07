using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_DoctorTendToHumanlikes(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("DoctorTendToHumanlikes");

    private Pawn patient;
    
    private Building_Bed bed;

    public override void SetUp()
    {
        base.SetUp();
        patient = TestUtility.GeneratePatient(Pawn.Faction);
        bed = (Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog);
        bed.SetFaction(Pawn.Faction);
        GenSpawn.Spawn(bed, FromRUCorner(GroundMap, 3), GroundMap);
        GenSpawn.Spawn(patient, bed.Position, GroundMap);
        patient.ownership.ClaimBedIfNonMedical(bed);
        patient.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDownResting, bed));
        patient.jobs.JobTrackerTick();
    }

    public override void TearDown()
    {
        patient.Destroy();
        bed.Destroy();
        patient = null;
        bed = null;
        base.TearDown();
    }
}