﻿using Verse;

namespace VehicleInteriors
{
    public class VehicleMapGrid : MapComponent
    {
        public VehicleMapGrid(Map map) : base(map)
        {
            vehicleGrid = new VehiclePawnWithMap[map.cellIndices.NumGridCells];
        }

        public VehiclePawnWithMap VehicleAt(IntVec3 c)
        {
            return vehicleGrid[map.cellIndices.CellToIndex(c)];
        }
        public Map VehicleMapAt(IntVec3 c)
        {
            return vehicleGrid[map.cellIndices.CellToIndex(c)]?.VehicleMap;
        }

        public void Register(IntVec3 c, VehiclePawnWithMap vehicle)
        {
            var index = map.cellIndices.CellToIndex(c);
            if (vehicleGrid[index] == null)
            {
                vehicleGrid[index] = vehicle;
            }
        }

        public void DeRegister(IntVec3 c, VehiclePawnWithMap vehicle)
        {
            var index = map.cellIndices.CellToIndex(c);
            if (vehicleGrid[index] == vehicle)
            {
                vehicleGrid[index] = null;
            }
        }

        public override void MapComponentUpdate()
        {
            if (VehicleInteriors.settings.drawVehicleMapGrid)
            {
                DebugDraw();
            }
        }

        internal void DebugDraw()
        {
            for (var i = 0; i < vehicleGrid.Length; i++)
            {
                if (vehicleGrid[i] != null)
                {
                    CellRenderer.RenderCell(map.cellIndices.IndexToCell(i));
                }
            }
        }

        private VehiclePawnWithMap[] vehicleGrid;
    }
}