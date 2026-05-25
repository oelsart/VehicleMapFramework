using System.Collections.Generic;
using RimWorld;
using SmashTools;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobDriver_RepairMapVehicle : JobDriver_RepairVehicle
{
  protected override JobDef JobDef => VMF_DefOf.VMF_RepairMapVehicle;

  public override bool TryMakePreToilReservations(bool errorOnFailed)
  {
    return pawn.Reserve(job.GetTarget(TargetIndex.B), job, errorOnFailed: errorOnFailed);
  }

  protected override IEnumerable<Toil> MakeNewToils()
  {
    this.FailOn(() => !pawn.IsOnVehicleMap);
    var gotoCellToil = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.ClosestTouch);
    yield return gotoCellToil;

    var workToil = ToilMaker.MakeToil();
    workToil.initAction = ResetWork;
    workToil.tickIntervalAction = WorkAction;
    if (EffecterDef != null)
    {
      workToil.WithEffect(EffecterDef, TargetIndex.A);
    }
    else
    {
      workToil.WithProgressBar(TargetIndex.A, GetProgressPct);
    }

    workToil.defaultCompleteMode = ToilCompleteMode;
    workToil.defaultDuration = 2000;
    if (Skill != null)
    {
      workToil.activeSkill = () => Skill;
    }

    yield return workToil;
    yield break;

    void WorkAction(int interval)
    {
      var actor = workToil.actor;
      if (Skill != null)
        actor.skills?.Learn(Skill, SkillAmount * interval);

      var statValue = actor.GetStatValue(Stat);
      Work -= statValue * interval;
      if (Work <= 0f)
      {
        WorkComplete(actor);
      }
    }
  }

  protected override void WorkComplete(Pawn actor)
  {
    var cell = VehicleMapUtility.MapCellToHitbox((VehiclePawnWithMap)Vehicle) + TargetB.Cell.ToIntVec2;
    var component =
      Vehicle.statHandler.ComponentsPrioritized.FirstOrDefault(c =>
        c.props.hitbox.Hitbox.Contains(cell) && c.HealthPercent < 1);
    if (component is null)
    {
      if (Vehicle.Spawned)
        MapComponentCache<ListerVehiclesRepairable>.GetComponent(Vehicle.Map).NotifyVehicleRepaired(Vehicle);
      actor.records.Increment(RecordDefOf.ThingsRepaired);
      actor.jobs.EndCurrentJob(JobCondition.Succeeded);
      return;
    }
    ResetWork();
    component.HealComponent(Vehicle.GetStatValue(VehicleStatDefOf.RepairRate));
    Vehicle.Transform.rotation = 0;
    if (!Vehicle.VehicleDef.graphicData.drawRotated)
      Vehicle.Rotation = Vehicle.VehicleDef.defaultPlacingRot;
  }
}
