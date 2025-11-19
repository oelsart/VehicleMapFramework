using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework;

public class Dialog_LoadCargoToBuildableContainer : Window
{
    public float MassUsage
    {
        get
        {
            if (massUsageDirty)
            {
                massUsageDirty = false;
                field = CollectionsMassCalculator.MassUsageTransferables(transferables, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnloadOrPlayerPawn, true);
                field += MassUtility.GearAndInventoryMass(vehicle);
            }
            return field;
        }
    }

    public float MassCapacity
    {
        get
        {
            return vehicle.GetStatValue(VehicleStatDefOf.CargoCapacity);
        }
    }

    public Dialog_LoadCargoToBuildableContainer(CompBuildableContainer comp)
    {
        this.comp = comp;
        vehicle = comp.Vehicle;
        closeOnAccept = true;
        closeOnCancel = true;
        forcePause = false;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize
    {
        get
        {
            return new Vector2(1024f, UI.screenHeight);
        }
    }

    public override void PostOpen()
    {
        base.PostOpen();
        massUsageDirty = true;
        CalculateAndRecacheTransferables();
    }

    public override void DoWindowContents(Rect inRect)
    {
        Rect rect = new(0f, 0f, inRect.width, 35f);
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, vehicle.LabelShortCap);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        DrawCargoNumbers(new Rect(12f, 35f, inRect.width - 24f, 40f));
        Rect rect2 = new(inRect.width - 225f, 35f, 225f, 40f);
        var showAllCargoItems = VehicleMod.settings.showAllCargoItems;
        string text = "VF_ShowAllItemsOnMap".Translate();
        Widgets.Label(rect2, text);
        rect2.x += Text.CalcSize(text).x + 20f;
        Widgets.Checkbox(new Vector2(rect2.x, rect2.y), ref VehicleMod.settings.showAllCargoItems);
        if (showAllCargoItems != VehicleMod.settings.showAllCargoItems)
        {
            CalculateAndRecacheTransferables();
        }

        inRect.yMin += 60f;
        Widgets.DrawMenuSection(inRect);
        inRect = inRect.ContractedBy(17f);
        Widgets.BeginGroup(inRect);
        var rect3 = inRect.AtZero();
        BottomButtons(rect3);
        var inRect2 = rect3;
        inRect2.yMax -= 76f;
        itemsTransfer.OnGUI(inRect2, out var anythingChanged);
        if (anythingChanged)
        {
            CountToTransferChanged();
        }

        Widgets.EndGroup();
    }

    public void BottomButtons(Rect rect)
    {
        Rect rect2 = new((rect.width / 2f) - (BottomButtonSize.x / 2f), rect.height - 55f - 17f, BottomButtonSize.x, BottomButtonSize.y);
        if (Widgets.ButtonText(rect2, "AcceptButton".Translate()))
        {
            List<TransferableOneWay> cargoToLoad = [.. transferables.Where(t => t.CountToTransfer > 0)];
            comp.leftToLoad = cargoToLoad;
            if (!comp.leftToLoad.Empty())
            {
                TransporterUtility.InitiateLoading([comp]);
            }
            //MapComponentCache.GetCachedMapComponent<VehicleReservationManager>(vehicle.Map).RegisterLister(vehicle, "LoadVehicle");
            Close();
        }

        if (Widgets.ButtonText(new Rect(rect2.x - 10f - BottomButtonSize.x, rect2.y, BottomButtonSize.x, BottomButtonSize.y), "ResetButton".Translate()))
        {
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            CalculateAndRecacheTransferables();
        }

        if (Widgets.ButtonText(new Rect(rect2.xMax + 10f, rect2.y, BottomButtonSize.x, BottomButtonSize.y), "CancelButton".Translate()))
        {
            Close();
        }

        if (!Prefs.DevMode)
        {
            return;
        }

        var width = 200f;
        var num = BottomButtonSize.y / 2f;
        if (Widgets.ButtonText(new Rect(0f, rect.height - 55f - 17f, width, num), "Dev: Pack Instantly"))
        {
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            for (var i = 0; i < transferables.Count; i++)
            {
                var things = transferables[i].things;
                var countToTransfer = transferables[i].CountToTransfer;
                TransferableUtility.Transfer(things, countToTransfer, transferred);
                continue;

                void transferred(Thing thing, IThingHolder originalHolder)
                {
                    vehicle.AddOrTransfer(thing);
                }
            }

            Close(doCloseSound: false);
        }

        if (Widgets.ButtonText(new Rect(0f, rect.height - 55f - 17f + num, width, num), "Dev: Select everything"))
        {
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            SetToSendEverything();
        }
    }

