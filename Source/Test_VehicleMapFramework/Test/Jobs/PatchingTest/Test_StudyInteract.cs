using DevTools.Testing;
using HarmonyLib;
using RimWorld;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_DevTools;

internal sealed class Test_StudyInteract(VehicleGroup group) : WorkGiverTestBase(group)
{
  public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("StudyInteract");

  public override Type BeforePatchingType => typeof(BeforePatching);

  public override Type AfterPatchingType => typeof(AfterPatching);

  private const string OpenActivatedDialog = "OpenActivatedDialog";

  [LoadIfAnomalyActive]
  private new class BeforePatching(Test_StudyInteract parent) : WorkGiverTestBase.BeforePatching(parent)
  {
    public override void SetUp()
    {
      VMF_Harmony.Instance.Patch(
        AccessTools.Method(typeof(Building_VoidMonolith), OpenActivatedDialog),
        ((Delegate)PatchSuppressDialog).Method);
      var component = Current.Game.GetComponent<GameComponent_Anomaly>();
      component.lastLevelActivationLetterSent = 6;
      var cell = FromRUCorner(Pawn.Map, 3);
      if (component.MonolithSpawned && component.monolith.Map == Pawn.Map)
        component.monolith.DeSpawn();
      component.SpawnNewMonolith(cell, Pawn.Map);

      Pawn.pather.TryRecoverFromUnwalkablePosition();
      component.monolith.Activate(Pawn);
    }
  }

  [LoadIfAnomalyActive]
  private new class AfterPatching(Test_StudyInteract parent) : WorkGiverTestBase.AfterPatching(parent)
  {
    public override void TearDown()
    {
      Thing.allowDestroyNonDestroyable = true;
      Current.Game.GetComponent<GameComponent_Anomaly>().monolith.Destroy();
      Thing.allowDestroyNonDestroyable = false;
      VMF_Harmony.Instance.Unpatch(
        AccessTools.Method(typeof(Building_VoidMonolith), OpenActivatedDialog),
        ((Delegate)PatchSuppressDialog).Method);
    }
  }

  public static bool PatchSuppressDialog() => false;
}