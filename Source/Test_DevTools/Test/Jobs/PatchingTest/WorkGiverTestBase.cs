using System.Text;
using DevTools.Testing;
using RimWorld;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Vehicles.Testing;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.Test_Logics;

internal abstract class WorkGiverTestBase(VehicleGroup group)
{

  protected VehicleGroup group = group;
  protected WorkGiverResult[] results = new WorkGiverResult[2];

  public abstract WorkGiverDef WorkGiverDef { get; }

  public abstract Type BeforePatchingType { get; }

  public abstract Type AfterPatchingType { get; }

  protected static WorkGiverResult RunWorkGiverBeforePatch(Pawn pawn, WorkGiverDef workGiverDef)
  {
    var result = new WorkGiverResult();
    var workGiver = workGiverDef.Worker;
    result.workGiver = workGiver;
    result.pawnCanUse = PawnCanUseWorkGiver(pawn, workGiver);
    if (!result.pawnCanUse)
      return result;
    result.job = workGiver.NonScanJob(pawn);
    if (result.job != null || workGiver is not WorkGiver_Scanner scanner)
      return result;
    var target = TargetInfo.Invalid;
    if (workGiverDef.scanThings)
    {
      result.things = scanner.PotentialWorkThingsGlobal(pawn)?.ToList();
      var flag = pawn.carryTracker?.CarriedThing != null &&
                 scanner.PotentialWorkThingRequest.Accepts(pawn.carryTracker.CarriedThing) &&
                 Validator(pawn.carryTracker.CarriedThing);
      if (scanner.Prioritized)
      {
        result.things ??= pawn.Map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
        result.thing = !scanner.AllowUnreachable
          ? GenClosest.ClosestThing_Global_Reachable(pawn.Position,
            pawn.Map,
            result.things,
            scanner.PathEndMode,
            TraverseParms.For(pawn, scanner.MaxPathDanger(pawn)),
            9999f,
            Validator,
            x => scanner.GetPriority(pawn, x))
          : GenClosest.ClosestThing_Global(pawn.Position,
            result.things,
            99999f,
            Validator,
            x => scanner.GetPriority(pawn, x));
        if (flag)
        {
          if (result.thing != null)
          {
            var num2 = scanner.GetPriority(pawn, pawn.carryTracker.CarriedThing);
            var num3 = scanner.GetPriority(pawn, result.thing);
            if (num2 >= num3)
            {
              result.thing = pawn.carryTracker.CarriedThing;
            }
          }
          else
          {
            result.thing = pawn.carryTracker.CarriedThing;
          }
        }
      }
      else if (flag)
      {
        result.thing = pawn.carryTracker.CarriedThing;
      }
      else if (scanner.AllowUnreachable)
      {
        result.things ??= pawn.Map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
        result.thing = GenClosest.ClosestThing_Global(pawn.Position, result.things, 99999f, Validator);
      }
      else
      {
        result.thing = GenClosest.ClosestThingReachable(pawn.Position,
          pawn.Map,
          scanner.PotentialWorkThingRequest,
          scanner.PathEndMode,
          TraverseParms.For(pawn, scanner.MaxPathDanger(pawn)),
          9999f,
          Validator,
          result.things,
          0,
          scanner.MaxRegionsToScanBeforeGlobalSearch,
          result.things != null);
      }

      if (result.thing != null)
        target = result.thing;

      bool Validator(Thing t)
      {
        return !t.IsForbidden(pawn) && scanner.HasJobOnThing(pawn, t);
      }
    }

    if (scanner.def.scanCells)
    {
      var closestDistSquared = 99999f;
      var bestPriority = float.MinValue;
      var allowUnreachable = scanner.AllowUnreachable;
      var maxPathDanger = scanner.MaxPathDanger(pawn);
      result.cells = scanner.PotentialWorkCellsGlobal(pawn)?.ToList();
      if (result.cells != null)
      {
        foreach (var c in result.cells)
        {
          ProcessCell(c);
        }
      }

      void ProcessCell(IntVec3 c)
      {
        var flag = false;
        float num5 = (c - pawn.Position).LengthHorizontalSquared;
        var num6 = 0f;
        if (scanner.Prioritized)
        {
          if (!c.IsForbidden(pawn) && scanner.HasJobOnCell(pawn, c))
          {
            if (!allowUnreachable && !pawn.CanReach(c, scanner.PathEndMode, maxPathDanger))
            {
              return;
            }

            num6 = scanner.GetPriority(pawn, c);
            if (num6 > bestPriority ||
                Mathf.Approximately(num6, bestPriority) && num5 < closestDistSquared)
            {
              flag = true;
            }
          }
        }
        else if (num5 < closestDistSquared && !c.IsForbidden(pawn) && scanner.HasJobOnCell(pawn, c))
        {
          if (!allowUnreachable && !pawn.CanReach(c, scanner.PathEndMode, maxPathDanger))
          {
            return;
          }

          flag = true;
        }

        if (flag)
        {
          result.cell = c;
          target = new TargetInfo(c, pawn.Map);
          closestDistSquared = num5;
          bestPriority = num6;
        }
      }
    }

    if (target.IsValid)
    {
      var job3 = target.HasThing
        ? scanner.JobOnThing(pawn, target.Thing)
        : scanner.JobOnCell(pawn, target.Cell);
      result.job = job3;
    }

    return result;

    static bool PawnCanUseWorkGiver(Pawn pawn, WorkGiver giver)
    {
      if (!giver.def.nonColonistsCanDo && !pawn.IsColonist && !pawn.IsColonyMech && !pawn.IsColonySubhuman)
      {
        return false;
      }
      if (pawn.WorkTagIsDisabled(giver.def.workTags))
      {
        return false;
      }
      if (giver.def.workType != null && pawn.WorkTypeIsDisabled(giver.def.workType))
      {
        return false;
      }
      if (giver.ShouldSkip(pawn))
      {
        return false;
      }
      if (giver.MissingRequiredCapacity(pawn) != null)
      {
        return false;
      }
      return !pawn.RaceProps.IsMechanoid || giver.def.canBeDoneByMechs;
    }
  }

