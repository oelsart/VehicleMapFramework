using DevTools.Testing;
using RimWorld;
using Vehicles;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal abstract class CrossMapWorkGiverTestBase(VehicleGroup group)
{
    public abstract WorkGiverDef WorkGiverDef { get; }

    protected VehicleGroup group = group;
    
    protected Map GroundMap => group.vehicle.Map;
    
    protected Map VehicleMap => ((VehiclePawnWithMap)group.vehicle).VehicleMap;
    
    protected Pawn Pawn => group.pawns[0];

    protected VehiclePawn Vehicle => group.vehicle;

    protected WorkGiverTestBase.WorkGiverResult result;

    public virtual void SetUp()
    {
    }

    public virtual void Execute()
    {
        result = WorkGiverTestBase.RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
        Expect.IsNotNull(result.job, result.ToString());
        Pawn.jobs.StartJob(result.job);
        Pawn.jobs.JobTrackerTick();
        Expect.IsTrue(result.job == Pawn.CurJob ||
                      Pawn.jobs.curDriver is JobDriver_GotoDestMap { nextJob: { } nextJob } && nextJob.def == result.job?.def,
            $"job interrupted\n{result}\nbut curjob: {Pawn.CurJob}");
    }

    public virtual void TearDown()
    {
        UnitTest_WorkGivers.ClearPawnState(Pawn);
        Clear();
    }

    public void Clear()
    {
        group = null;
    }
}