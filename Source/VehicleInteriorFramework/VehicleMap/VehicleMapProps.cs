using SmashTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class VehicleMapProps : DefModExtension
    {
        public IntVec2 size;

        public Vector3 offset = Vector3.zero;

        public Vector3? offsetNorth;

        public Vector3? offsetSouth;

        public Vector3? offsetEast;

        public Vector3? offsetWest;

        public Vector3? offsetNorthEast;

        public Vector3? offsetNorthWest;

        public Vector3? offsetSouthEast;

        public Vector3? offsetSouthWest;

        public List<IntVec2> filledStructureCells;

        public List<IntVec2> emptyStructureCells;

        public List<CellRect> filledStructureCellRects;

        public List<CellRect> emptyStructureCellRects;

        public override IEnumerable<string> ConfigErrors()
        {
            if (!this.filledStructureCellRects.NullOrEmpty())
            {
                if (this.filledStructureCells == null)
                {
                    this.filledStructureCells = new List<IntVec2>();
                }
                this.filledStructureCells.AddRange(this.filledStructureCellRects.SelectMany(r => r.Cells2D));
                this.filledStructureCells.Distinct();
            }
            if (!this.emptyStructureCellRects.NullOrEmpty())
            {
                if (this.emptyStructureCells == null)
                {
                    this.emptyStructureCells = new List<IntVec2>();
                }
                this.emptyStructureCells.AddRange(this.emptyStructureCellRects.SelectMany(r => r.Cells2D));
                this.emptyStructureCells.Distinct();
            }

            var mapRect = new CellRect(0, 0, this.size.x, this.size.z);
            if (!this.filledStructureCells.NullOrEmpty())
            {
                foreach (var c in this.filledStructureCells)
                {
                    if (!mapRect.Contains(c.ToIntVec3))
                    {
                        yield return "[VehicleMapFramework] Structure cells contain out of map range.";
                    }
                }
            }
            if (!this.emptyStructureCells.NullOrEmpty())
            {
                foreach (var c in this.emptyStructureCells)
                {
                    if (!mapRect.Contains(c.ToIntVec3))
                    {
                        yield return "[VehicleMapFramework] Structure cells contain out of map range.";
                    }
                }
            }
            if (!this.filledStructureCells.NullOrEmpty() && !this.emptyStructureCells.NullOrEmpty())
            {
                foreach (var c in this.filledStructureCells.Intersect(this.emptyStructureCells))
                {
                    yield return $"[VehicleMapFramework] Cell {c} is designated both filled and empty structure.";
                }
            }
        }
    }
}
