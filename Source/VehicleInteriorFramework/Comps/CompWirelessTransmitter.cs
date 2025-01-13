using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VehicleInteriors
{
    public class CompWirelessTransmitter : CompToggleLitGraphic
    {
        new public CompProperties_WirelessCharger Props => (CompProperties_WirelessCharger)this.props;

        public override void CompTick()
        {
            base.CompTick();
            if (Find.TickManager.TicksGame % ticksInterval != 0) return;

            var rect = CellRect.SingleCell(this.parent.Position);
            foreach (var c in rect.ExpandedBy(1).Cells)
            {
                if (!c.InBounds(this.parent.Map)) continue;
                if (c.ToVector3Shifted().TryGetVehicleMap(this.parent.Map, out var vehicle))
                {
                    var c2 = c.VehicleMapToOrig(vehicle);
                    if (!c2.InBounds(vehicle.VehicleMap)) continue;

                    var receiver = c2.GetFirstThingWithComp<CompWirelessReceiver>(vehicle.VehicleMap);
                    if (receiver != null)
                    {
                        var compReceiver = receiver.GetComp<CompWirelessReceiver>();
                        if (!this.PowerOn)
                        {
                            compReceiver.PowerOutput = 0f;
                            return;
                        }

                        var powerNet = compReceiver.PowerNet;
                        var powerComps = compReceiver.PowerNet.powerComps.Where(p => p != compReceiver);
                        if (powerComps.Any(p => !p.PowerOn && FlickUtility.WantsToBeOn(p.parent) && !p.parent.IsBrokenDown()))
                        {
                            this.PowerOutput = Mathf.Max(this.PowerOutput - 10f, -this.powerOutputSetting);
                        }
                        else
                        {
                            var sumBatteriesDiscarge = powerNet.batteryComps.Count * 5f;
                            var needs = (powerNet.batteryComps.Sum(b => b.AmountCanAccept) - powerComps.Sum(p => p.EnergyOutputPerTick)) / CompPower.WattsToWattDaysPerTick + sumBatteriesDiscarge;
                            this.PowerOutput = -Mathf.Clamp(needs / this.Props.powerLossFactor + 1E-07f, sumBatteriesDiscarge, this.powerOutputSetting);
                        }
                        compReceiver.shouldBeLitNow = true;
                        this.shouldBeLitNow = true;
                        if (compReceiver.PowerOutput == 0f)
                        {
                            if (receiver.TryGetComp<CompGlower>(out var compGlower))
                            {
                                compGlower.UpdateLit(receiver.Map);
                            }
                        }
                        compReceiver.PowerOutput = -this.PowerOutput * this.Props.powerLossFactor;
                        return;
                    }
                }
            }
            this.PowerOutput = 0f;
            this.shouldBeLitNow = false;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            yield return new Command_Action
            {
                action = delegate ()
                {
                    this.powerOutputSetting = Mathf.Clamp(this.powerOutputSetting - 1000f, minPowerOutput, maxPowerOutput);
                    MoteMaker.ThrowText(this.parent.DrawPos, this.parent.BaseMap(), this.powerOutputSetting.ToString(), Color.white, -1f);
                },
                defaultLabel = "-1000W",
                defaultDesc = "VIF_LowerPowerDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc5,
                icon = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/PowerLower", true)
            };
            yield return new Command_Action
            {
                action = delegate ()
                {
                    this.powerOutputSetting = Mathf.Clamp(this.powerOutputSetting - 100f, minPowerOutput, maxPowerOutput);
                    MoteMaker.ThrowText(this.parent.DrawPos, this.parent.BaseMap(), this.powerOutputSetting.ToString(), Color.white, -1f);
                },
                defaultLabel = "-100W",
                defaultDesc = "VIF_LowerPowerDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc4,
                icon = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/PowerLower", true)
            };
            yield return new Command_Action
            {
                action = delegate ()
                {
                    this.powerOutputSetting = 500f;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                    MoteMaker.ThrowText(this.parent.DrawPos, this.parent.BaseMap(), this.powerOutputSetting.ToString(), Color.white, -1f);
                },
                defaultLabel = "VIF_ResetPower".Translate(),
                defaultDesc = "VIF_ResetPowerDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc1,
                icon = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/PowerReset", true)
            };
            yield return new Command_Action
            {
                action = delegate ()
                {
                    this.powerOutputSetting = Mathf.Clamp(this.powerOutputSetting + 100f, minPowerOutput, maxPowerOutput);
                    MoteMaker.ThrowText(this.parent.DrawPos, this.parent.BaseMap(), this.powerOutputSetting.ToString(), Color.white, -1f);
                },
                defaultLabel = "+100W",
                defaultDesc = "VIF_RaisePowerDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc2,
                icon = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/PowerRaise", true)
            };
            yield return new Command_Action
            {
                action = delegate ()
                {
                    this.powerOutputSetting = Mathf.Clamp(this.powerOutputSetting + 1000f, minPowerOutput, maxPowerOutput);
                    MoteMaker.ThrowText(this.parent.DrawPos, this.parent.BaseMap(), this.powerOutputSetting.ToString(), Color.white, -1f);
                },
                defaultLabel = "+1000W",
                defaultDesc = "VIF_RaisePowerDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc3,
                icon = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/PowerRaise", true)
            };
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            var rect = CellRect.SingleCell(this.parent.Position);
            GenDraw.DrawFieldEdges(rect.ExpandedBy(1).Cells.ToList());
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref this.powerOutputSetting, "powerOutputSetting", 500f);
        }

        public override string CompInspectStringExtra()
        {
            var str = base.CompInspectStringExtra() + "\n";
            str += $"{"VIF_PowerTransferSetting".Translate()}: {this.powerOutputSetting} W";
            return str;
        }

        private float powerOutputSetting = 500f;

        private const float minPowerOutput = 0f;

        private const float maxPowerOutput = 10000f;

        public const int ticksInterval = 20;
    }
}
