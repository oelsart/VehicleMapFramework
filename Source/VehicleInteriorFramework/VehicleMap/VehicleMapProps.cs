using SmashTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors;

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
    public List<IntVec2> filledStructureCells = [];

    public List<CellRect> filledStructureCellRects = [];

    //Specify vehicle structures with a fillPercent of 0.0. Vehicle structures cannot be destroyed and damage is absorbed by the vehicle.
    public List<IntVec2> emptyStructureCells = [];

    public List<CellRect> emptyStructureCellRects = [];

    //Specify cells to be designated as OutOfBounds.
    public List<IntVec2> outOfBoundsCells = [];

    public List<CellRect> outOfBoundsCellRects = [];

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

    public IEnumerable<IntVec2> FilledStructureCells => filledStructureCells.Union(filledStructureCellRects.SelectMany(r => r.Cells2D));

    public IEnumerable<IntVec2> EmptyStructureCells => emptyStructureCells.Union(emptyStructureCellRects.SelectMany(r => r.Cells2D));

    public IEnumerable<IntVec2> OutOfBoundsCells
    {
        get
        {
            return new CellRect(0, 0, size.x + 2, size.z + 2).EdgeCells.Select(c => c.ToIntVec2).Union(outOfBoundsCells.Union(outOfBoundsCellRects.SelectMany(r => r.Cells2D)));
        }
    }

    public override IEnumerable<string> ConfigErrors()
    {
        var mapRect = new CellRect(0, 0, size.x, size.z);
        foreach (var c in FilledStructureCells.Union(EmptyStructureCells))
        {
            if (!mapRect.Contains(c.ToIntVec3))
            {
                yield return "[VehicleMapFramework] Structure cells contain out of map range.";
            }
        }
        foreach (var c in FilledStructureCells.Intersect(EmptyStructureCells))
        {
            yield return $"[VehicleMapFramework] Cell {c} is designated both filled and empty structure.";
        }
    }

    public float EdgeSpaceValue(Rot8 vehicleRot, Rot4 thingRot)
    {
        return EdgeSpaceByRot(vehicleRot).SpaceByRot(thingRot);
    }

    public EdgeSpace EdgeSpaceByRot(Rot8 rot)
    {
        switch (rot.AsInt)
        {
            case Rot8.NorthInt: return edgeSpaceNorth ?? (edgeSpaceNorth = edgeSpaceSouth?.FlipVertical() ?? edgeSpace).Value;
            case Rot8.EastInt: return edgeSpaceEast ?? (edgeSpaceEast = edgeSpaceWest?.FlipHorizontal() ?? edgeSpace).Value;
            case Rot8.SouthInt: return edgeSpaceSouth ?? (edgeSpaceSouth = edgeSpaceNorth?.FlipVertical() ?? edgeSpace).Value;
            case Rot8.WestInt: return edgeSpaceWest ?? (edgeSpaceWest = edgeSpaceEast?.FlipHorizontal() ?? edgeSpace).Value;
            case Rot8.NorthEastInt: return edgeSpaceNorthEast ?? (edgeSpaceNorthEast = edgeSpaceNorth?.ToDiagonal() ?? (edgeSpaceNorth = edgeSpaceSouth?.FlipVertical() ?? edgeSpace).Value.ToDiagonal()).Value;
            case Rot8.SouthEastInt: return edgeSpaceSouthEast ?? (edgeSpaceSouthEast = edgeSpaceSouth?.ToDiagonal() ?? (edgeSpaceSouth = edgeSpaceNorth?.FlipVertical() ?? edgeSpace).Value.ToDiagonal()).Value;
            case Rot8.SouthWestInt: return edgeSpaceSouthWest ?? (edgeSpaceSouthWest = edgeSpaceSouth?.ToDiagonal() ?? (edgeSpaceSouth = edgeSpaceNorth?.FlipVertical() ?? edgeSpace).Value.ToDiagonal()).Value;
            case Rot8.NorthWestInt: return edgeSpaceNorthWest ?? (edgeSpaceNorthWest = edgeSpaceNorth?.ToDiagonal() ?? (edgeSpaceNorth = edgeSpaceSouth?.FlipVertical() ?? edgeSpace).Value.ToDiagonal()).Value;
            default: return edgeSpace;
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
                case Rot4.NorthInt: return north ?? (north = south ?? space).Value;
                case Rot4.EastInt: return east ?? (east = west ?? space).Value;
                case Rot4.SouthInt: return south ?? (south = north ?? space).Value;
                case Rot4.WestInt: return west ?? (west = east ?? space).Value;
                default: return space;
            }
        }

        public EdgeSpace ToDiagonal()
        {
            return new EdgeSpace
            {
                space = space * sin45,
                north = north * sin45,
                east = east * sin45,
                south = south * sin45,
                west = west * sin45,
            };
        }

        public EdgeSpace FlipHorizontal()
        {
            return new EdgeSpace()
            {
                space = space,
                north = north,
                east = west,
                south = south,
                west = east
            };
        }

        public EdgeSpace FlipVertical()
        {
            return new EdgeSpace()
            {
                space = space,
                north = south,
                east = east,
                south = north,
                west = west
            };
        }

        private const float sin45 = 0.707106781f;
    }
}
