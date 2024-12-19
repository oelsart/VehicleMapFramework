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
            this.PowerOutput = 0f;
            this.shouldBeLitNow = false;
            var rect = CellRect.SingleCell(this.parent.Position);
            foreach (var c in rect.ExpandedBy(1).Cells)
            {
                if (!c.InBounds(this.parent.Map)) continue;
                if (c.ToVector3Shifted().TryGetVehiclePawnWithMap(out var vehicle))
                {
                    var c2 = c.VehicleMapToOrig(vehicle);
                    if (!c2.InBounds(vehicle.interiorMap)) continue;

                    var receiver = c2.GetFirstThingWithComp<CompWirelessReceiver>(vehicle.interiorMap);
                    if (receiver != null)
                    {
                        this.PowerOutput = -this.powerOutputSetting;
                        var compReceiver = receiver.GetComp<CompWirelessReceiver>();
                        compReceiver.shouldBeLitNow = true;
                        this.shouldBeLitNow = true;
                        if (compReceiver.PowerOutput == 0f)
                        {
                            compReceiver.PowerOutput = this.powerOutputSetting * this.Props.powerLossFactor;
                            if (receiver.TryGetComp<CompGlower>(out var compGlower))
                            {
                                compGlower.UpdateLit(receiver.Map);
                            }
                        }
                    }
                }
            }
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
                defaultDesc = "VIF.LowerPowerDesc".Translate(),
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
                defaultDesc = "VIF.LowerPowerDesc".Translate(),
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
                defaultLabel = "VIF.ResetPower".Translate(),
                defaultDesc = "VIF.ResetPowerDesc".Translate(),
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
                defaultDesc = "VIF.RaisePowerDesc".Translate(),
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
                defaultDesc = "VIF.RaisePowerDesc".Translate(),
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
            str += $"{"VIF.PowerTransferSetting".Translate()}: {this.powerOutputSetting} W";
            return str;
        }

        private float powerOutputSetting = 500f;

        private const float minPowerOutput = 0f;

        private const float maxPowerOutput = 10000f;
    }
}
