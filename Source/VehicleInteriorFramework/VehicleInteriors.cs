using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class VehicleInteriors : Mod
    {
        public static VehicleInteriors Mod { get; private set; }

        public static AssetBundle Bundle => VehicleInteriors.Mod.Content.assetBundles.loadedAssetBundles.Find(a => a.name == "vehicleinteriors");

        public VehicleInteriors(ModContentPack content) : base(content)
        {
            VehicleInteriors.Mod = this;
        }
    }
}