  internal static WorkGiverResult RunWorkGiverAfterPatch(Pawn pawn, VehiclePawn vehicle, WorkGiverDef workGiverDef)
  {
    Expect.IsNotNull(pawn, "pawn is null");
    Expect.IsNotNull(vehicle, "vehicle is null");
    Expect.IsNotNull(workGiverDef, "workGiverDef is null");
    var result = new WorkGiverResult();

    var workGiver = workGiverDef.Worker;
    result.workGiver = workGiver;
    result.pawnCanUse = PawnCanUseWorkGiver(pawn, workGiver);
    if (!result.pawnCanUse)
    {
      Test.Fail($"{pawn} can not use {workGiver}");
      return result;
    }
    result.job = workGiver.NonScanJobAll(pawn);
    if (result.job != null || workGiver is not WorkGiver_Scanner scanner)
      return result;
    var target = TargetInfo.Invalid;
    if (workGiverDef.scanThings)
    {
      result.things = scanner.PotentialWorkThingsGlobalAll(pawn)?.ToList();
      RemoveGroupVehicle();
      var flag = pawn.carryTracker?.CarriedThing != null &&
                 scanner.PotentialWorkThingRequest.Accepts(pawn.carryTracker.CarriedThing) &&
                 Validator(pawn.carryTracker.CarriedThing);
      if (scanner.Prioritized)
      {
        result.things ??= Patch_JobGiver_Work_TryIssueJobPackage.AddSearchSet(
          pawn.Map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest),
          pawn,
          scanner).ToList();
        RemoveGroupVehicle();
        result.thing = !scanner.AllowUnreachable
          ? GenClosestCrossMap.ClosestThing_Global_Reachable(pawn.Position,
            pawn.Map,
            result.things,
            scanner.PathEndMode,
            TraverseParms.For(pawn, scanner.MaxPathDanger(pawn)),
            9999f,
            Validator,
            x => scanner.GetPriority(pawn, x))
          : GenClosestCrossMap.ClosestThing_Global(pawn.Position,
            result.things,
            99999f,
            Validator,
            x => scanner.GetPriority(pawn, x));
        if (flag)
        {
          if (result.thing != null)
          {
            var num2 = scanner.GetPriority(pawn, pawn.carryTracker.CarriedThing);
            var num3 = scanner.GetPriority(pawn, result.thing);
            if (num2 >= num3)
            {
              result.thing = pawn.carryTracker.CarriedThing;
            }
          }
          else
          {
            result.thing = pawn.carryTracker.CarriedThing;
          }
        }
      }
      else if (flag)
      {
        result.thing = pawn.carryTracker.CarriedThing;
      }
      else if (scanner.AllowUnreachable)
      {
        result.things ??= Patch_JobGiver_Work_TryIssueJobPackage.AddSearchSet(
          pawn.Map.listerThings.ThingsMatching(scanner.PotentialWorkThingRequest),
          pawn,
          scanner).ToList();
        RemoveGroupVehicle();
        result.thing = GenClosestCrossMap.ClosestThing_Global(pawn.Position, result.things, 99999f, Validator);
      }
      else
      {
        result.thing = GenClosest.ClosestThingReachable(pawn.Position,
          pawn.Map,
          scanner.PotentialWorkThingRequest,
          scanner.PathEndMode,
          TraverseParms.For(pawn, scanner.MaxPathDanger(pawn)),
          9999f,
          Validator,
          result.things,
          0,
          scanner.MaxRegionsToScanBeforeGlobalSearch,
          result.things != null);
      }

      if (result.thing != null)
        target = result.thing;

      bool Validator(Thing t)
      {
        return !t.IsForbidden(pawn) && scanner.HasJobOnThingMap(pawn, t);
      }
    }

