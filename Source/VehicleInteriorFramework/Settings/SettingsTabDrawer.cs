using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VehicleInteriors.Settings
{
    internal abstract class SettingsTabDrawer
    {
        public virtual void ResetSettings()
        {
            SoundDefOf.Click.PlayOneShotOnCamera(null);
        }

        private readonly Vector2 ResetButtonSize = new Vector2(150f, 35f);

        public virtual void Draw(Rect inRect)
        {
            var rect = new Rect(inRect.xMax - ResetButtonSize.x, inRect.yMax - ResetButtonSize.y, ResetButtonSize.x, ResetButtonSize.y);
            if (Widgets.ButtonText(rect, "Default".Translate()))
            {
                this.ResetSettings();
            }
        }

        protected VehicleMapSettings settings = VehicleInteriors.settings;
    }
}
