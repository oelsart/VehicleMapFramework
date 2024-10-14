﻿using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public struct ShotReportOnVehicle
    {
        private float FactorFromPosture
        {
            get
            {
                Pawn p;
                if (this.target.HasThing && (p = (this.target.Thing as Pawn)) != null && this.distance >= 4.5f && p.GetPosture() != PawnPosture.Standing)
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
                Pawn p;
                if (this.target.HasThing && (p = (this.target.Thing as Pawn)) != null && this.distance <= 3.9f && p.GetPosture() != PawnPosture.Standing)
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
                float num = this.factorFromShooterAndDist * this.factorFromEquipment * this.factorFromWeather * this.factorFromCoveringGas * this.FactorFromExecution;
                num += this.offsetFromDarkness;
                if (num < 0.0201f)
                {
                    num = 0.0201f;
                }
                return num;
            }
        }

        public float AimOnTargetChance_IgnoringPosture
        {
            get
            {
                return this.AimOnTargetChance_StandardTarget * this.factorFromTargetSize;
            }
        }

        public float AimOnTargetChance
        {
            get
            {
                return this.AimOnTargetChance_IgnoringPosture * this.FactorFromPosture;
            }
        }

        public float PassCoverChance
        {
            get
            {
                return 1f - this.coversOverallBlockChance;
            }
        }

        public float TotalEstimatedHitChance
        {
            get
            {
                return Mathf.Clamp01(this.AimOnTargetChance * this.PassCoverChance);
            }
        }

        public ShootLine ShootLine
        {
            get
            {
                return this.shootLine;
            }
        }

        public static ShotReportOnVehicle HitReportFor(Thing caster, Verb verb, LocalTargetInfo target)
        {
            Map targetMap = target.HasThing ? target.Thing.Map : caster.BaseMapOfThing();
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
                        if (enumerator.Current.AnyGas(caster.BaseMapOfThing(), GasType.BlindSmoke))
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
            if (!caster.PositionOnBaseMap().Roofed(caster.BaseMapOfThing()) || !target.CellOnBaseMap().Roofed(caster.BaseMapOfThing()))
            {
                shotReportOnVehicle.factorFromWeather = caster.Map.weatherManager.CurWeatherAccuracyMultiplier;
            }
            else
            {
                shotReportOnVehicle.factorFromWeather = 1f;
            }
            if (target.HasThing)
            {
                Pawn pawn;
                if ((pawn = (target.Thing as Pawn)) != null)
                {
                    shotReportOnVehicle.factorFromTargetSize = pawn.BodySize;
                }
                else
                {
                    shotReportOnVehicle.factorFromTargetSize = target.Thing.def.fillPercent * (float)target.Thing.def.size.x * (float)target.Thing.def.size.z * 2.5f;
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
            StringBuilder stringBuilder = new StringBuilder();
            if (this.forcedMissRadius > 0.5f)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("WeaponMissRadius".Translate() + ": " + this.forcedMissRadius.ToString("F1"));
                stringBuilder.AppendLine("DirectHitChance".Translate() + ": " + (1f / (float)GenRadial.NumCellsInRadius(this.forcedMissRadius)).ToStringPercent());
            }
            else
            {
                stringBuilder.AppendLine(this.TotalEstimatedHitChance.ToStringPercent());
                stringBuilder.AppendLine("   " + "ShootReportShooterAbility".Translate() + ": " + this.factorFromShooterAndDist.ToStringPercent());
                stringBuilder.AppendLine("   " + "ShootReportWeapon".Translate() + ": " + this.factorFromEquipment.ToStringPercent());
                if (this.target.HasThing && this.factorFromTargetSize != 1f)
                {
                    stringBuilder.AppendLine("   " + "TargetSize".Translate() + ": " + this.factorFromTargetSize.ToStringPercent());
                }
                if (this.factorFromWeather < 0.99f)
                {
                    stringBuilder.AppendLine("   " + "Weather".Translate() + ": " + this.factorFromWeather.ToStringPercent());
                }
                if (this.factorFromCoveringGas < 0.99f)
                {
                    stringBuilder.AppendLine("   " + "BlindSmoke".Translate().CapitalizeFirst() + ": " + this.factorFromCoveringGas.ToStringPercent());
                }
                if (this.FactorFromPosture < 0.9999f)
                {
                    stringBuilder.AppendLine("   " + "TargetProne".Translate() + ": " + this.FactorFromPosture.ToStringPercent());
                }
                if (this.FactorFromExecution != 1f)
                {
                    stringBuilder.AppendLine("   " + "Execution".Translate() + ": " + this.FactorFromExecution.ToStringPercent());
                }
                if (ModsConfig.IdeologyActive && this.target.HasThing && this.offsetFromDarkness != 0f)
                {
                    if (DarknessCombatUtility.IsOutdoorsAndLit(this.target.Thing))
                    {
                        stringBuilder.AppendLine("   " + StatDefOf.ShootingAccuracyOutdoorsLitOffset.LabelCap + ": " + this.offsetFromDarkness.ToStringPercent());
                    }
                    else if (DarknessCombatUtility.IsOutdoorsAndDark(this.target.Thing))
                    {
                        stringBuilder.AppendLine("   " + StatDefOf.ShootingAccuracyOutdoorsDarkOffset.LabelCap + ": " + this.offsetFromDarkness.ToStringPercent());
                    }
                    else if (DarknessCombatUtility.IsIndoorsAndDark(this.target.Thing))
                    {
                        stringBuilder.AppendLine("   " + StatDefOf.ShootingAccuracyIndoorsDarkOffset.LabelCap + ": " + this.offsetFromDarkness.ToStringPercent());
                    }
                    else if (DarknessCombatUtility.IsIndoorsAndLit(this.target.Thing))
                    {
                        stringBuilder.AppendLine("   " + StatDefOf.ShootingAccuracyIndoorsLitOffset.LabelCap + "   " + this.offsetFromDarkness.ToStringPercent());
                    }
                }
                if (this.PassCoverChance < 1f)
                {
                    stringBuilder.AppendLine("   " + "ShootingCover".Translate() + ": " + this.PassCoverChance.ToStringPercent());
                    for (int i = 0; i < this.covers.Count; i++)
                    {
                        CoverInfo coverInfo = this.covers[i];
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

        public Thing GetRandomCoverToMissInto()
        {
            CoverInfo coverInfo;
            if (this.covers.TryRandomElementByWeight((CoverInfo c) => c.BlockChance, out coverInfo))
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
}