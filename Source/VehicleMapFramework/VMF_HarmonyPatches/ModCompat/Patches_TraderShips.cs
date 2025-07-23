using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_TranderShips
{
    public const string Category = "VMF_Patches_TraderShips";

    static Patches_TranderShips()
    {
        if (ModCompat.TraderShips)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_TranderShips.Category)]
[HarmonyPatch("TraderShips.CompShip", "PostDraw")]
public static class Patch_CompShip_PostDraw
{
    [PatchLevel(Level.Sensitive)]
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
[HarmonyPatchCategory(Patches_TranderShips.Category)]
[HarmonyPatch("TraderShips.LandedShip", "ColonyThingsWillingToBuy")]
public static class Patch_LandedShip_ColonyThingsWillingToBuy
{
    [PatchLevel(Level.Safe)]
    public static void Prefix(Pawn playerNegotiator)
    {
        if (working) return;

        CrossMapReachabilityUtility.DepartMap = playerNegotiator.Map;
    }

    [PatchLevel(Level.Safe)]
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
            CrossMapReachabilityUtility.DepartMap = null;
        }
    }

    private static bool working;
}
