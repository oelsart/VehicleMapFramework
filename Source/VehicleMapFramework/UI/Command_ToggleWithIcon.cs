using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class Command_ToggleWithIcon : Command_Toggle
{
  public Texture miniIcon;

  public float miniIconSize = 24f;

  public override GizmoResult GizmoOnGUI(Vector2 loc, float maxWidth, GizmoRenderParms parms)
  {
    var gizmoResult = GizmoOnGUIInt(new Rect(loc.x, loc.y, GetWidth(maxWidth), 75f), parms);
    if (!disabled || !hideIconIfDisabled)
    {
      var rect = new Rect(loc.x, loc.y, GetWidth(maxWidth), 75f);
      var rect2 = new Rect(rect.x + rect.width - miniIconSize, rect.y, miniIconSize, miniIconSize);
      Widgets.DrawTextureFitted(rect2, miniIcon, 1f, isActive() ? 1f : 0.3f);
    }
    return gizmoResult;
  }
}
