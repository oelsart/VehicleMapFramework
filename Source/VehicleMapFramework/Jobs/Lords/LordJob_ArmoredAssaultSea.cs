using RimWorld;
using Vehicles;
using Verse.AI.Group;

namespace VehicleMapFramework;

public class LordJob_ArmoredAssaultSea : LordJob_ArmoredAssault
{
    public LordJob_ArmoredAssaultSea()
    {
    }

    public LordJob_ArmoredAssaultSea(SpawnedPawnParams parms) : base(parms)
    {
    }

    public LordJob_ArmoredAssaultSea(Faction assaulterFaction, RaiderPermissions permission)
        : base(assaulterFaction, permission)
    {
    }

    public override StateGraph CreateGraph()
    {
        var graph = base.CreateGraph();
        graph.lordToils[0] = new LordToil_AssaultColonyArmoredSea();
        return graph;
    }
}