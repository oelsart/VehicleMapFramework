using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class RGBMaskOpacityDef : PatternDef
{
    public override ShaderTypeDef ShaderTypeDef => VMF_DefOf.VMF_CutoutComplexRGBOpacity;
}
