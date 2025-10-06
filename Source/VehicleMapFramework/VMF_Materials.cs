using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
internal static class VMF_Materials
{
    public static readonly Material LightOverlayColorDodge = LoadMat("LightOverlayColorDodge");

    private static Material LoadMat(string matPath)
    {
        foreach (var mat in VehicleMapFramework.mod.Content.assetBundles.loadedAssetBundles
                     .Select(bundle => bundle.LoadAsset<Material>($"Assets/Data/{VehicleMapFramework.mod.Content.PackageIdPlayerFacing}/Materials/{matPath}.mat"))
                     .Where(mat => mat != null))
        {
            return mat;
        }
        Log.Warning("Could not load material " + matPath);
        return BaseContent.BadMat;
    }
}
