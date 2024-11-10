using RimWorld;
using System;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VehicleInteriors
{
    public class EphemenalWindow : Window
    {
        public override Vector2 InitialSize
        {
            get
            {
                return this.windowRect.size;
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
            this.layer = WindowLayer.Super;
            this.closeOnClickedOutside = true;
            this.doWindowBackground = false;
            this.drawShadow = false;
            this.doCloseButton = false;
            this.doCloseX = false;
            this.soundAppear = null;
            this.soundClose = null;
            this.closeOnAccept = false;
            this.closeOnCancel = false;
            this.focusWhenOpened = false;
            this.preventCameraMotion = false;
        }

        protected override void SetInitialSizeAndPosition()
        {
        }

        public override void DoWindowContents(Rect inRect)
        {
            UpdateBaseColor();
            GUI.color = this.baseColor;
            this.doWindowFunc();
            GUI.color = Color.white;
        }

        private void UpdateBaseColor()
        {
            this.baseColor = Color.white;
            if (this.vanishIfMouseDistant)
            {
                Rect r = this.windowRect.AtZero().ContractedBy(-5f);
                if (!r.Contains(Event.current.mousePosition))
                {
                    float num = GenUI.DistFromRect(r, Event.current.mousePosition);
                    this.baseColor = new Color(1f, 1f, 1f, 1f - num / 95f);
                    if (num > 95f)
                    {
                        this.Close(false);
                        this.Cancel();
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
}
