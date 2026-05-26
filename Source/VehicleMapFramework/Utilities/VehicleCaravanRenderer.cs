using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld.Planet;
using UnityEngine;
using Vehicles;
using Vehicles.World;
using Verse;

namespace VehicleMapFramework;

public static class VehicleCaravanRenderer
{
    private static ConditionalWeakTable<WorldObject, Dictionary<VehiclePawn, Vector3>> DrawPositions { get; } = [];

    private static readonly List<CellRect> tmpCellRects = [];
    
    extension(WorldObject vehicleCaravanOrStashedVehicle)
    {
        public Dictionary<VehiclePawn, Vector3> DrawPositions => DrawPositions.GetOrCreateValue(vehicleCaravanOrStashedVehicle);

        public IEnumerable<VehiclePawn> Vehicles => vehicleCaravanOrStashedVehicle switch
        {
            VehicleCaravan caravan => caravan.Vehicles,
            StashedVehicle stashedVehicle => stashedVehicle.Vehicles,
            _ => []
        };

        public void RecalculateVehiclePositions()
        {
            var radialCount = GenRadial.NumCellsInRadius(CombatExtended ? 119f : GenRadial.MaxRadialPatternRadius - 0.1f);
            var drawPositions = vehicleCaravanOrStashedVehicle.DrawPositions;
            var vehicles = (vehicleCaravanOrStashedVehicle switch
            {
                VehicleCaravan caravan => caravan.Vehicles,
                StashedVehicle stashedVehicle => stashedVehicle.Vehicles,
                _ => []
            }).ToList();
            foreach (var vehicle in vehicles)
            {
                var cellRect = CellRect.FromLimits(IntVec3.Zero, vehicle.VehicleDef.Size.ToIntVec3 + IntVec3.NorthEast);
                for (var i = 0; i < radialCount; i++)
                {
                    var cellRect2 = cellRect.MovedBy(GenRadial.RadialPattern[i]);
                    if (!tmpCellRects.Any(cr => cellRect2.Overlaps(cr)))
                    {
                        tmpCellRects.Add(cellRect2);
                        drawPositions[vehicle] = cellRect2.CenterVector3;
                        break;
                    }
                }
            }

            var values = drawPositions.Values;
            var y = AltitudeLayer.LayingPawn.AltitudeFor();
            var average = new Vector3(values.Average(p => p.x), -y, values.Average(p => p.z));
            foreach (var vehicle in vehicles)
                drawPositions[vehicle] -= average;
            
            tmpCellRects.Clear();
        }
    }
}