using RimWorld;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework
{
    public class Building_GravshipWheel : Building
    {
        [Unsaved(false)]
        private CompGravshipWheel gravshipWheel;

        public override bool TransmitsPowerNow => this.IsOnVehicleMapOf(out var vehicle) && (vehicle.ignition?.Drafted ?? false);

        //車上でPrintする時しか呼ばれないはず
        public override Vector3 DrawPos
        {
            get
            {
                if (Map?.GetCachedMapComponent<VehiclePawnWithMapCache>()?.cacheMode ?? false)
                {
                    var drawPos = base.DrawPos;
                    var engine = GravshipUtility.GetPlayerGravEngine(Map);
                    if (engine == null || engine is not Building_GravEngine building_gravEngine) return drawPos;
                    if (this.OccupiedRect().Any(building_gravEngine.ValidSubstructureAt)) return drawPos;

                    var cell = CompGravshipWheel.AdjacentCells.FirstOrFallback(building_gravEngine.ValidSubstructureAt, IntVec3.Invalid);
                    if (!cell.IsValid) return drawPos;
                    drawPos += new Vector3(0f, 0f, -0.2f).RotatedBy(VehicleMapUtility.RotForPrintCounter);
                    var offset = new Vector3(DrawSize.x * 0.12f, 0f, 0f);
                    if (cell.x > Position.x)
                    {
                        if (VehicleMapUtility.rotForPrint == Rot4.East)
                        {
                            offset.y -= Altitudes.AltInc * 250f;
                        }
                    }
                    else
                    {
                        if (VehicleMapUtility.rotForPrint == Rot4.West)
                        {
                            offset.y -= Altitudes.AltInc * 250f;
                        }
                        offset.x = -offset.x;
                    }
                    drawPos += offset;
                    return drawPos;
                }
                return base.DrawPos;
            }
        }

        public CompGravshipWheel CompGravshipWheel
        {
            get
            {
                gravshipWheel ??= GetComp<CompGravshipWheel>();
                return gravshipWheel;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;

            if (!this.IsOnVehicleMapOf(out var vehicle))
            {
                yield return new Command_Action()
                {
                    defaultLabel = "VMF_JackUp".Translate(),
                    action = GenerateGravshipVehicle
                };
            }
            else if (vehicle.Spawned)
            {
                yield return new Command_Action()
                {
                    defaultLabel = "VMF_PlaceOn".Translate(),
                    action = () => PlaceGravship(vehicle)
                };
            }
        }

        public override void Print(SectionLayer layer)
        {
            base.Print(layer);
        }

        public void GenerateGravshipVehicle()
        {
            var curretGravship = Current.Game.Gravship;
            var report = GravshipVehicleUtility.GenerateGravshipVehicle(CompGravshipWheel?.engine, Map);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
            }
            Current.Game.Gravship = curretGravship;
        }

        public void PlaceGravship(VehiclePawnWithMap vehicle)
        {
            var curretGravship = Current.Game.Gravship;
            var report = GravshipVehicleUtility.PlaceGravshipVehicle(CompGravshipWheel?.engine, vehicle);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
            }
            Current.Game.Gravship = curretGravship;
        }
    }
}
