using System.Linq;
using JetBrains.Annotations;
using LudeonTK;
using RimWorld;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class Reactor_Sink : Reactor, ITweakFields
{
    [TweakField(SettingsType = UISettingsType.SliderFloat)]
    [SliderValues(MinValue = 0f, MaxValue = 1f, Increment = 0.01f, RoundDecimalPlaces = 2)]
    public float chance = 1f;
    
    [TweakField(SettingsType = UISettingsType.SliderFloat)]
    [SliderValues(MinValue = 0f, MaxValue = 1f, Increment = 0.05f, RoundDecimalPlaces = 2)]
    [LoadAlias("maxHealth")]
    public float healthPercent = 1f;

    [TweakField(SettingsType = UISettingsType.FloatBox)]
    public float speed = 5f;

    [TweakField(SettingsType = UISettingsType.FloatBox)]
    public FloatRange angle = new (-5f, 5f);

    [TweakField(SettingsType = UISettingsType.FloatBox)]
    public FloatRange rotationRate = FloatRange.Zero;

    public string overlayId;
    public string resetKey;
    public Color overlayColor;
    public SimpleCurve colorOverlayAlphaCurve;
    
    string ITweakFields.Category => "";

    string ITweakFields.Label => "Reactor_Sink";

    public override void Hit(VehiclePawn vehicle, VehicleComponent component, ref DamageInfo dinfo, VehicleComponent.Penetration penetration)
    {
        if (!vehicle.Spawned)
            return;
        if (component.Health > 0f && component.HealthPercent <= healthPercent && Rand.Chance(chance))
        {
            SpawnMote(vehicle);
            ResetUpgrades(vehicle);
        }
    }

    private void SpawnMote(VehiclePawn vehicle)
    {
        var mote = (FlyingObject)ThingMaker.MakeThing(VMF_DefOf.VMF_MoteSink);
        var overlays = vehicle.DrawTracker.overlayRenderer.AllOverlaysListForReading;
        for (var i = overlays.Count - 1; i >= 0; i--)
        {
            var overlay = overlays[i];
            if (overlay.data.identifier == overlayId)
            {
                var sinker = new SinkingComponent(GraphicOverlay.Create(overlay.data, vehicle), mote, this);
                mote.Add(sinker, vehicle.FullRotation, vehicle.Transform.rotation);
            }
        }
        mote.Launch(vehicle.Map, vehicle.DrawPos, rotationRate.RandomInRange, speed, angle.RandomInRange);
    }

    private void ResetUpgrades(VehiclePawn vehicle)
    {
        if (vehicle.CompUpgradeTree is not { } compUpgradeTree)
            return;
        foreach (var node in compUpgradeTree.Props.def.nodes)
        {
            if (node.key == resetKey && compUpgradeTree.NodeUnlocked(node))
            {
                // TODO VF updates: waiting refundless upgrade reset
                var tmpList = node.ingredients;
                node.ingredients = [];
                compUpgradeTree.ResetUnlock(node);
                node.ingredients = tmpList;
            }
        }
    }
    
    void ITweakFields.OnFieldChanged()
    {
    }
    
    [DebugAction("Vehicle Map Framework", "Sink component", actionType = DebugActionType.ToolMapForPawns)]
    private static void SinkComponent(Pawn pawn)
    {
        if (pawn is not VehiclePawn vehicle)
        {
            SoundDefOf.ClickReject.PlayOneShotOnCamera();
            return;
        }
        foreach (var component in vehicle.statHandler.components)
        {
            if (!component.props.reactors.NullOrEmpty())
            {
                if (component.props.reactors.OfType<Reactor_Sink>().FirstOrDefault() is { } reactor)
                {
                    Log.Message($"{reactor.resetKey}, {reactor.overlayId}");
                    reactor.SpawnMote(vehicle);
                    reactor.ResetUpgrades(vehicle);
                    break;
                }
            }
        }
    }
}
