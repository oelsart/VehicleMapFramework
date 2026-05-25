using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class Command_FlipBuilding : Command_Action
{
  public Texture2D commandIcon;

  public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
  {
    base.DrawIcon(rect, buttonMat, parms);
    if (!commandIcon) return;
    rect.y -= 8f;
    Widgets.DrawTextureFitted(rect, commandIcon, 0.7f);
  }
}
