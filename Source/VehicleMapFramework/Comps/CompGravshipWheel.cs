using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleMapFramework
{
    public class CompGravshipWheel : CompGravshipFacility
    {
        public IEnumerable<IntVec3> AdjacentCells
        {
            get
            {
                var cellRect = parent.OccupiedRect().ExpandedBy(1);
                return cellRect.EdgeCells.Except(cellRect.GetCellsOnEdge(parent.Rotation)).Except(cellRect.GetCellsOnEdge(parent.Rotation.Opposite));
            }
        }
    }
}
