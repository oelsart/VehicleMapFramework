using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework;

public class CompWirelessTransmitter : CompToggleLitGraphic
{
    new public CompProperties_WirelessCharger Props => (CompProperties_WirelessCharger)props;

    public override void CompTick()
    {
        base.CompTick();
        if (Find.TickManager.TicksGame % ticksInterval != 0) return;

        foreach (var c in parent.OccupiedRect().ExpandedBy(1).Cells)
        {
            if (!c.InBounds(parent.Map)) continue;
            if (c.TryGetVehicleMap(parent.Map, out var vehicle))
            {
                var c2 = c.ToVehicleMapCoord(vehicle);
                if (!c2.InBounds(vehicle.VehicleMap)) continue;

                var receiver = c2.GetFirstThingWithComp<CompWirelessReceiver>(vehicle.VehicleMap);
                if (receiver != null)
                {
                    var compReceiver = receiver.GetComp<CompWirelessReceiver>();
                    if (!PowerOn)
                    {
                        compReceiver.PowerOutput = 0f;
                        return;
                    }

                    var powerNet = compReceiver.PowerNet;
                    var powerComps = compReceiver.PowerNet.powerComps.Where(p => p != compReceiver);
                    if (powerComps.Any(p => !p.PowerOn && FlickUtility.WantsToBeOn(p.parent) && !p.parent.IsBrokenDown()))
                    {
                        PowerOutput = Mathf.Max(PowerOutput - 10f, -powerOutputSetting);
                    }
                    else
                    {
                        var sumBatteriesDiscarge = powerNet.batteryComps.Count * 5f;
                        var needs = ((powerNet.batteryComps.Sum(b => b.AmountCanAccept) - powerComps.Sum(p => p.EnergyOutputPerTick)) / WattsToWattDaysPerTick) + sumBatteriesDiscarge;
                        PowerOutput = -Mathf.Clamp((needs / Props.powerLossFactor) + 1E-07f, sumBatteriesDiscarge, powerOutputSetting);
                    }
                    compReceiver.shouldBeLitNow = true;
                    shouldBeLitNow = true;
                    if (compReceiver.PowerOutput == 0f)
                    {
                        if (receiver.TryGetComp<CompGlower>(out var compGlower))
                        {
                            compGlower.UpdateLit(receiver.Map);
                        }
                    }
                    compReceiver.PowerOutput = -PowerOutput * Props.powerLossFactor;
                    return;
                }
            }
        }
        PowerOutput = 0f;
        shouldBeLitNow = false;
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
                powerOutputSetting = Mathf.Clamp(powerOutputSetting - 1000f, minPowerOutput, maxPowerOutput);
                MoteMaker.ThrowText(parent.DrawPos, parent.BaseMap(), powerOutputSetting.ToString(), Color.white, -1f);
            },
            defaultLabel = "-1000W",
            defaultDesc = "VMF_LowerPowerDesc".Translate(),
            hotKey = KeyBindingDefOf.Misc5,
            icon = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/PowerLower", true)
        };
        yield return new Command_Action
        {
            action = delegate ()
            {
                powerOutputSetting = Mathf.Clamp(powerOutputSetting - 100f, minPowerOutput, maxPowerOutput);
                MoteMaker.ThrowText(parent.DrawPos, parent.BaseMap(), powerOutputSetting.ToString(), Color.white, -1f);
            },
            defaultLabel = "-100W",
            defaultDesc = "VMF_LowerPowerDesc".Translate(),
            hotKey = KeyBindingDefOf.Misc4,
            icon = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/PowerLower", true)
        };
        yield return new Command_Action
        {
            action = delegate ()
            {
                powerOutputSetting = 500f;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                MoteMaker.ThrowText(parent.DrawPos, parent.BaseMap(), powerOutputSetting.ToString(), Color.white, -1f);
            },
            defaultLabel = "VMF_ResetPower".Translate(),
            defaultDesc = "VMF_ResetPowerDesc".Translate(),
            hotKey = KeyBindingDefOf.Misc1,
            icon = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/PowerReset", true)
        };
        yield return new Command_Action
        {
            action = delegate ()
            {
                powerOutputSetting = Mathf.Clamp(powerOutputSetting + 100f, minPowerOutput, maxPowerOutput);
                MoteMaker.ThrowText(parent.DrawPos, parent.BaseMap(), powerOutputSetting.ToString(), Color.white, -1f);
            },
            defaultLabel = "+100W",
            defaultDesc = "VMF_RaisePowerDesc".Translate(),
            hotKey = KeyBindingDefOf.Misc2,
            icon = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/PowerRaise", true)
        };
        yield return new Command_Action
        {
            action = delegate ()
            {
                powerOutputSetting = Mathf.Clamp(powerOutputSetting + 1000f, minPowerOutput, maxPowerOutput);
                MoteMaker.ThrowText(parent.DrawPos, parent.BaseMap(), powerOutputSetting.ToString(), Color.white, -1f);
            },
            defaultLabel = "+1000W",
            defaultDesc = "VMF_RaisePowerDesc".Translate(),
            hotKey = KeyBindingDefOf.Misc3,
            icon = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/PowerRaise", true)
        };
    }

    public override void PostDrawExtraSelectionOverlays()
    {
        var rect = CellRect.SingleCell(parent.Position);
        GenDraw.DrawFieldEdges([.. rect.ExpandedBy(1).Cells]);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref powerOutputSetting, "powerOutputSetting", 500f);
    }

    public override string CompInspectStringExtra()
    {
        var str = base.CompInspectStringExtra() + "\n";
        str += $"{"VMF_PowerTransferSetting".Translate()}: {powerOutputSetting} W";
        return str;
    }

    private float powerOutputSetting = 500f;

    private const float minPowerOutput = 0f;

    private const float maxPowerOutput = 10000f;

    public const int ticksInterval = 30;
}
