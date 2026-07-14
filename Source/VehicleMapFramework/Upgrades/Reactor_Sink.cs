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
        if (vehicle is not VehiclePawnWithMap { CompUpgradeTree: { } compUpgradeTree } vehiclePawnWithMap)
            return;

        ExpansionUpgrade upgradeToReset = null;
        foreach (var node in compUpgradeTree.Props.def.nodes)
        {
            if (node.key == resetKey && compUpgradeTree.NodeUnlocked(node))
            {
                upgradeToReset = node.upgrades.OfType<ExpansionUpgrade>().FirstOrDefault();
                if (upgradeToReset is not null) break;
            }
        }
        var drawPos = vehicle.DrawPos;
        var overlays = vehicle.DrawTracker.overlayRenderer.AllOverlaysListForReading;
        CellRect? mapLimit = null;
        if (upgradeToReset is not null)
        {
            foreach (var cellRect in upgradeToReset.expandAreas)
            {
                mapLimit = mapLimit?.Encapsulate(cellRect.MovedBy(IntVec2.One)) ?? cellRect.MovedBy(IntVec2.One);
            }
        }
        mapLimit ??= CellRect.Empty;

        var rot = vehicle.FullRotation.RotForVehicleDraw();
        for (var i = overlays.Count - 1; i >= 0; i--)
        {
            var overlay = overlays[i];
            if (overlay.data.identifier == overlayId)
            {
                var mote = (MoteThrownSinker)ThingMaker.MakeThing(VMF_DefOf.VMF_MoteSink);
                var drawSize = overlay.Graphic.drawSize;
                var drawSizeRotated = rot.IsHorizontal ? drawSize.Rotated() : drawSize;
                var textureSize = (Mathf.CeilToInt(drawSizeRotated.x * 256), Mathf.CeilToInt(drawSizeRotated.y * 256));
                var texture = VehicleMapUIRenderer.GetOverlayWithVehicleMapTexture(
                    vehiclePawnWithMap,
                    overlay,
                    rot,
                    textureSize,
                    mapLimit.Value);
                mote.SetParameters(
                    texture,
                    Quaternion.AngleAxis(-vehicle.ExtraAngle, Vector3.up),
                    drawSizeRotated.ToVector3().WithY(1f),
                    textureSize,
                    vehiclePawnWithMap,
                    overlay,
                    overlayColor,
                    colorOverlayAlphaCurve);
                mote.SetVelocity(angle.RandomInRange, speed);
                mote.exactPosition = drawPos +
                                     overlay.Graphic.DrawOffset(rot).RotatedBy(vehicle.ExtraAngle);
                GenSpawn.Spawn(mote, mote.exactPosition.ToIntVec3(), vehicle.Map);
            }
        }
    }

    private void ResetUpgrades(VehiclePawn vehicle)
    {
        if (vehicle.CompUpgradeTree is not { } compUpgradeTree)
            return;
        foreach (var node in compUpgradeTree.Props.def.nodes)
        {
            if (node.key == resetKey && compUpgradeTree.NodeUnlocked(node))
            {
                // TODO with VF updates: waiting refundless upgrade reset
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
    
    [DebugAction(VehicleMapFramework.CategoryName, "Sink component", actionType = DebugActionType.ToolMapForPawns)]
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
                    reactor.SpawnMote(vehicle);
                    reactor.ResetUpgrades(vehicle);
                    break;
                }
            }
        }
    }
}
