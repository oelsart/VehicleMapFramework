using RimWorld;
using System;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VehicleInteriors;

public class EphemenalWindow : Window
{
    public override Vector2 InitialSize
    {
        get
        {
            return windowRect.size;
        }
    }

    protected override float Margin
    {
        get
        {
            return 0f;
        }
    }

    public EphemenalWindow() : base(null)
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
            Rect r = windowRect.AtZero().ContractedBy(-5f);
            if (!r.Contains(Event.current.mousePosition))
            {
                float num = GenUI.DistFromRect(r, Event.current.mousePosition);
                baseColor = new Color(1f, 1f, 1f, 1f - (num / 95f));
                if (num > 95f)
                {
                    Close(false);
                    Cancel();
                    return;
                }
            }
        }
    }

    public void Cancel()
    {
        SoundDefOf.FloatMenu_Cancel.PlayOneShotOnCamera(null);
        Find.WindowStack.TryRemove(this, true);
    }

    public Action doWindowFunc;

    public bool vanishIfMouseDistant = true;

    private Color baseColor = Color.white;
}
