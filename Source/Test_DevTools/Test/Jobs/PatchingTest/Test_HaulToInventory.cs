using DevTools.Testing;
using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class Test_HaulToInventory(VehicleGroup group) : Test_HaulGeneral(group)
{
  public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulToInventory", false);

  public override Type BeforePatchingType => typeof(BeforePatching);

  public override Type AfterPatchingType => typeof(AfterPatching);

  protected override bool DisablePUAH => false;

  [LoadIfModsActive("Mehni.PickUpAndHaul")]
  [TestCategory("PickUpAndHaul")]
  private new class BeforePatching(Test_HaulToInventory parent) : Test_HaulGeneral.BeforePatching(parent);
  
  [LoadIfModsActive("Mehni.PickUpAndHaul")]
  [TestCategory("PickUpAndHaul")]
  private new class AfterPatching(Test_HaulToInventory parent) : Test_HaulGeneral.AfterPatching(parent);
}
