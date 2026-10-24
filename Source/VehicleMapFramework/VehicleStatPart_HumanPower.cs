using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehicleStatPart_HumanPower : VehicleStatPart
{
  private static float Modifier(VehiclePawn vehicle)
  {
    if (vehicle.handlers is null)
      return 0f;
    
    var total = 0f;
    var count = 0;
    foreach (var h in vehicle.handlers)
    {
      if (h is VehicleRoleHandlerBuildable or { RequiredForMovement: false })
        continue;

      var statValue = 0f;
      foreach (var pawn in h.thingOwner)
      {
        if (!h.CanOperateRole(pawn)) continue;
        var statFactor = 1f;
        statFactor *= pawn.GetStatValue(StatDefOf.MoveSpeed) / StatDefOf.MoveSpeed.defaultBaseValue;
        statFactor *= Mathf.LerpUnclamped(1f,
          1f / pawn.GetStatValue(StatDefOf.IncomingDamageFactor) / StatDefOf.IncomingDamageFactor.defaultBaseValue,
          0.5f);
        statFactor *= Mathf.LerpUnclamped(1f,
          pawn.GetStatValue(StatDefOf.WorkSpeedGlobal) / StatDefOf.WorkSpeedGlobal.defaultBaseValue, 0.25f);
        statFactor *= Mathf.LerpUnclamped(1f, pawn.BodySize, 1.25f);
        var skillFactor =
          Mathf.Min(pawn.skills.GetSkill(SkillDefOf.Melee).Level + pawn.skills.GetSkill(SkillDefOf.Mining).Level,
            20f) / 10f;
        statFactor *= Mathf.LerpUnclamped(1f, skillFactor, 0.5f);
        statValue += statFactor;
      }

      total += statValue / h.role.Slots;
      count++;
    }

    return count > 0 ? total / count : 0f;
  }

  public override float TransformValue(VehiclePawn vehicle, float value)
  {
    if (vehicle.VehicleDef.HasModExtension<VehicleHumanPowered>())
    {
      return value * Modifier(vehicle);
    }
    return value;
  }

  public override string ExplanationPart(VehiclePawn vehicle)
  {
    return vehicle.VehicleDef.HasModExtension<VehicleHumanPowered>() ? "VMF_StatsReport_HumanPowerAverage".Translate(Modifier(vehicle).ToStringByStyle(ToStringStyle.FloatMaxTwo, ToStringNumberSense.Factor)) : null;
  }
}
