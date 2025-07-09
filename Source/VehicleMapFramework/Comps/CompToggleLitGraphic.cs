using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class CompToggleLitGraphic : CompPowerTrader
{
    new public CompProperties_WirelessCharger Props => (CompProperties_WirelessCharger)props;

    public override void PostDraw()
    {
        base.PostDraw();
        if (PowerOutput != 0f)
        {
            Props.lightGraphic?.Graphic?.Draw(parent.DrawPos.WithYOffset(Altitudes.AltInc), parent.Rotation, parent, parent.IsOnNonFocusedVehicleMapOf(out var vehicle) ? -vehicle.Angle : 0f);
        }
    }

    public bool shouldBeLitNow;
}
