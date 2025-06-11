using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_CallTradeShips
    {
        static Patches_CallTradeShips()
        {
            if (ModCompat.CallTradeShips)
            {
                //var method = AccessTools.Method(typeof(FloatMenuMakerOnVehicle), "AddHumanlikeOrders");
                //var patch = AccessTools.Method(typeof(Patches_CallTradeShips), nameof(Postfix));
                //var patchOrig = AccessTools.Method("CallTradeShips.Patch_FloatMenuMakerMap_AddHumanlikeOrders:Postfix");
                //VMF_Harmony.Instance.CreateReversePatcher(patchOrig, patch).Patch();
                //VMF_Harmony.Instance.Patch(method, postfix: patch);

                VMF_Harmony.Instance.PatchCategory("VMF_Patches_CallTradeShips");
            }
        }

        private static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return instructions.MethodReplacer(MethodInfoCache.m_GetThingList, MethodInfoCache.m_GetThingListAcrossMaps)
                    .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
            }
            _ = Transpiler(null);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_CallTradeShips")]
    [HarmonyPatch(typeof(Job), nameof(Job.Clone))]
    public static class Patch_Job_Clone
    {
        public static bool Prefix(Job __instance, ref Job __result)
        {
            if (__instance.GetType() == t_Job_CallTradeShip)
            {
                __result = (Job)t_Job_CallTradeShip.CreateInstance();
                __result = __instance.Clone();
                TraderKindDef(__result) = TraderKindDef(__instance);
                TraderKind(__result) = TraderKind(__instance);
                return false;
            }
            return true;
        }

        private static readonly Type t_Job_CallTradeShip = AccessTools.TypeByName("CallTradeShips.Job_CallTradeShip");

        private static readonly AccessTools.FieldRef<Job, TraderKindDef> TraderKindDef = AccessTools.FieldRefAccess<TraderKindDef>(t_Job_CallTradeShip, "TraderKindDef");

        private static readonly AccessTools.FieldRef<Job, int> TraderKind = AccessTools.FieldRefAccess<int>(t_Job_CallTradeShip, "TraderKind");
    }
}
