using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class Dialog_ChooseVehicleRoles : Window
{
    public override Vector2 InitialSize => new(350f, 216f);

    public Dialog_ChooseVehicleRoles(VehiclePawn vehicle, RoleUpgradeBuildable roleUpgrade, VehicleUpgradeBuildable upgradeBuildable) : base(null)
    {
        this.vehicle = vehicle;
        this.roleUpgrade = roleUpgrade;
        this.upgradeBuildable = upgradeBuildable;
        doCloseButton = true;

        forcePause = true;
    }

    public override string CloseButtonText => "OK".Translate();

    public override void DoWindowContents(Rect inRect)
    {
        var font = Text.Font;
        Text.Font = GameFont.Small;
        var rect = inRect;
        rect.height -= Window.CloseButSize.y + 10f;
        Widgets.DrawMenuSection(rect);

        //まだ割り当てられてないタレットたち
        var turrets = vehicle.CompVehicleTurrets?.Turrets?.Where(t => vehicle.handlers?.All(h => (!h.role?.TurretIds?.Contains(t.key) ?? true) && (!h.role?.TurretIds?.Contains(t.groupKey) ?? true)) ?? true).ToList();
        var turretsByGroup = turrets.GroupBy(t => !t.groupKey.NullOrEmpty() ? t.groupKey : t.key);
        var viewRect = new Rect(0f, 0f, inRect.width, turrets.Count * Text.LineHeight);
        var outRect = rect;
        Widgets.AdjustRectsForScrollView(rect, ref outRect, ref viewRect);
        var rect2 = new Rect(outRect.x, outRect.y, outRect.width, Text.LineHeight);
        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        foreach (var t in turretsByGroup)
        {
            var rect3 = rect2.LeftPartPixels(Text.LineHeight);
            var rect4 = rect2.RightPartPixels(rect2.width - Text.LineHeight - 10f);
            Widgets.DrawTextureFitted(rect3, t.First().GizmoIcon, 1f);
            Widgets.Label(rect4, t.First().gizmoLabel);
            if (Widgets.ButtonInvisible(rect2))
            {
                if (!turretIds.Contains(t.Key))
                {
                    turretIds.Add(t.Key);
                    if (turretIds.Count > roleUpgrade.slotsToOperate)
                    {
                        turretIds.RemoveAt(0);
                    }
                }
                else
                {
                    turretIds.Remove(t.Key);
                }
            }
            if (turretIds.Contains(t.Key))
            {
                Widgets.DrawHighlightSelected(rect2);
            }
            Widgets.DrawHighlightIfMouseover(rect2);
            rect2.y += Text.LineHeight;
        }
        Widgets.EndScrollView();
        Text.Font = font;
    }

    public override void PostClose()
    {
        upgradeBuildable.UpgradeRole(vehicle, roleUpgrade, false, false, turretIds);
    }

    private readonly VehiclePawn vehicle;

    private readonly RoleUpgradeBuildable roleUpgrade;

    private readonly VehicleUpgradeBuildable upgradeBuildable;

    private readonly List<string> turretIds = [];

    private Vector2 scrollPosition;
}
