using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class Test_HaulToInventoryCrossMap(VehicleGroup group) : Test_HaulGeneralCrossMap(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulToInventory");
    
    protected override bool DisablePUAH => false;
}