    if (scanner.def.scanCells)
    {
      var innerClass = new Patch_JobGiver_Work_TryIssueJobPackage.InnerClass
      {
        pawn = pawn, bestTargetOfLastPriority = target, scannerWhoProvidedTarget = scanner
      };
      var innerStruct = new Patch_JobGiver_Work_TryIssueJobPackage.InnerStruct
      {
        pawnPosition = pawn.Position,
        prioritized = scanner.Prioritized,
        allowUnreachable = scanner.AllowUnreachable,
        maxPathDanger = scanner.MaxPathDanger(pawn),
        bestPriority = float.MinValue,
        closestDistSquared = 99999f
      };
      result.cells = scanner.PotentialWorkCellsGlobal(pawn)?.ToList();
      scanner.ScanCellsAcrossMaps(ref innerClass, ref innerStruct);
      target = innerClass.bestTargetOfLastPriority;
      result.cell = target.Cell;
    }

    if (target.IsValid)
    {
      var job3 = target.HasThing
        ? scanner.JobOnThingMap(pawn, target.Thing)
        : scanner.JobOnCellMap(pawn, target);
      result.job = job3;
    }

    return result;

    // VehicleGroupのvehicleがスポーンしたことにより結果が変わるのを防ぐ
    void RemoveGroupVehicle()
    {
      result.things?.Remove(vehicle);
    }

