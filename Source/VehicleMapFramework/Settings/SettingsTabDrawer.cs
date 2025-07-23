using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework.Settings;

internal abstract class SettingsTabDrawer
{
    public abstract int Index { get; }

    public abstract string Label { get; }

    public virtual void ResetSettings()
    {
        SoundDefOf.Click.PlayOneShotOnCamera(null);
    }

    private readonly Vector2 ResetButtonSize = new(150f, 35f);

    public virtual void Draw(Rect inRect)
    {
        var rect = new Rect(inRect.xMax - ResetButtonSize.x, inRect.yMax - ResetButtonSize.y, ResetButtonSize.x, ResetButtonSize.y);
        if (Widgets.ButtonText(rect, "Default".Translate()))
        {
            ResetSettings();
        }
    }

    protected VehicleMapSettings settings = VehicleMapFramework.settings;
}
