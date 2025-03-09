using UnityEngine;
using VMF_PUAHPatch;

namespace VehicleInteriors.Settings
{
    internal class SettingsTab_VMF_PUAHPatch : SettingsTabDrawer
    {
        public override void ResetSettings()
        {
            base.ResetSettings();
            VMF_PUAHMod.settings.patchEnabled = true;
            VMF_PUAHMod.settings.debugMode = false;
        }

        public override void Draw(Rect inRect)
        {
            base.Draw(inRect);
            VMF_PUAHMod.mod.DoSettingsWindowContents(inRect);
        }
    }
}
