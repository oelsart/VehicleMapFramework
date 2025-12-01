using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework;

public class EphemenalWindow : Window
{
    public Action doWindowFunc;

    public bool vanishIfMouseDistant = true;

    private Color baseColor = Color.white;
    
    public override Vector2 InitialSize => windowRect.size;

    protected override float Margin => 0f;

    public EphemenalWindow()
    {
        layer = WindowLayer.Super;
        closeOnClickedOutside = true;
        doWindowBackground = false;
        drawShadow = false;
        doCloseButton = false;
        doCloseX = false;
        soundAppear = null;
        soundClose = null;
        closeOnAccept = false;
        closeOnCancel = false;
        focusWhenOpened = false;
        preventCameraMotion = false;
    }

    protected override void SetInitialSizeAndPosition()
    {
    }

    public override void DoWindowContents(Rect inRect)
    {
        UpdateBaseColor();
        GUI.color = baseColor;
        doWindowFunc();
        GUI.color = Color.white;
    }

    private void UpdateBaseColor()
    {
        baseColor = Color.white;
        if (vanishIfMouseDistant)
        {
            var r = windowRect.AtZero().ContractedBy(-5f);
            if (!r.Contains(Event.current.mousePosition))
            {
                var num = GenUI.DistFromRect(r, Event.current.mousePosition);
                baseColor = new Color(1f, 1f, 1f, 1f - (num / 95f));
                if (num > 95f)
                {
                    Close(false);
                    Cancel();
                }
            }
        }
    }

    public void Cancel()
    {
        SoundDefOf.FloatMenu_Cancel.PlayOneShotOnCamera();
        Find.WindowStack.TryRemove(this);
    }
}
