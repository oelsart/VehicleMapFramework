using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VehicleInteriors;

public struct ShotReportOnVehicle
{
    private float FactorFromPosture
    {
        get
        {
            if (target.HasThing && target.Thing is Pawn p && distance >= 4.5f && p.GetPosture() != PawnPosture.Standing)
            {
                return 0.5f;
            }
            return 1f;
        }
    }

    private float FactorFromExecution
    {
        get
        {
            if (target.HasThing && target.Thing is Pawn p && distance <= 3.9f && p.GetPosture() != PawnPosture.Standing)
            {
                return 7.5f;
            }
            return 1f;
        }
    }

    public float AimOnTargetChance_StandardTarget
    {
        get
        {
            float num = factorFromShooterAndDist * factorFromEquipment * factorFromWeather * factorFromCoveringGas * FactorFromExecution;
            num += offsetFromDarkness;
            if (num < 0.0201f)
            {
                num = 0.0201f;
            }
            return num;
        }
    }

    public float AimOnTargetChance_IgnoringPosture => AimOnTargetChance_StandardTarget * factorFromTargetSize;

    public float AimOnTargetChance => AimOnTargetChance_IgnoringPosture * FactorFromPosture;

    public readonly float PassCoverChance => 1f - coversOverallBlockChance;

    public float TotalEstimatedHitChance
    {
        get
        {
            return Mathf.Clamp01(AimOnTargetChance * PassCoverChance);
        }
    }

    public readonly ShootLine ShootLine
    {
        get
        {
            return shootLine;
        }
    }

