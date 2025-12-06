using System.Linq;
using SmashTools;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompNpcVehicleMap : VehicleComp
{
    public CompProperties_NpcVehicleMap Props => (CompProperties_NpcVehicleMap)props;

    public CompProperties_NpcVehicleMap.VehicleMapParams Params { get; private set; }

    public void SetParams(int pawnCount)
    {
        if (Params != null)
        {
            VMF_Log.Warning("CompNpcVehicleMap: Params already set.");
            return;
        }

        if (Props.mapParamsList
            .Where(mapParams => mapParams.pawnCountRange.InRange(pawnCount))
            .TryRandomElement(out var result))
        {
            Params = result;
            return;
        }

        VMF_Log.Warning($"CompNpcVehicleMap: No mapParams found for pawnCount {pawnCount}. Using first mapParams.");
        Params = Props.mapParamsList.FirstOrDefault();
    }
}