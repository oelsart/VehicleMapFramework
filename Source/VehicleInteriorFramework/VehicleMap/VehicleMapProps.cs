using SmashTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class VehicleMapProps : DefModExtension
    {
        //Map size.
        public IntVec2 size;

        //Draw offset of vehicle map.
        public Vector3 offset = Vector3.zero;

        public Vector3? offsetNorth;

        public Vector3? offsetSouth;

        public Vector3? offsetEast;

        public Vector3? offsetWest;

        public Vector3? offsetNorthEast;

        public Vector3? offsetNorthWest;

        public Vector3? offsetSouthEast;

        public Vector3? offsetSouthWest;

        //Specify vehicle structures with a fillPercent of 1.0. Vehicle structures cannot be destroyed and damage is absorbed by the vehicle.
        public List<IntVec2> filledStructureCells = new List<IntVec2>();

        public List<CellRect> filledStructureCellRects = new List<CellRect>();

        //Specify vehicle structures with a fillPercent of 0.0. Vehicle structures cannot be destroyed and damage is absorbed by the vehicle.
        public List<IntVec2> emptyStructureCells = new List<IntVec2>();

        public List<CellRect> emptyStructureCellRects = new List<CellRect>();

        //Explicitly specify cells to be designated as OutOfBounds. If this is false, the map size is expanded by 1 and edges are specified as OutOfBounds
        public bool specificOutOfBounds;

        public List<IntVec2> outOfBoundsCells = new List<IntVec2>();

        public List<CellRect> outOfBoundsCellRects = new List<CellRect>();

        //Specify the size of the gap between the position of the entrance to the vehicle map, such as a ladder or ramp, and where it will actually draw.
        public EdgeSpace edgeSpace;

        public EdgeSpace? edgeSpaceNorth;

        public EdgeSpace? edgeSpaceNorthEast;

        public EdgeSpace? edgeSpaceEast;

        public EdgeSpace? edgeSpaceSouthEast;

        public EdgeSpace? edgeSpaceSouth;

        public EdgeSpace? edgeSpaceSouthWest;

        public EdgeSpace? edgeSpaceWest;

        public EdgeSpace? edgeSpaceNorthWest;

        public IEnumerable<IntVec2> FilledStructureCells => this.filledStructureCells.Union(this.filledStructureCellRects.SelectMany(r => r.Cells2D));

        public IEnumerable<IntVec2> EmptyStructureCells => this.emptyStructureCells.Union(this.emptyStructureCellRects.SelectMany(r => r.Cells2D));

        public IEnumerable<IntVec2> OutOfBoundsCells
        {
            get
            {
                if (this.specificOutOfBounds)
                {
                    return this.outOfBoundsCells.Union(this.outOfBoundsCellRects.SelectMany(r => r.Cells2D));
                }
                return new CellRect(0, 0, this.size.x, this.size.z).EdgeCells.Select(c => c.ToIntVec2);
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            var mapRect = new CellRect(0, 0, this.size.x, this.size.z);
            foreach (var c in this.FilledStructureCells.Union(this.EmptyStructureCells))
            {
                if (!mapRect.Contains(c.ToIntVec3))
                {
                    yield return "[VehicleMapFramework] Structure cells contain out of map range.";
                }
            }
            foreach (var c in this.FilledStructureCells.Intersect(this.EmptyStructureCells))
            {
                yield return $"[VehicleMapFramework] Cell {c} is designated both filled and empty structure.";
            }
        }

        public float EdgeSpaceValue(Rot8 vehicleRot, Rot4 thingRot)
        {
            return this.EdgeSpaceByRot(vehicleRot).SpaceByRot(thingRot);
        }

        public EdgeSpace EdgeSpaceByRot(Rot8 rot)
        {
            switch (rot.AsInt)
            {
                case Rot8.NorthInt: return this.edgeSpaceNorth ?? (this.edgeSpaceNorth = this.edgeSpaceSouth?.FlipVertical() ?? this.edgeSpace).Value;
                case Rot8.EastInt: return this.edgeSpaceEast ?? (this.edgeSpaceEast = this.edgeSpaceWest?.FlipHorizontal() ?? this.edgeSpace).Value;
                case Rot8.SouthInt: return this.edgeSpaceSouth ?? (this.edgeSpaceSouth = this.edgeSpaceNorth?.FlipVertical() ?? this.edgeSpace).Value;
                case Rot8.WestInt: return this.edgeSpaceWest ?? (this.edgeSpaceWest = this.edgeSpaceEast?.FlipHorizontal() ?? this.edgeSpace).Value;
                case Rot8.NorthEastInt: return this.edgeSpaceNorthEast ?? (this.edgeSpaceNorthEast = this.edgeSpaceNorth?.ToDiagonal() ?? (this.edgeSpaceNorth = this.edgeSpaceSouth?.FlipVertical() ?? this.edgeSpace).Value.ToDiagonal()).Value;
                case Rot8.SouthEastInt: return this.edgeSpaceSouthEast ?? (this.edgeSpaceSouthEast = this.edgeSpaceSouth?.ToDiagonal() ?? (this.edgeSpaceSouth = this.edgeSpaceNorth?.FlipVertical() ?? this.edgeSpace).Value.ToDiagonal()).Value;
                case Rot8.SouthWestInt: return this.edgeSpaceSouthWest ?? (this.edgeSpaceSouthWest = this.edgeSpaceSouth?.ToDiagonal() ?? (this.edgeSpaceSouth = this.edgeSpaceNorth?.FlipVertical() ?? this.edgeSpace).Value.ToDiagonal()).Value;
                case Rot8.NorthWestInt: return this.edgeSpaceNorthWest ?? (this.edgeSpaceNorthWest = this.edgeSpaceNorth?.ToDiagonal() ?? (this.edgeSpaceNorth = this.edgeSpaceSouth?.FlipVertical() ?? this.edgeSpace).Value.ToDiagonal()).Value;
                default: return this.edgeSpace;
            }
        }

        public struct EdgeSpace
        {
            public float space;

            public float? north;

            public float? east;

            public float? south;

            public float? west;

            public float SpaceByRot(Rot4 rot)
            {
                switch (rot.AsInt)
                {
                    case Rot4.NorthInt: return this.north ?? (this.north = this.south ?? this.space).Value;
                    case Rot4.EastInt: return this.east ?? (this.east = this.west ?? this.space).Value;
                    case Rot4.SouthInt: return this.south ?? (this.south = this.north ?? this.space).Value;
                    case Rot4.WestInt: return this.west ?? (this.west = this.east ?? this.space).Value;
                    default: return this.space;
                }
            }

            public EdgeSpace ToDiagonal()
            {
                return new EdgeSpace
                {
                    space = this.space * sin45,
                    north = this.north * sin45,
                    east = this.east * sin45,
                    south = this.south * sin45,
                    west = this.west * sin45,
                };
            }

            public EdgeSpace FlipHorizontal()
            {
                return new EdgeSpace()
                {
                    space = this.space,
                    north = this.north,
                    east = this.west,
                    south = this.south,
                    west = this.east
                };
            }

            public EdgeSpace FlipVertical()
            {
                return new EdgeSpace()
                {
                    space = this.space,
                    north = this.south,
                    east = this.east,
                    south = this.north,
                    west = this.west
                };
            }

            private const float sin45 = 0.707106781f;
        }
    }
}
