using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.Sound;

namespace VehicleInteriors
{
    public class Dialog_LoadCargoToBuildableContainer : Window
    {
        public float MassUsage
        {
            get
            {
                if (this.massUsageDirty)
                {
                    this.massUsageDirty = false;
                    this.cachedMassUsage = CollectionsMassCalculator.MassUsageTransferables(this.transferables, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnloadOrPlayerPawn, true, false);
                    this.cachedMassUsage += MassUtility.GearAndInventoryMass(this.vehicle);
                }
                return this.cachedMassUsage;
            }
        }

        public float MassCapacity
        {
            get
            {
                return this.vehicle.GetStatValue(VehicleStatDefOf.CargoCapacity);
            }
        }

        public Dialog_LoadCargoToBuildableContainer(CompBuildableContainer comp)
        {
            this.comp = comp;
            this.vehicle = comp.Vehicle;
            closeOnAccept = true;
            closeOnCancel = true;
            forcePause = false;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(1024f, (float)UI.screenHeight);
            }
        }

        public override void PostOpen()
        {
            base.PostOpen();
            this.massUsageDirty = true;
            this.CalculateAndRecacheTransferables();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect rect = new Rect(0f, 0f, inRect.width, 35f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, this.vehicle.LabelShortCap);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            DrawCargoNumbers(new Rect(12f, 35f, inRect.width - 24f, 40f));
            Rect rect2 = new Rect(inRect.width - 225f, 35f, 225f, 40f);
            bool showAllCargoItems = VehicleMod.settings.showAllCargoItems;
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
            Rect rect3 = inRect.AtZero();
            BottomButtons(rect3);
            Rect inRect2 = rect3;
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
            Rect rect2 = new Rect(rect.width / 2f - BottomButtonSize.x / 2f, rect.height - 55f - 17f, BottomButtonSize.x, BottomButtonSize.y);
            if (Widgets.ButtonText(rect2, "AcceptButton".Translate()))
            {
                List<TransferableOneWay> cargoToLoad = transferables.Where((TransferableOneWay t) => t.CountToTransfer > 0).ToList();
                comp.leftToLoad = cargoToLoad;
                if (!comp.leftToLoad.Empty())
                {
                    TransporterUtility.InitiateLoading(new[] { this.comp });
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

            float width = 200f;
            float num = BottomButtonSize.y / 2f;
            if (Widgets.ButtonText(new Rect(0f, rect.height - 55f - 17f, width, num), "Dev: Pack Instantly"))
            {
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                for (int i = 0; i < transferables.Count; i++)
                {
                    List<Thing> things = transferables[i].things;
                    int countToTransfer = transferables[i].CountToTransfer;
                    Action<Thing, IThingHolder> transferred = delegate (Thing thing, IThingHolder originalHolder)
                    {
                        vehicle.AddOrTransfer(thing);
                    };
                    TransferableUtility.Transfer(things, countToTransfer, transferred);
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
            if (this.MassUsage > this.MassCapacity)
            {
                color = Color.red;
            }
            else if (this.MassCapacity == 0f)
            {
                color = Color.grey;
            }
            else
            {
                color = GenUI.LerpColor(Dialog_LoadCargoToBuildableContainer.MassColor, this.MassUsage / this.MassCapacity);
            }
            Color color2 = GUI.color;
            GUI.color = color;
            string label = string.Format("{0}: {1}/{2}", "Mass".Translate(), this.MassUsage, this.MassCapacity);
            Widgets.Label(rect, label);
            GUI.color = color2;
        }

        private void AddToTransferables(Thing t, bool setToTransferMax = false)
        {
            TransferableOneWay transferableOneWay = TransferableUtility.TransferableMatching<TransferableOneWay>(t, this.transferables, TransferAsOneMode.PodsOrCaravanPacking);
            if (transferableOneWay == null)
            {
                transferableOneWay = new TransferableOneWay();
                this.transferables.Add(transferableOneWay);
            }
            if (transferableOneWay.things.Contains(t))
            {
                Log.Error("Tried to add the same thing twice to TransferableOneWay: " + t?.ToString());
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
            this.transferables = new List<TransferableOneWay>();
            this.AddItemsToTransferables();
            this.itemsTransfer = new TransferableOneWayWidget(this.transferables, null, null, null, true, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, false, () => this.MassCapacity - this.MassUsage, 0f, false, -1, false, false, false, false, false, false, false, false, false, false);
            this.CountToTransferChanged();
        }

        private void AddItemsToTransferables()
        {
            List<Thing> list = CaravanFormingUtility.AllReachableColonyItems(comp.parent.Map, VehicleMod.settings.showAllCargoItems, false, false);
            if (comp.GatherFromBaseMap)
            {
                list.AddRange(CaravanFormingUtility.AllReachableColonyItems(comp.parent.BaseMap(), VehicleMod.settings.showAllCargoItems, false, false));
            }
            for (int i = 0; i < list.Count; i++)
            {
                this.AddToTransferables(list[i], false);
            }
        }

        private void SetToSendEverything()
        {
            for (int i = 0; i < this.transferables.Count; i++)
            {
                this.transferables[i].AdjustTo(this.transferables[i].GetMaximumToTransfer());
            }
            this.CountToTransferChanged();
        }

        private void CountToTransferChanged()
        {
            this.massUsageDirty = true;
        }

        private CompBuildableContainer comp;

        private VehiclePawnWithMap vehicle;

        private List<TransferableOneWay> transferables = new List<TransferableOneWay>();

        private TransferableOneWayWidget itemsTransfer;

        private bool massUsageDirty;

        private float cachedMassUsage;

        private readonly Vector2 BottomButtonSize = new Vector2(160f, 40f);

        private static readonly List<Pair<float, Color>> MassColor = new List<Pair<float, Color>>
        {
            new Pair<float, Color>(0.1f, Color.green),
            new Pair<float, Color>(0.75f, Color.yellow),
            new Pair<float, Color>(1f, new Color(1f, 0.6f, 0f))
        };
    }
}
