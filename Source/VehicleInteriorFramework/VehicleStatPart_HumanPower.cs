using RimWorld;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class VehicleStatPart_HumanPower : VehicleStatPart
    {
        protected float Modifier(VehiclePawn vehicle)
        {
            return vehicle.handlers.Where(h => h.Isnt<VehicleHandlerBuildable>() && h.RequiredForMovement).Average(h =>
            {
                var statValue = 0f;
                foreach (var pawn in h.handlers)
                {
                    if (!h.CanOperateRole(pawn)) continue;
                    var statFactor = 1f;
                    statFactor *= pawn.GetStatValue(StatDefOf.MoveSpeed);
                    statFactor *= this.StatFactorByWeight(pawn.GetStatValue(StatDefOf.IncomingDamageFactor), 0.75f);
                    statFactor *= this.StatFactorByWeight(pawn.GetStatValue(StatDefOf.WorkSpeedGlobal), 0.5f);
                    statFactor *= this.StatFactorByWeight(pawn.BodySize, 1.25f);
                    var skillFactor = Mathf.Max(pawn.skills.GetSkill(SkillDefOf.Melee).Level + pawn.skills.GetSkill(SkillDefOf.Mining).Level, 20f) / 10f;
                    statFactor *= this.StatFactorByWeight(skillFactor, 0.75f);
                }
                return statValue / h.role.Slots;
            });
        }

        private float StatFactorByWeight(float value, float weight)
        {
            return 1f + (value - 1f) * weight;
        }

        public override float TransformValue(VehiclePawn vehicle, float value)
        {
            if (vehicle.VehicleDef.HasModExtension<VehicleHumanPowered>())
            {
                return value * this.Modifier(vehicle);
            }
            return value;
        }

        public override string ExplanationPart(VehiclePawn vehicle)
        {
            if (vehicle.VehicleDef.HasModExtension<VehicleHumanPowered>())
            {
                return "VIF_StatsReport_HumanPowerAverage".Translate(this.Modifier(vehicle));
            }
            return null;
        }
    }
}
