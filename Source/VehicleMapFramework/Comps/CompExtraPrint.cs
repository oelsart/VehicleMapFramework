using Verse;

namespace VehicleMapFramework;

public class CompExtraPrint : ThingComp
{
    // ReSharper disable once MemberCanBePrivate.Global
    public CompProperties_ExtraPrint Props => (CompProperties_ExtraPrint)props;

    public override void PostPrintOnto(SectionLayer layer)
    {
        if (Props.graphics is null) return;
        foreach (var graphicData in Props.graphics)
        {
            graphicData.Graphic.Print(layer, parent, VehicleMapUtility.PrintExtraRotation(parent));
        }
    }
}
