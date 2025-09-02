using UnityEngine;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
internal static class VMF_Materials
{
    public static readonly Material LightOverlayColorDodge = LoadMat("LightOverlayColorDodge");

    public static Material LoadMat(string matPath)
    {
        foreach (var bundle in VehicleMapFramework.mod.Content.assetBundles.loadedAssetBundles)
        {
            var mat = bundle.LoadAsset<Material>($"Assets/Data/{VehicleMapFramework.mod.Content.PackageIdPlayerFacing}/Materials/{matPath}.mat");
            if (mat != null)
            {
                return mat;
            }
        }
        Log.Warning("Could not load material " + matPath);
        return BaseContent.BadMat;
    }
}
