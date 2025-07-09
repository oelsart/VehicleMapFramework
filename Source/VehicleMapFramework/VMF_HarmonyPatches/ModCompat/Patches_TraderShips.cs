using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_TranderShips
{
    static Patches_TranderShips()
    {
        if (ModCompat.TraderShips)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_TraderShips");
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_TraderShips")]
[HarmonyPatch("TraderShips.CompShip", "PostDraw")]
public static class Patch_CompShip_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsConstant(0f))
            {
                yield return CodeInstruction.LoadArgument(0);
                yield return CodeInstruction.Call(typeof(Patch_CompShip_PostDraw), nameof(Rotation));
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
[HarmonyPatchCategory("VMF_Patches_TraderShips")]
[HarmonyPatch("TraderShips.LandedShip", "ColonyThingsWillingToBuy")]
public static class Patch_LandedShip_ColonyThingsWillingToBuy
{
    public static void Prefix(Pawn playerNegotiator)
    {
        if (working) return;

        CrossMapReachabilityUtility.tmpDepartMap = playerNegotiator.Map;
    }

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

        var maps = playerNegotiator.Map.BaseMapAndVehicleMaps().Except(playerNegotiator.Map);
        if (!maps.Any()) yield break;
        var departMap = playerNegotiator.Map;
        try
        {
            working = true;
            foreach (var map in maps)
            {
                playerNegotiator.VirtualMapTransfer(map);
                foreach (var thing in __instance.ColonyThingsWillingToBuy(playerNegotiator))
                {
                    yield return thing;
                }
            }
        }
        finally
        {
            working = false;
            playerNegotiator.VirtualMapTransfer(departMap);
            CrossMapReachabilityUtility.tmpDepartMap = null;
        }
    }

    private static bool working;
}
