using System.Collections.Generic;
using Verse;

namespace VehicleMapFramework;

public class CompProperties_ExtraPrint : CompProperties
{
    public List<GraphicData> graphics;

    public CompProperties_ExtraPrint()
    {
        compClass = typeof(CompExtraPrint);
    }
}
