using RimWorld.Planet;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Vehicles;
using Verse.AI;
using Verse;

namespace VehicleInteriors
{
    public class VehicleOrientationControllerAcrossMaps : BaseTargeter
    {
        public Rot8 Rotation
        {
            get
            {
                IntVec3 point = UI.MouseCell();
                return Rot8.FromAngle(this.cell.AngleToCell(point));
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
            Job job = new Job(VIF_DefOf.VIF_GotoAcrossMaps, this.cell);
            var driver = job.GetCachedDriver(this.vehicle) as JobDriverAcrossMaps;
            driver.SetSpots(this.exitSpot, this.enterSpot);
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
                this.vehicle.vehiclePather.SetEndRotation(endRotation);
                var drawPos = this.cell.ToVector3Shifted();
                if (this.destMap.IsVehicleMapOf(out var vehicle2) && Find.CurrentMap != this.destMap)
                {
                    drawPos = drawPos.OrigToVehicleMap(vehicle2);
                }
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
            if (this.timeHeldDown >= 0.15f || num >= 0.5f || num <= -0.5f)
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
            VehicleGhostUtility.DrawGhostOverlays(center, rot, vehicleDef, graphic, ghostCol, drawAltitude, vehicle);
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
