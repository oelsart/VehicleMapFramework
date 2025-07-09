using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class WorkGiver_LoadBuildableContainer : WorkGiver_Scanner, IWorkGiverAcrossMaps
{
    public bool NeedVirtualMapTransfer => false;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        return pawn.Map.BaseMapAndVehicleMaps().SelectMany(m => m.listerBuildings.allBuildingsColonist.Where(b => b.HasComp<CompTransporter>()));
    }

    public override PathEndMode PathEndMode
    {
        get
        {
            return PathEndMode.Touch;
        }
    }

    public override Danger MaxPathDanger(Pawn pawn)
    {
        return Danger.Deadly;
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        CompTransporter transporter = t.TryGetComp<CompTransporter>();
        return LoadTransportersJobOnVehicleUtility.HasJobOnTransporter(pawn, transporter);
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        CompTransporter transporter = t.TryGetComp<CompTransporter>();
        return LoadTransportersJobOnVehicleUtility.JobOnTransporter(pawn, transporter);
    }
}
