using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class RGBMaskOpacityDef : PatternDef
    {
        public override ShaderTypeDef ShaderTypeDef => VMF_DefOf.VMF_CutoutComplexRGBOpacity;
    }
}
