using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_HaulToInventoryCrossMap(VehicleGroup group) : UnitTest_HaulGeneralCrossMap(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulToInventory");
    
    protected override bool DisablePUAH => false;
}