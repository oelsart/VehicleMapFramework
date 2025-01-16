using Vehicles;

namespace VehicleInteriors
{
    public class RGBMaskOpacityDef : PatternDef
    {
        public override RGBShaderTypeDef ShaderTypeDef => VMF_DefOf.VMF_CutoutComplexRGBOpacity;
    }
}
