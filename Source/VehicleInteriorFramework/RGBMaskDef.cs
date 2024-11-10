using Vehicles;

namespace VehicleInteriors
{
    public class RGBMaskDef : PatternDef
    {
        public override RGBShaderTypeDef ShaderTypeDef
        {
            get
            {
                return VIF_DefOf.VIF_CutoutComplexRGBOpacity;
            }
        }
    }
}
