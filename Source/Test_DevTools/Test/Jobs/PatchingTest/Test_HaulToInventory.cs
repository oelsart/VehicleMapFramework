using DevTools.Testing;
using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class Test_HaulToInventory(VehicleGroup group) : Test_HaulGeneral(group)
{
  public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulToInventory", false);

  protected override bool DisablePUAH => false;

  [LoadIfModsActive("Mehni.PickUpAndHaul")]
  [TestCategory("PickUpAndHaul")]
  private new class BeforePatching(Test_HaulGeneral parent) : Test_HaulGeneral.BeforePatching(parent);
}
