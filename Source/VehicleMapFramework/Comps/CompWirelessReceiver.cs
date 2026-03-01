using Verse;

namespace VehicleMapFramework;

public class CompWirelessReceiver : CompToggleLitGraphic, IThingGlower
{
    public new CompProperties_WirelessCharger Props => (CompProperties_WirelessCharger)props;

    public override void CompTick()
    {
        if (!parent.Spawned) return;
        base.CompTick();
        if (Find.TickManager.TicksGame % CompWirelessTransmitter.ticksInterval != 0) return;

        if (PowerOutput != 0f && !shouldBeLitNow)
        {
            PowerOutput = 0f;
            if (parent.TryGetComp<CompGlower>(out var comp))
            {
                comp.UpdateLit(parent.Map);
            }
        }
        shouldBeLitNow = false;
    }

    public new bool ShouldBeLitNow()
    {
        return PowerOutput != 0f;
    }
}
