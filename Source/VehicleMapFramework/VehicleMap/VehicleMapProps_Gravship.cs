using Verse;

namespace VehicleMapFramework
{
    public class VehicleMapProps_Gravship : VehicleMapProps_Unique, IExposable
    {
        public string defName;
        
        public void ExposeData()
        {
            Scribe_Defs.Look(ref baseDef, "baseDef");
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref offset, "offset");
            Scribe_Values.Look(ref size, "size");
            Scribe_Collections.Look(ref outOfBoundsCells, "outOfBoundsCells");
            
            if (Scribe.mode == LoadSaveMode.LoadingVars && baseDef != null)
            {
                var props = baseDef.GetModExtension<VehicleMapProps_Gravship>();
                if (props is null) return;
                size = props.size;
                offset = props.offset;
                offsetNorth = props.offsetNorth;
                offsetSouth = props.offsetSouth;
                offsetEast = props.offsetEast;
                offsetWest = props.offsetWest;
                offsetNorthEast = props.offsetNorthEast;
                offsetNorthWest = props.offsetNorthWest;
                offsetSouthEast = props.offsetSouthEast;
                offsetSouthWest = props.offsetSouthWest;
                filledStructureCells = props.filledStructureCells;
                filledStructureCellRects = props.filledStructureCellRects;
                emptyStructureCells = props.emptyStructureCells;
                emptyStructureCellRects = props.emptyStructureCellRects;
                expandableCells = props.expandableCells;
                expandableCellRects = props.expandableCellRects;
                outOfBoundsCells = props.outOfBoundsCells;
                outOfBoundsCellRects = props.outOfBoundsCellRects;
                edgeSpace = props.edgeSpace;
                edgeSpaceNorth = props.edgeSpaceNorth;
                edgeSpaceNorthEast = props.edgeSpaceNorthEast;
                edgeSpaceEast = props.edgeSpaceEast;
                edgeSpaceSouthEast = props.edgeSpaceSouthEast;
                edgeSpaceSouth = props.edgeSpaceSouth;
                edgeSpaceSouthWest = props.edgeSpaceSouthWest;
                edgeSpaceWest = props.edgeSpaceWest;
                edgeSpaceNorthWest = props.edgeSpaceNorthWest;
            }
        }
    }
}