    public static ShotReportOnVehicle HitReportFor(Thing caster, Verb verb, LocalTargetInfo target)
    {
        Map targetMap = target.HasThing ? target.Thing.Map : caster.BaseMap();
        IntVec3 casterPositionOnTargetMap = target.HasThing ? caster.PositionOnAnotherThingMap(target.Thing) : caster.PositionOnBaseMap();
        ShotReportOnVehicle shotReportOnVehicle;
        shotReportOnVehicle.distance = (target.Cell - casterPositionOnTargetMap).LengthHorizontal;
        shotReportOnVehicle.target = target.ToTargetInfo(targetMap);
        if (verb.verbProps.canGoWild)
        {
            shotReportOnVehicle.factorFromShooterAndDist = ShotReport.HitFactorFromShooter(caster, shotReportOnVehicle.distance);
        }
        else
        {
            shotReportOnVehicle.factorFromShooterAndDist = 1f;
        }
        shotReportOnVehicle.factorFromEquipment = verb.verbProps.GetHitChanceFactor(verb.EquipmentSource, shotReportOnVehicle.distance);
        shotReportOnVehicle.covers = CoverUtility.CalculateCoverGiverSet(target, casterPositionOnTargetMap, targetMap);
        shotReportOnVehicle.coversOverallBlockChance = CoverUtility.CalculateOverallBlockChance(target, casterPositionOnTargetMap, targetMap);
        shotReportOnVehicle.factorFromCoveringGas = 1f;
        if (verb.TryFindShootLineFromToOnVehicle(verb.caster.PositionOnBaseMap(), target, out shotReportOnVehicle.shootLine, false))
        {
            using (IEnumerator<IntVec3> enumerator = shotReportOnVehicle.shootLine.Points().GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.AnyGas(caster.BaseMap(), GasType.BlindSmoke))
                    {
                        shotReportOnVehicle.factorFromCoveringGas = 0.7f;
                        break;
                    }
                }
                goto IL_13D;
            }
        }
        shotReportOnVehicle.shootLine = new ShootLine(IntVec3.Invalid, IntVec3.Invalid);
    IL_13D:
        if (!caster.PositionOnBaseMap().Roofed(caster.BaseMap()) || !target.CellOnBaseMap().Roofed(caster.BaseMap()))
        {
            shotReportOnVehicle.factorFromWeather = caster.Map.weatherManager.CurWeatherAccuracyMultiplier;
        }
        else
        {
            shotReportOnVehicle.factorFromWeather = 1f;
        }
        if (target.HasThing)
        {
            if (target.Thing is Pawn pawn)
            {
                shotReportOnVehicle.factorFromTargetSize = pawn.BodySize;
            }
            else
            {
                shotReportOnVehicle.factorFromTargetSize = target.Thing.def.fillPercent * target.Thing.def.size.x * target.Thing.def.size.z * 2.5f;
            }
            shotReportOnVehicle.factorFromTargetSize = Mathf.Clamp(shotReportOnVehicle.factorFromTargetSize, 0.5f, 2f);
        }
        else
        {
            shotReportOnVehicle.factorFromTargetSize = 1f;
        }
        shotReportOnVehicle.forcedMissRadius = verb.verbProps.ForcedMissRadius;
        shotReportOnVehicle.offsetFromDarkness = 0f;
        if (ModsConfig.IdeologyActive && target.HasThing)
        {
            if (DarknessCombatUtility.IsOutdoorsAndLit(target.Thing))
            {
                shotReportOnVehicle.offsetFromDarkness = caster.GetStatValue(StatDefOf.ShootingAccuracyOutdoorsLitOffset, true, -1);
            }
            else if (DarknessCombatUtility.IsOutdoorsAndDark(target.Thing))
            {
                shotReportOnVehicle.offsetFromDarkness = caster.GetStatValue(StatDefOf.ShootingAccuracyOutdoorsDarkOffset, true, -1);
            }
            else if (DarknessCombatUtility.IsIndoorsAndDark(target.Thing))
            {
                shotReportOnVehicle.offsetFromDarkness = caster.GetStatValue(StatDefOf.ShootingAccuracyIndoorsDarkOffset, true, -1);
            }
            else if (DarknessCombatUtility.IsIndoorsAndLit(target.Thing))
            {
                shotReportOnVehicle.offsetFromDarkness = caster.GetStatValue(StatDefOf.ShootingAccuracyIndoorsLitOffset, true, -1);
            }
        }
        return shotReportOnVehicle;
    }

    public string GetTextReadout()
    {
        StringBuilder stringBuilder = new();
        if (forcedMissRadius > 0.5f)
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("WeaponMissRadius".Translate() + ": " + forcedMissRadius.ToString("F1"));
            stringBuilder.AppendLine("DirectHitChance".Translate() + ": " + (1f / GenRadial.NumCellsInRadius(forcedMissRadius)).ToStringPercent());
        }
        else
        {
            stringBuilder.AppendLine(TotalEstimatedHitChance.ToStringPercent());
            stringBuilder.AppendLine("   " + "ShootReportShooterAbility".Translate() + ": " + factorFromShooterAndDist.ToStringPercent());
            stringBuilder.AppendLine("   " + "ShootReportWeapon".Translate() + ": " + factorFromEquipment.ToStringPercent());
            if (target.HasThing && factorFromTargetSize != 1f)
            {
                stringBuilder.AppendLine("   " + "TargetSize".Translate() + ": " + factorFromTargetSize.ToStringPercent());
            }
            if (factorFromWeather < 0.99f)
            {
                stringBuilder.AppendLine("   " + "Weather".Translate() + ": " + factorFromWeather.ToStringPercent());
            }
            if (factorFromCoveringGas < 0.99f)
            {
                stringBuilder.AppendLine("   " + "BlindSmoke".Translate().CapitalizeFirst() + ": " + factorFromCoveringGas.ToStringPercent());
            }
            if (FactorFromPosture < 0.9999f)
            {
                stringBuilder.AppendLine("   " + "TargetProne".Translate() + ": " + FactorFromPosture.ToStringPercent());
            }
            if (FactorFromExecution != 1f)
            {
                stringBuilder.AppendLine("   " + "Execution".Translate() + ": " + FactorFromExecution.ToStringPercent());
            }
            if (ModsConfig.IdeologyActive && target.HasThing && offsetFromDarkness != 0f)
            {
                if (DarknessCombatUtility.IsOutdoorsAndLit(target.Thing))
                {
                    stringBuilder.AppendLine("   " + StatDefOf.ShootingAccuracyOutdoorsLitOffset.LabelCap + ": " + offsetFromDarkness.ToStringPercent());
                }
                else if (DarknessCombatUtility.IsOutdoorsAndDark(target.Thing))
                {
                    stringBuilder.AppendLine("   " + StatDefOf.ShootingAccuracyOutdoorsDarkOffset.LabelCap + ": " + offsetFromDarkness.ToStringPercent());
                }
                else if (DarknessCombatUtility.IsIndoorsAndDark(target.Thing))
                {
                    stringBuilder.AppendLine("   " + StatDefOf.ShootingAccuracyIndoorsDarkOffset.LabelCap + ": " + offsetFromDarkness.ToStringPercent());
                }
                else if (DarknessCombatUtility.IsIndoorsAndLit(target.Thing))
                {
                    stringBuilder.AppendLine("   " + StatDefOf.ShootingAccuracyIndoorsLitOffset.LabelCap + "   " + offsetFromDarkness.ToStringPercent());
                }
            }
            if (PassCoverChance < 1f)
            {
                stringBuilder.AppendLine("   " + "ShootingCover".Translate() + ": " + PassCoverChance.ToStringPercent());
                for (int i = 0; i < covers.Count; i++)
                {
                    CoverInfo coverInfo = covers[i];
                    if (coverInfo.BlockChance > 0f)
                    {
                        stringBuilder.AppendLine("     " + "CoverThingBlocksPercentOfShots".Translate(coverInfo.Thing.LabelCap, coverInfo.BlockChance.ToStringPercent(), new NamedArgument(coverInfo.Thing.def, "COVER")).CapitalizeFirst());
                    }
                }
            }
            else
            {
                stringBuilder.AppendLine("   (" + "NoCoverLower".Translate() + ")");
            }
        }
        return stringBuilder.ToString();
    }

    public readonly Thing GetRandomCoverToMissInto()
    {
        if (covers.TryRandomElementByWeight(c => c.BlockChance, out CoverInfo coverInfo))
        {
            return coverInfo.Thing;
        }
        return null;
    }

    private TargetInfo target;

    private float distance;

    private List<CoverInfo> covers;

    private float coversOverallBlockChance;

    private float factorFromShooterAndDist;

    private float factorFromEquipment;

    private float factorFromTargetSize;

    private float factorFromWeather;

    private float forcedMissRadius;

    private float offsetFromDarkness;

    private float factorFromCoveringGas;

    private ShootLine shootLine;
}
