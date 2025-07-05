using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public static class ShaderUtility
    {
        public static bool SupportsOpacity(this Shader shader)
        {
            return shader == VMF_DefOf.VMF_CutoutComplexRGBOpacity.Shader ||
                shader == VMF_DefOf.VMF_CutoutComplexPatternOpacity.Shader ||
                shader == VMF_DefOf.VMF_CutoutComplexSkinOpacity.Shader;
        }

        public static ShaderTypeDef OpacityShaderTypeDefCorrespond(this ShaderTypeDef shaderTypeDef)
        {
            if (shaderTypeDef == VehicleShaderTypeDefOf.CutoutComplexRGB)
            {
                return VMF_DefOf.VMF_CutoutComplexRGBOpacity;
            }
            if (shaderTypeDef == VehicleShaderTypeDefOf.CutoutComplexPattern)
            {
                return VMF_DefOf.VMF_CutoutComplexPatternOpacity;
            }
            if (shaderTypeDef == VehicleShaderTypeDefOf.CutoutComplexSkin)
            {
                return VMF_DefOf.VMF_CutoutComplexSkinOpacity;
            }
            return shaderTypeDef;
        }

        public static Shader OpacityShaderCorrespond(this Shader shader)
        {
            if (shader == VehicleShaderTypeDefOf.CutoutComplexRGB.Shader)
            {
                return VMF_DefOf.VMF_CutoutComplexRGBOpacity.Shader;
            }
            if (shader == VehicleShaderTypeDefOf.CutoutComplexPattern.Shader)
            {
                return VMF_DefOf.VMF_CutoutComplexPatternOpacity.Shader;
            }
            if (shader == VehicleShaderTypeDefOf.CutoutComplexSkin.Shader)
            {
                return VMF_DefOf.VMF_CutoutComplexSkinOpacity.Shader;
            }
            return shader;
        }
    }
}
