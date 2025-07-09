using UnityEngine;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public static class VMF_Materials
{
    public static Material LoadMat(string matPath)
    {
        var mat = VehicleMapFramework.Bundle.LoadAsset<Material>($"Assets/Data/OELS.VehicleMapFramework/Materials/{matPath}.mat");
        if (mat == null)
        {
            Log.Warning("Could not load material " + mat);
            return BaseContent.BadMat;
        }
        return mat;
    }

    public static readonly Material LightOverlayColorDodge = VMF_Materials.LoadMat("LightOverlayColorDodge");
}
