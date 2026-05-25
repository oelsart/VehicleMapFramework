using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework.Settings;

internal abstract class SettingsTabDrawer
{

  private readonly Vector2 ResetButtonSize = new(150f, 35f);

  protected VehicleMapSettings settings = VehicleMapFramework.settings;

  public abstract int Index { get; }

  public abstract string Label { get; }

  protected virtual void ResetSettings()
  {
    SoundDefOf.Click.PlayOneShotOnCamera();
  }

  public virtual void Draw(Rect inRect)
  {
    var rect = new Rect(inRect.xMax - ResetButtonSize.x, inRect.yMax - ResetButtonSize.y, ResetButtonSize.x, ResetButtonSize.y);
    if (Widgets.ButtonText(rect, "Default".Translate()))
    {
      ResetSettings();
    }
  }
}
