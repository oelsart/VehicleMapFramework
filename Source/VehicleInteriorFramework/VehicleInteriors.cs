using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class VehicleInteriors : Mod
    {
        public static VehicleInteriors mod;

        public static VehicleMapSettings settings;

        public static AssetBundle Bundle => VehicleInteriors.mod.Content.assetBundles.loadedAssetBundles.Find(a => a.name == "vehicleinteriors");

        public VehicleInteriors(ModContentPack content) : base(content)
        {
            VehicleInteriors.mod = this;
            VehicleInteriors.settings = base.GetSettings<VehicleMapSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.CheckboxLabeled("VMF_Settings.DrawPlanet".Translate(), ref settings.drawPlanet);
            listingStandard.End();
        }

        public override string SettingsCategory()
        {
            return "Vehicle Map Framework";
        }
    }
}
