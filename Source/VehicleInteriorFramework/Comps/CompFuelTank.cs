using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    [StaticConstructorOnStartup]
    public class CompFuelTank : ThingComp
    {
        public override void PostDraw()
        {
            CompFueledTravel comp;
            if (this.parent.IsOnVehicleMapOf(out var vehicle) && (comp = vehicle.CompFueledTravel) != null)
            {
                GenDraw.FillableBarRequest r = new GenDraw.FillableBarRequest
                {
                    center = this.parent.DrawPos + DrawOffset,
                    size = BarSize,
                    fillPercent = comp.FuelPercent,
                    filledMat = FilledMat,
                    unfilledMat = UnfilledMat,
                    margin = 0.01f,
                    rotation = this.parent.BaseFullRotationAsRot4()
                };
                GenDraw.DrawFillableBar(r);
            }
        }

        private static readonly Vector3 DrawOffset = new Vector3(0.0015f, 0.1f, -0.3125f);

        private static readonly Vector2 BarSize = new Vector2(0.15f, 0.18f);

        private static readonly Material FilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.4f, 0.25f, 0.1f));

        private static readonly Material UnfilledMat = BaseContent.ClearMat;
    }
}
