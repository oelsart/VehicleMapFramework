using RimWorld;
using Verse;

namespace VehicleInteriors
{
    public class CompToggleLitGraphic : CompPowerTrader
    {
        new public CompProperties_WirelessCharger Props => (CompProperties_WirelessCharger)this.props;

        public override void PostDraw()
        {
            base.PostDraw();
            if (this.PowerOutput != 0f)
            {
                this.Props.lightGraphic?.Graphic?.Draw(this.parent.DrawPos.WithYOffset(Altitudes.AltInc), this.parent.Rotation, this.parent, this.parent.IsOnNonFocusedVehicleMapOf(out var vehicle) ? -vehicle.Angle : 0f);
            }
        }

        public bool shouldBeLitNow;
    }
}
