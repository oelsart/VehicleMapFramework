using System;
using UnityEngine;
using Verse;

namespace VehicleInteriors;

public class Command_ToggleIcon : Command
{
    public override SoundDef CurActivateSound => toggleSound;

    public override string Label => isActive() ? defaultLabel : labelTwo;

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        toggleAction();
    }

    public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
    {
        Texture badTex = (isActive() ? icon : iconTwo) ?? BaseContent.BadTex;
        rect.position += new Vector2(iconOffset.x * rect.size.x, iconOffset.y * rect.size.y);
        if (!disabled || parms.lowLight)
        {
            GUI.color = IconDrawColor;
        }
        else
        {
            GUI.color = IconDrawColor.SaturationChanged(0f);
        }

        if (parms.lowLight)
        {
            GUI.color = GUI.color.ToTransparent(0.6f);
        }

        Widgets.DrawTextureFitted(rect, badTex, iconDrawScale * 0.85f, iconProportions, iconTexCoords, iconAngle, buttonMat);
        GUI.color = Color.white;
    }

    public Func<bool> isActive;

    public Action toggleAction;

    public SoundDef toggleSound;

    public string labelTwo;

    public Texture iconTwo;
}
