using System.Collections;
using DevTools.Testing;
using RimWorld;
using UnityEngine.Assertions;
using Vehicles.Testing;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.Test_DevTools;

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
  public virtual IEnumerator Run()
  {
    result = WorkGiverTestBase.RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
    Assert.IsNotNull(result.job, result.ToString());
    Pawn.jobs.StartJob(result.job, JobCondition.InterruptForced);
    Expect.AreEqual(result.job.NextJobOrMe.def, Pawn.NextJobOrCurJob.def, $"job interrupted\n{result}\nbut curjob: {Pawn.NextJobOrCurJob}");
    yield return Pawn.WaitUntilIdle();
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
