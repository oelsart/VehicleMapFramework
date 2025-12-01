using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class Command_ActionWithIcon : Command_Action
{
    public bool hideIconIfDisabled;

    public Texture miniIcon;

    public float miniIconSize = 24f;
    
    public override GizmoResult GizmoOnGUI(Vector2 loc, float maxWidth, GizmoRenderParms parms)
    {
        var gizmoResult = base.GizmoOnGUI(loc, maxWidth, parms);
        if (!disabled || !hideIconIfDisabled)
        {
            var rect = new Rect(loc.x, loc.y, GetWidth(maxWidth), 75f);
            var rect2 = new Rect(rect.x + rect.width - miniIconSize, rect.y, miniIconSize, miniIconSize);
            GUI.DrawTexture(rect2, miniIcon);
        }
        return gizmoResult;
    }
}