    public void DrawCargoNumbers(Rect rect)
    {
        Color color;
        if (MassUsage > MassCapacity)
        {
            color = Color.red;
        }
        else if (MassCapacity == 0f)
        {
            color = Color.grey;
        }
        else
        {
            color = GenUI.LerpColor(MassColor, MassUsage / MassCapacity);
        }
        var color2 = GUI.color;
        GUI.color = color;
        var label = $"{"Mass".Translate()}: {MassUsage}/{MassCapacity}";
        Widgets.Label(rect, label);
        GUI.color = color2;
    }

    private void AddToTransferables(Thing t, bool setToTransferMax = false)
    {
        var transferableOneWay = TransferableUtility.TransferableMatching(t, transferables, TransferAsOneMode.PodsOrCaravanPacking);
        if (transferableOneWay == null)
        {
            transferableOneWay = new TransferableOneWay();
            transferables.Add(transferableOneWay);
        }
        if (transferableOneWay.things.Contains(t))
        {
            Log.Error("Tried to add the same thing twice to TransferableOneWay: " + t);
            return;
        }
        transferableOneWay.things.Add(t);
        if (setToTransferMax)
        {
            transferableOneWay.AdjustTo(transferableOneWay.CountToTransfer + t.stackCount);
        }
    }

    private void CalculateAndRecacheTransferables()
    {
        transferables = [];
        AddItemsToTransferables();
        itemsTransfer = new TransferableOneWayWidget(transferables, null, null, null, true, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, false, () => MassCapacity - MassUsage, 0f, false, -1);
        CountToTransferChanged();
    }

    private void AddItemsToTransferables()
    {
        var list = CaravanFormingUtility.AllReachableColonyItems(comp.parent.Map, VehicleMod.settings.showAllCargoItems);
        if (comp.GatherFromBaseMap && comp.parent.Map != comp.parent.BaseMap())
        {
            list.AddRange(CaravanFormingUtility.AllReachableColonyItems(comp.parent.BaseMap(), VehicleMod.settings.showAllCargoItems));
        }
        for (var i = 0; i < list.Count; i++)
        {
            var thing = list[i];
            if (!TransferableUtility.TransferableMatching(thing, transferables, TransferAsOneMode.PodsOrCaravanPacking)?.things?.Contains(thing) ?? true)
            {
                AddToTransferables(thing);
            }
        }
    }

    private void SetToSendEverything()
    {
        for (var i = 0; i < transferables.Count; i++)
        {
            transferables[i].AdjustTo(transferables[i].GetMaximumToTransfer());
        }
        CountToTransferChanged();
    }

    private void CountToTransferChanged()
    {
        massUsageDirty = true;
    }

    private readonly CompBuildableContainer comp;

    private readonly VehiclePawnWithMap vehicle;

    private List<TransferableOneWay> transferables = [];

    private TransferableOneWayWidget itemsTransfer;

    private bool massUsageDirty;

    private readonly Vector2 BottomButtonSize = new(160f, 40f);

    private static readonly List<Pair<float, Color>> MassColor =
    [
        new Pair<float, Color>(0.1f, Color.green),
        new Pair<float, Color>(0.75f, Color.yellow),
        new Pair<float, Color>(1f, new Color(1f, 0.6f, 0f))
    ];
}
