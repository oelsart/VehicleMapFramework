using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class VehicleInteriors : Mod
    {
        public static AssetBundle Bundle => VehicleInteriors.mod.Content.assetBundles.loadedAssetBundles.Find(a => a.name == "vehicleinteriors");

        public VehicleInteriors(ModContentPack content) : base(content)
        {
            VehicleInteriors.mod = this;
        }

        public static VehicleInteriors mod;
    }
}
