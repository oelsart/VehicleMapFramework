using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework;

[HotSwap]
public class CompWirelessTransmitter : CompPowerNetLink, IThingGlower
{
    protected const float PushMin = 0f;
    protected const float PushMax = 5000f;
    private static readonly CachedTexture PowerLowerTex = new("VehicleMapFramework/UI/PowerLower");
    private static readonly CachedTexture PowerRaiseTex = new("VehicleMapFramework/UI/PowerRaise");
    private static readonly CachedTexture PowerResetTex = new("VehicleMapFramework/UI/PowerReset");
    private static readonly CachedTexture Push = new("VehicleMapFramework/UI/Push");
    private static readonly CachedTexture Draw = new("VehicleMapFramework/UI/Draw");
    private static readonly CachedTexture Transmit = new("VehicleMapFramework/UI/Transmit");
    private readonly Color PushColor = new ColorInt(215, 90, 0).ToColor;
    private readonly Color DrawColor = new ColorInt(47, 207, 0).ToColor;
    private readonly Color TransmitColor = new ColorInt(0, 198, 208).ToColor;

    protected PowerTransferMode mode = PowerTransferMode.Transmit;
    
    protected float powerPushSetting = 500f;
    
    public new CompProperties_WirelessTransmitter Props => (CompProperties_WirelessTransmitter)props;

    protected override float Radius => Props.radius;

    protected override float MaxPowerPush => powerPushSetting;

    protected override float PowerLossFactor => Props.powerLossFactor;

    protected override PowerTransferMode Mode => mode;

    protected override bool TryFindConnection(out CompPowerNetLink linkTo)
    {
        
        var num = GenRadial.NumCellsInRadius(Radius);
        var root = parent.Position;
        for (var i = 0; i < num; i++)
        {
            var c = root + GenRadial.RadialPattern[i];
            foreach (var thing in c.GetThingListAcrossMaps(parent.Map))
            {
                if (thing is not ThingWithComps thingWithComps) continue;
                if (thing.Map != parent.Map && thingWithComps.GetComp<CompPowerNetLink>() is { } comp && CanLinkTo(comp))
                {
                    linkTo = comp;
                    return true;
                }
            }
        }
        linkTo = null;
        return false;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (var gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }
        yield return new Command_Action
        {
            action = delegate
            {
                mode = (PowerTransferMode)(((int)mode + 1) % typeof(PowerTransferMode).GetEnumValues().Length);
                Disconnect();
                parent.DrawColor = mode switch
                {
                    PowerTransferMode.Push => PushColor,
                    PowerTransferMode.Draw => DrawColor,
                    _ => TransmitColor
                };
            },
            defaultLabel = mode switch
            {
                PowerTransferMode.Push => "VMF_PowerPush".Translate(),
                PowerTransferMode.Draw => "VMF_PowerDraw".Translate(),
                _ => "VMF_PowerTransmit".Translate()
            },
            defaultDesc = mode switch
            {
                PowerTransferMode.Push => "VMF_PowerPushDesc".Translate(),
                PowerTransferMode.Draw => "VMF_PowerDrawDesc".Translate(),
                _ => "VMF_PowerTransmitDesc".Translate()
            },
            icon = mode switch
            {
                PowerTransferMode.Push => Push.Texture,
                PowerTransferMode.Draw => Draw.Texture,
                _ => Transmit.Texture
            }
        };
        if (mode != PowerTransferMode.Draw)
        {
            yield return new Command_Action
            {
                action = delegate
                {
                    powerPushSetting = Mathf.Clamp(powerPushSetting - 1000f, PushMin, PushMax);
                    MoteMaker.ThrowText(parent.DrawPos, parent.BaseMap(), powerPushSetting.ToString("F0"), Color.white);
                },
                defaultLabel = "-1000W",
                defaultDesc = "VMF_LowerPowerDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc5,
                icon = PowerLowerTex.Texture
            };
            yield return new Command_Action
            {
                action = delegate
                {
                    powerPushSetting = Mathf.Clamp(powerPushSetting - 100f, PushMin, PushMax);
                    MoteMaker.ThrowText(parent.DrawPos, parent.BaseMap(), powerPushSetting.ToString("F0"), Color.white);
                },
                defaultLabel = "-100W",
                defaultDesc = "VMF_LowerPowerDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc4,
                icon = PowerLowerTex.Texture
            };
            yield return new Command_Action
            {
                action = delegate
                {
                    powerPushSetting = 500f;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    MoteMaker.ThrowText(parent.DrawPos, parent.BaseMap(), powerPushSetting.ToString("F0"), Color.white);
                },
                defaultLabel = "VMF_ResetPower".Translate(),
                defaultDesc = "VMF_ResetPowerDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc1,
                icon =  PowerResetTex.Texture
            };
            yield return new Command_Action
            {
                action = delegate
                {
                    powerPushSetting = Mathf.Clamp(powerPushSetting + 100f, PushMin, PushMax);
                    MoteMaker.ThrowText(parent.DrawPos, parent.BaseMap(), powerPushSetting.ToString("F0"), Color.white);
                },
                defaultLabel = "+100W",
                defaultDesc = "VMF_RaisePowerDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc2,
                icon =  PowerRaiseTex.Texture
            };
            yield return new Command_Action
            {
                action = delegate
                {
                    powerPushSetting = Mathf.Clamp(powerPushSetting + 1000f, PushMin, PushMax);
                    MoteMaker.ThrowText(parent.DrawPos, parent.BaseMap(), powerPushSetting.ToString("F0"), Color.white);
                },
                defaultLabel = "+1000W",
                defaultDesc = "VMF_RaisePowerDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc3,
                icon = PowerRaiseTex.Texture
            };
        }
    }
    
    public override void PostDraw()
    {
        base.PostDraw();
        if (Connected && Props.lightGraphic?.Graphic is { } graphic)
        {
            var colored = PowerOutput != 0f ? graphic : graphic.GetColoredVersion(graphic.Shader, graphic.Color.WithAlpha(0.5f), graphic.ColorTwo);
            colored.Draw(parent.DrawPos.WithYOffset(Altitudes.AltInc / (parent.IsOnNonFocusedVehicleMap ? 10f : 1f)), parent.Rotation, parent, parent.IsOnNonFocusedVehicleMapOf(out var vehicle) ? -vehicle.Angle : 0f);
        }
    }

    public override void PostDrawExtraSelectionOverlays()
    {
        base.PostDrawExtraSelectionOverlays();
        GenDraw.DrawRadiusRing(parent.Position, Props.radius);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref mode, nameof(mode), PowerTransferMode.Transmit);
        Scribe_Values.Look(ref powerPushSetting, nameof(powerPushSetting), 500f);
    }

    public override string CompInspectStringExtra()
    {
        var str = base.CompInspectStringExtra() + "\n";
        str += $"{"VMF_PowerTransferSetting".Translate()}: {powerPushSetting} W";
        return str;
    }

    bool IThingGlower.ShouldBeLitNow()
    {
        return PowerOutput != 0f;
    }
}
