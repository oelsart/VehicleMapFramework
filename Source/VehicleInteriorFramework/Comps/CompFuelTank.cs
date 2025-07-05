using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors;

[StaticConstructorOnStartup]
public class CompFuelTank : ThingComp
{
    public VehiclePawnWithMap Vehicle
    {
        get
        {
            if (vehicle == null)
            {
                if (!parent.IsOnVehicleMapOf(out vehicle))
                {
                    VMF_Log.Error("Fuel tank is not on vehicle map.");
                }
            }
            return vehicle;
        }
    }

    public override void PostDraw()
    {
        CompFueledTravel comp;
        if ((comp = Vehicle?.CompFueledTravel) != null)
        {
            var rot = Vehicle.FullRotation.RotForVehicleDraw();
            if (!rot.IsHorizontal) rot = rot.Opposite;
            if ((parent.Position + rot.FacingCell).GetFirstThing(parent.Map, parent.def) != null)
            {
                return;
            }
            GenDraw.FillableBarRequest r = new()
            {
                center = parent.DrawPos + DrawOffset.RotatedBy(-vehicle.Angle) + (Vector3.down * 0.015f),
                size = BarSize,
                fillPercent = comp.FuelPercent,
                filledMat = FilledMat,
                unfilledMat = UnfilledMat,
                margin = 0.03f,
                rotation = parent.BaseFullRotationAsRot4()
            };
            //中にRot8が入ってるのでIsHorizontalは使えません
            if (r.rotation == Rot4.East || r.rotation == Rot4.West)
            {
                r.rotation = Rot4.North;
            }
            Rot8Utility.Rotate(ref r.rotation, RotationDirection.Clockwise);
            GenDraw.DrawFillableBar(r);
        }
    }

    private VehiclePawnWithMap vehicle;

    private static readonly Vector3 DrawOffset = new(0.0015f, 0.1f, -0.3125f);

    private static readonly Vector2 BarSize = new(0.15f, 0.18f);

    private static readonly Material FilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.4f, 0.25f, 0.1f));

    private static readonly Material UnfilledMat = BaseContent.ClearMat;
}
