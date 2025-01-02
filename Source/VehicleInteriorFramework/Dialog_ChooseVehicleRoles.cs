using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class Dialog_ChooseVehicleRoles : Window
    {
        public override Vector2 InitialSize => new Vector2(350f, 216f);

        public Dialog_ChooseVehicleRoles(VehiclePawn vehicle, RoleUpgradeBuildable roleUpgrade, VehicleUpgradeBuildable upgradeBuildable) : base(null)
        {
            this.vehicle = vehicle;
            this.roleUpgrade = roleUpgrade;
            this.upgradeBuildable = upgradeBuildable;
            this.doCloseButton = true;

            this.forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var rect = inRect;
            rect.height -= Window.CloseButSize.y + 10f;
            Widgets.DrawMenuSection(rect);

            //まだ割り当てられてないタレットたち
            var turrets = this.vehicle.CompVehicleTurrets?.turrets?.Where(t => this.vehicle.handlers?.All(h => h.role?.TurretIds?.All(i => i != t.key) ?? true) ?? true).ToList();
            var viewRect = new Rect(0f, 0f, inRect.width, turrets.Count * Text.LineHeight);
            var outRect = rect;
            Widgets.AdjustRectsForScrollView(rect, ref outRect, ref viewRect);
            var rect2 = new Rect(outRect.x, outRect.y, outRect.width, Text.LineHeight);
            Widgets.BeginScrollView(outRect, ref this.scrollPosition, viewRect);
            foreach (var t in turrets)
            {
                using(new TextBlock(GameFont.Small))
                {
                    var rect3 = rect2.LeftPartPixels(Text.LineHeight);
                    var rect4 = rect2.RightPartPixels(rect2.width - Text.LineHeight - 10f);
                    Widgets.DrawTextureFitted(rect3, t.GizmoIcon, 1f);
                    Widgets.Label(rect4, t.gizmoLabel);
                    if (Widgets.ButtonInvisible(rect2))
                    {
                        if (!this.turretIds.Contains(t.key))
                        {
                            this.turretIds.Add(t.key);
                            if (this.turretIds.Count > roleUpgrade.slotsToOperate)
                            {
                                this.turretIds.RemoveAt(0);
                            }
                        }
                        else
                        {
                            this.turretIds.Remove(t.key);
                        }
                    }
                }
                if (this.turretIds.Contains(t.key))
                {
                    Widgets.DrawHighlightSelected(rect2);
                }
                Widgets.DrawHighlightIfMouseover(rect2);
            }
            Widgets.EndScrollView();

        }

        public override void PostClose()
        {
            upgradeBuildable.UpgradeRole(this.vehicle, this.roleUpgrade, false, false, this.turretIds);
        }

        private readonly VehiclePawn vehicle;

        private readonly RoleUpgradeBuildable roleUpgrade;

        private readonly VehicleUpgradeBuildable upgradeBuildable;

        private readonly List<string> turretIds = new List<string>();

        private Vector2 scrollPosition;
    }
}
