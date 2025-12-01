using RimWorld;
using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class CompFuelTank : CompRefuelable
{
    public VehiclePawnWithMap Vehicle => parent.IsOnVehicleMapOf(out var vehicle) ? vehicle : null;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            if (parent.IsOnVehicleMapOf(out var vehicle))
            {
                vehicle.FuelTankComps.Add(this);
                if (ModCompat.VGE && vehicle.def.HasModExtension<VehicleMapProps_Gravship>())
                {
                    FilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.2f, 0.5f));
                }
                else
                {
                    FilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.4f, 0.25f, 0.1f));
                }
            }
        });
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        if (map.IsVehicleMapOf(out var vehicle))
        {
            vehicle.FuelTankComps.Remove(this);
        }
    }

    public override void PostDraw()
    {
        if (parent.IsOnVehicleMapOf(out var vehicle) && vehicle.CompFueledTravel != null)
        {
            var rot = Vehicle.FullRotation.RotForVehicleDraw();
            if (!rot.IsHorizontal) rot = rot.Opposite;
            if ((parent.Position + rot.FacingCell).GetFirstThing(parent.Map, parent.def) != null)
            {
                return;
            }
            GenDraw.FillableBarRequest r = new()
            {
                center = parent.DrawPos + DrawOffset.RotatedBy(-vehicle.Angle + vehicle.Transform.rotation) + (Vector3.down * 0.015f),
                size = BarSize,
                fillPercent = vehicle.CompFueledTravel.FuelPercent,
                filledMat = FilledMat,
                unfilledMat = UnfilledMat,
                margin = 0.03f,
                rotation = Rot8.FromAngle(Mathf.Repeat(-vehicle.Angle, 360f)).AsRot4Force()
            };
            Rot8Utility.Rotate(ref r.rotation, RotationDirection.Clockwise);
            GenDraw.DrawFillableBar(r);
        }
    }

    private static readonly Vector3 DrawOffset = new(0.0015f, 0.1f, -0.3125f);

    private static readonly Vector2 BarSize = new(0.15f, 0.18f);

    private Material FilledMat;

    private static readonly Material UnfilledMat = BaseContent.ClearMat;
}
