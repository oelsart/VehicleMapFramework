using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class Test_HaulToInventory(VehicleGroup group) : Test_HaulGeneral(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulToInventory");

    protected override bool DisablePUAH => false;
}