using Verse;

namespace VehicleMapFramework;

public class CompExtraPrint : ThingComp
{
    public CompProperties_ExtraPrint Props => (CompProperties_ExtraPrint)props;

    public override void PostPrintOnto(SectionLayer layer)
    {
        if (Props.graphicDatas is null) return;
        foreach (var graphicData in Props.graphicDatas)
        {
            graphicData.Graphic.Print(layer, parent, VehicleMapUtility.PrintExtraRotation(parent));
        }
    }
}
