using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_HaulToInventory(VehicleGroup group) : UnitTest_HaulGeneral(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulToInventory");

    protected override bool DisablePUAH => false;
}