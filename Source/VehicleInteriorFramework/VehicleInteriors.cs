using SmashTools;
using System.Runtime.InteropServices;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class VehicleInteriors : Mod
    {
        public static VehicleInteriors mod;

        public static VehicleMapSettings settings;

        public static AssetBundle Bundle
        {
            get
            {
                if (bundleInt == null)
                {
                    bundleInt = AssetBundle.LoadFromFile($@"{VehicleInteriors.mod.Content.RootDir}\Common\AssetBundles\{PlatformInfo}");
                }
                return bundleInt;
            }
        }

        private static AssetBundle bundleInt;

        private static string PlatformInfo
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return "StandaloneWindows64";
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return "StandaloneLinux64";
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return "StandaloneLinux64";
                }
                Log.Error($"[VehicleMapFramework] {RuntimeInformation.OSDescription} is not supported platform. Please let the mod author know the OS info.");
                return null;
            }
        }

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
            listingStandard.SliderLabeled("VMF_Settings.WeightFactor".Translate(), null, null, ref settings.weightFactor, 0f, 3f);
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("VMF_Settings.ThreadingPathCost".Translate(), ref settings.threadingPathCost);
            if (settings.threadingPathCost)
            {
                listingStandard.SliderLabeled("VMF_Settings.MinAreaForThreading".Translate(), null, null, ref settings.minAreaForThreading, 0, 2500, 1, "0", "2500");
            }
            listingStandard.End();
        }

        public override string SettingsCategory()
        {
            return "Vehicle Map Framework";
        }
    }
}