    static bool PawnCanUseWorkGiver(Pawn pawn, WorkGiver giver)
    {
      if (!giver.def.nonColonistsCanDo && !pawn.IsColonist && !pawn.IsColonyMech && !pawn.IsColonySubhuman)
      {
        Log.Error($"Pawn {pawn.LabelShort} cannot use workgiver {giver.def.defName} because it is not a colonist.");
        return false;
      }
      if (pawn.WorkTagIsDisabled(giver.def.workTags))
      {
        Log.Error($"Pawn {pawn.LabelShort} cannot use workgiver {giver.def.defName} because it has a disabled work tag.");
        return false;
      }
      if (giver.def.workType != null && pawn.WorkTypeIsDisabled(giver.def.workType))
      {
        Log.Error($"Pawn {pawn.LabelShort} cannot use workgiver {giver.def.defName} because it has a disabled work type.");
        return false;
      }
      if (giver.ShouldSkipAll(pawn))
      {
        Log.Error($"Pawn {pawn.LabelShort} cannot use workgiver {giver.def.defName} because it should skip job.");
        return false;
      }
      if (giver.MissingRequiredCapacity(pawn) != null)
      {
        Log.Error($"Pawn {pawn.LabelShort} cannot use workgiver {giver.def.defName} because it is missing required capacity.");
        return false;
      }
      if (pawn.RaceProps.IsMechanoid && !giver.def.canBeDoneByMechs)
      {
        Log.Error($"Pawn {pawn.LabelShort} cannot use workgiver {giver.def.defName} because it is a mechanoid and cannot be done by mechanoids.");
        return false;
      }
      return true;
    }
  }

  protected abstract class Base(WorkGiverTestBase parent)
  {
    protected WorkGiverDef WorkGiverDef => parent.WorkGiverDef;

    protected VehicleGroup Group
    {
      get => parent.group;
      set => parent.group = value;
    }

    protected Pawn Pawn => Group.pawns[0];

    protected VehiclePawnWithMap Vehicle => (VehiclePawnWithMap)Group.vehicle;

    protected WorkGiverResult[] Results
    {
      get => parent.results;
      set => parent.results = value;
    }
  }

  protected abstract class BeforePatching(WorkGiverTestBase parent) : Base(parent)
  {
    [SetUp]
    public virtual void SetUp() { }

    [Test]
    public virtual void RunBefore()
    {
      Results[0] = RunWorkGiverBeforePatch(Pawn, WorkGiverDef);
      Expect.IsNotNull(Results[0].job);
    }
  }

  protected abstract class AfterPatching(WorkGiverTestBase parent) : Base(parent)
  {
    [Test]
    public virtual void RunAfter()
    {
      Results[1] = RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
      Expect.AreEqual(Results[0], Results[1]);
      Expect.IsFalse(JobFailReason.HaveReason, JobFailReason.Reason);
    }

    [TearDown]
    public virtual void TearDown()
    {
      Test_WorkGivers.ClearPawnState(Pawn);
      Clear();
    }

    public void Clear()
    {
      Group = null;
      Results = null;
    }
  }

  internal struct WorkGiverResult : IEquatable<WorkGiverResult>
  {
    public WorkGiverResult() { }

    public WorkGiver workGiver;

    public bool pawnCanUse;

    public List<Thing> things;

    public Thing thing;

    public List<IntVec3> cells;

    public IntVec3 cell = IntVec3.Invalid;

    public Job job;

    bool IEquatable<WorkGiverResult>.Equals(WorkGiverResult other)
    {
      return workGiver == other.workGiver &&
             pawnCanUse == other.pawnCanUse &&
             SequenceEqual(things, other.things) &&
             thing == other.thing &&
             SequenceEqual(cells, other.cells) &&
             cell == other.cell &&
             (job is null && other.job is null ||
              job != null && other.job != null &&
              job.def == other.job.def &&
              job.targetA == other.job.targetA &&
              job.targetB == other.job.targetB &&
              job.targetC == other.job.targetC);

      static bool SequenceEqual<TSource>(IEnumerable<TSource> first, IEnumerable<TSource> second)
      {
        if (first == null)
        {
          return second == null;
        }
        return second != null && first.SequenceEqual(second);
      }
    }

    public override string ToString()
    {
      var stringBuilder = new StringBuilder("\n");
      stringBuilder.AppendLine($"  WorkGiver: {workGiver?.def.defName}");
      stringBuilder.AppendLine($"  PawnCanUse: {pawnCanUse}");
      stringBuilder.AppendLine($"  ThingsCount: {things?.Count ?? 0}");
      stringBuilder.AppendLine($"  Thing: {thing}");
      stringBuilder.AppendLine($"  CellsCount: {cells?.Count ?? 0}");
      stringBuilder.AppendLine($"  Cell: {cell}");
      stringBuilder.AppendLine($"  Job: {job}");
      return stringBuilder.ToString();
    }
  }
}
