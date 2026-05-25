using DevTools.Testing;
using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

[LoadIfModsActive("Mehni.PickUpAndHaul")]
[TestCategory("PickUpAndHaul")]
internal class Test_HaulToInventoryCrossMap(VehicleGroup group) : Test_HaulGeneralCrossMap(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulToInventory", false);
    
    protected override bool DisablePUAH => false;
}