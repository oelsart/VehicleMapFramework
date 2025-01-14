using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class VehicleOrientationControllerAcrossMaps : BaseTargeter
    {
        public Rot8 Rotation
        {
            get
            {
                IntVec3 point = UI.MouseCell();
                var c = this.cell;
                if (this.destMap.IsVehicleMapOf(out var vehicle) && Find.CurrentMap != this.destMap)
                {
                    c = c.OrigToVehicleMap(vehicle);
                }
                return Rot8.FromAngle(c.AngleToCell(point));
            }
        }

        private bool IsDragging { get; set; }

        public override bool IsTargeting
        {
            get
            {
                return this.vehicle != null && this.vehicle.Spawned;
            }
        }

        public static VehicleOrientationControllerAcrossMaps Instance { get; private set; }

        private void Init(VehiclePawn vehicle, IntVec3 cell, IntVec3 clickCell, Map destMap, TargetInfo exitSpot, TargetInfo enterSpot)
        {
            this.vehicle = vehicle;
            this.cell = cell;
            this.clickCell = clickCell;
            this.clickPos = UI.MouseMapPosition();
            this.destMap = destMap;
            this.exitSpot = exitSpot;
            this.enterSpot = enterSpot;
        }

        public static void StartOrienting(VehiclePawn dragging, IntVec3 cell, Map destMap, TargetInfo exitSpot, TargetInfo enterSpot)
        {
            VehicleOrientationControllerAcrossMaps.StartOrienting(dragging, cell, cell, destMap, exitSpot, enterSpot);
        }

        public static void StartOrienting(VehiclePawn dragging, IntVec3 cell, IntVec3 clickCell, Map destMap, TargetInfo exitSpot, TargetInfo enterSpot)
        {
            VehicleOrientationControllerAcrossMaps.Instance.StopTargeting();
            VehicleOrientationControllerAcrossMaps.Instance.Init(dragging, cell, clickCell, destMap, exitSpot, enterSpot);
        }

        public void ConfirmOrientation()
        {
            Job job = new Job(VIF_DefOf.VIF_GotoAcrossMaps, this.cell).SetSpotsToJobAcrossMaps(this.vehicle, this.exitSpot, this.enterSpot);
            bool flag = CellRect.WholeMap(this.vehicle.Map).IsOnEdge(this.clickCell, 3);
            bool flag2 = this.vehicle.Map.exitMapGrid.IsExitCell(this.clickCell);
            bool flag3 = this.vehicle.InhabitedCellsProjected(this.clickCell, Rot8.Invalid, 0).NotNullAndAny((IntVec3 cell) => cell.InBounds(this.vehicle.Map) && this.vehicle.Map.exitMapGrid.IsExitCell(cell));
            if (flag2 || flag3)
            {
                job.exitMapOnArrival = true;
            }
            else if (!this.vehicle.Map.IsPlayerHome && !this.vehicle.Map.exitMapGrid.MapUsesExitGrid && flag)
            {
                FormCaravanComp component = this.vehicle.Map.Parent.GetComponent<FormCaravanComp>();
                if (component != null && MessagesRepeatAvoider.MessageShowAllowed(string.Format("MessagePlayerTriedToLeaveMapViaExitGrid-{0}", this.vehicle.Map.uniqueID), 60f))
                {
                    if (component.CanFormOrReformCaravanNow)
                    {
                        Messages.Message("MessagePlayerTriedToLeaveMapViaExitGrid_CanReform".Translate(), this.vehicle.Map.Parent, MessageTypeDefOf.RejectInput, false);
                    }
                    else
                    {
                        Messages.Message("MessagePlayerTriedToLeaveMapViaExitGrid_CantReform".Translate(), this.vehicle.Map.Parent, MessageTypeDefOf.RejectInput, false);
                    }
                }
            }
            if (this.vehicle.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false))
            {
                Rot8 endRotation = this.IsDragging ? this.Rotation : Rot8.Invalid;
                var drawPos = this.cell.ToVector3Shifted();
                if (this.destMap.IsVehicleMapOf(out var vehicle2) && Find.CurrentMap != this.destMap)
                {
                    drawPos = drawPos.OrigToVehicleMap(vehicle2);
                    endRotation = this.IsDragging ? Rot8.FromAngle(Ext_Math.RotateAngle(endRotation.AsAngle, -vehicle2.FullRotation.AsAngle)) : endRotation;
                }
                this.vehicle.vehiclePather.SetEndRotation(endRotation);
                FleckMaker.Static(drawPos, this.vehicle.BaseMap(), FleckDefOf.FeedbackGoto, 1f);
            }
            this.StopTargeting();
        }

        public override void ProcessInputEvents()
        {
            if (!this.IsTargeting)
            {
                return;
            }
            if (!Input.GetMouseButton(1))
            {
                this.ConfirmOrientation();
            }
        }

        public override void TargeterUpdate()
        {
            if (!this.IsTargeting)
            {
                return;
            }
            this.timeHeldDown += Time.deltaTime;
            float num = Vector3.Distance(this.clickPos, UI.MouseMapPosition()); 
            if (this.timeHeldDown >= HoldTimeThreshold || num >= DragThreshold || num <= -DragThreshold)
            {
                this.IsDragging = true;
            }
            if (this.IsDragging)
            {
                VehicleOrientationControllerAcrossMaps.DrawGhostVehicleDef(cell, Rotation, vehicle.VehicleDef, VehicleGhostUtility.whiteGhostColor, AltitudeLayer.MoteOverhead, vehicle);
            }
        }

        public static void DrawGhostVehicleDef(IntVec3 center, Rot8 rot, VehicleDef vehicleDef, Color ghostCol, AltitudeLayer drawAltitude, VehiclePawn vehicle = null)
        {
            Graphic graphic = vehicleDef.graphic;
            Graphic graphic2 = GhostUtility.GhostGraphicFor(graphic, vehicleDef, ghostCol, null);
            Vector3 loc = GenThing.TrueCenter(center, rot, vehicleDef.Size, drawAltitude.AltitudeFor());
            if (VehicleOrientationControllerAcrossMaps.Instance.destMap.IsVehicleMapOf(out var vehicle2) && Find.CurrentMap != VehicleOrientationControllerAcrossMaps.Instance.destMap)
            {
                loc = loc.OrigToVehicleMap(vehicle2).WithY(drawAltitude.AltitudeFor());
            }
            Rot8 rot2 = rot;
            float extraRotation = rot.AsRotationAngle;
            if (rot2.IsDiagonal)
            {
                switch (rot2.AsInt)
                {
                    case 4:
                        rot2 = Rot8.North;
                        extraRotation = 45f;
                        break;
                    case 5:
                        rot2 = Rot8.South;
                        extraRotation = -45f;
                        break;
                    case 6:
                        rot2 = Rot8.South;
                        extraRotation = 45f;
                        break;
                    case 7:
                        rot2 = Rot8.North;
                        extraRotation = -45f;
                        break;
                }
            }
            graphic2.DrawFromDef(loc, rot2, vehicleDef, extraRotation);
            Vector3 vector = GenThing.TrueCenter(center, rot, vehicleDef.Size, drawAltitude.AltitudeFor());
            if (vehicle2 != null && Find.CurrentMap != VehicleOrientationControllerAcrossMaps.Instance.destMap)
            {
                vector = loc.OrigToVehicleMap(vehicle2).WithY(drawAltitude.AltitudeFor());
            }
            foreach (var (graphicOverlay, extraRotationOverlay) in vehicleDef.GhostGraphicOverlaysFor(ghostCol))
            {
                if (graphicOverlay is Graphic_RGB graphicOverlayRGB)
                {
                    graphicOverlayRGB.DrawWorker(vector + graphic.DrawOffsetFull(rot), rot, vehicleDef, vehicle, extraRotationOverlay);
                }
                else
                {
                    graphicOverlay.DrawWorker(vector + graphic.DrawOffsetFull(rot), rot, vehicleDef, vehicle, extraRotationOverlay);
                }
            }

            if (vehicleDef.GetSortedCompProperties<CompProperties_VehicleTurrets>() != null)
            {
                vehicleDef.DrawGhostTurretTextures(vector, rot, ghostCol);
            }
        }

        public override void StopTargeting()
        {
            this.vehicle = null;
            this.cell = IntVec3.Invalid;
            this.clickCell = IntVec3.Invalid;
            this.clickPos = Vector3.zero;
            this.IsDragging = false;
            this.timeHeldDown = 0f;
            this.destMap = null;
            this.exitSpot = TargetInfo.Invalid;
            this.enterSpot = TargetInfo.Invalid;
        }

        public override void TargeterOnGUI()
        {
        }

        public override void PostInit()
        {
            VehicleOrientationControllerAcrossMaps.Instance = this;
        }

        private const float DragThreshold = 0.5f;

        private const float HoldTimeThreshold = 0.15f;

        private IntVec3 cell;

        private IntVec3 clickCell;

        private Vector3 clickPos;

        private Map destMap;

        private TargetInfo exitSpot;

        private TargetInfo enterSpot;

        private float timeHeldDown;
    }
}
