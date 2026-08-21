using DevTools.Testing;
using RimWorld;
using Vehicles.Testing;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.Test_Logics;

internal abstract class CrossMapWorkGiverTestBase(VehicleGroup group)
{

  protected VehicleGroup group = group;

  protected WorkGiverTestBase.WorkGiverResult result;

  public abstract WorkGiverDef WorkGiverDef { get; }

  protected Map GroundMap => group.vehicle.Map;

  protected Map VehicleMap => ((VehiclePawnWithMap)group.vehicle).VehicleMap;

  protected Pawn Pawn => group.pawns[0];

  protected VehiclePawnWithMap Vehicle => (VehiclePawnWithMap)group.vehicle;

  [SetUp]
  public virtual void SetUp() { }

  [Test]
  public virtual void Run()
  {
    result = WorkGiverTestBase.RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
    Expect.IsNotNull(result.job, result.ToString());
    Pawn.jobs.StartJob(result.job, JobCondition.Succeeded);
    Pawn.jobs.JobTrackerTick();
    Expect.IsTrue(result.job == Pawn.CurJob ||
                  Pawn.jobs.curDriver is JobDriver_GotoDestMap { nextJob: { } nextJob } && nextJob.def == result.job?.def,
      $"job interrupted\n{result}\nbut curjob: {Pawn.CurJob}");
    TickWaiter.WaitUntilJobEnd(Pawn);
  }

  [TearDown]
  public virtual void TearDown()
  {
    Test_WorkGivers.ClearPawnState(Pawn);
    Pawn.DeSpawn();
    GenSpawn.Spawn(Pawn, Vehicle.VehicleMap.Center, Vehicle.VehicleMap);
    Clear();
  }

  public void Clear()
  {
    group = null;
  }
}
