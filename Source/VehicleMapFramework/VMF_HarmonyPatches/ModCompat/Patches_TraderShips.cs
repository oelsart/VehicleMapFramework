using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_TraderShips
{
  static Patches_TraderShips()
  {
    if (TraderShips)
    {
      VMF_Harmony.PatchCategory(PatchCategories.TraderShips);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.TraderShips)]
[HarmonyPatch("TraderShips.CompShip", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompShip_PostDraw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    foreach (var instruction in instructions)
    {
      if (instruction.LoadsConstant(0f))
      {
        yield return CodeInstruction.LoadArgument(0);
        yield return ((Delegate)Rotation).Method.CallInstruction;
      }
      else
      {
        yield return instruction;
      }
    }
  }

  private static float Rotation(ThingComp comp)
  {
    return comp.parent.BaseFullRotationDoor().AsAngle;
  }
}

//車上マップにそれぞれVirtualMapTransferしてColonyThingsWillingToBuyを集める
[HarmonyPatchCategory(PatchCategories.TraderShips)]
[HarmonyPatch("TraderShips.LandedShip", "ColonyThingsWillingToBuy")]
[PatchLevel(Level.Safe)]
public static class Patch_LandedShip_ColonyThingsWillingToBuy
{

  private static bool working;

  public static IEnumerable<Thing> Postfix(IEnumerable<Thing> values, Pawn playerNegotiator, ITrader __instance)
  {
    if (values != null)
    {
      foreach (var thing in values)
      {
        yield return thing;
      }
    }
    if (working) yield break;

    var maps = playerNegotiator.Map.BaseMapAndVehicleMaps(false);
    var departMap = playerNegotiator.Map;
    CrossMapReachabilityUtility.DepartMapGlobal = departMap;
    try
    {
      working = true;
      foreach (var map in maps)
      {
        playerNegotiator.VirtualMapTransfer(map);
        var things = __instance.ColonyThingsWillingToBuy(playerNegotiator).ToList();
        foreach (var thing in things)
        {
          yield return thing;
        }
      }
    }
    finally
    {
      working = false;
      playerNegotiator.VirtualMapTransfer(departMap);
      CrossMapReachabilityUtility.DepartMapGlobal = null;
    }
  }
}
