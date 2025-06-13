using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using static VehicleInteriors.MethodInfoCache;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_CallTradeShips
    {
        static Patches_CallTradeShips()
        {
            if (ModCompat.CallTradeShips)
            {
                var method = AccessTools.Method(typeof(FloatMenuMakerOnVehicle), "AddHumanlikeOrders");
                var patch = AccessTools.Method(typeof(Patches_CallTradeShips), nameof(Postfix));
                var patchOrig = AccessTools.Method("CallTradeShips.Patch_FloatMenuMakerMap_AddHumanlikeOrders:Postfix");
                VMF_Harmony.Instance.CreateReversePatcher(patchOrig, patch).Patch();
                VMF_Harmony.Instance.Patch(method, postfix: patch);

                VMF_Harmony.Instance.PatchCategory("VMF_Patches_CallTradeShips");
            }
        }

        private static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return instructions.MethodReplacer(CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps)
                    .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
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
                __result.def = __instance.def;
                __result.targetA = __instance.targetA;
                __result.targetB = __instance.targetB;
                __result.targetC = __instance.targetC;
                __result.targetQueueA = __instance.targetQueueA;
                __result.targetQueueB = __instance.targetQueueB;
                __result.globalTarget = __instance.globalTarget;
                __result.count = __instance.count;
                __result.countQueue = __instance.countQueue;
                __result.loadID = __instance.loadID;
                __result.expiryInterval = __instance.expiryInterval;
                __result.checkOverrideOnExpire = __instance.checkOverrideOnExpire;
                __result.playerForced = __instance.playerForced;
                __result.showCarryingInspectLine = __instance.showCarryingInspectLine;
                __result.placedThings = __instance.placedThings;
                __result.maxNumMeleeAttacks = __instance.maxNumMeleeAttacks;
                __result.maxNumStaticAttacks = __instance.maxNumStaticAttacks;
                __result.locomotionUrgency = __instance.locomotionUrgency;
                __result.haulMode = __instance.haulMode;
                __result.bill = __instance.bill;
                __result.commTarget = __instance.commTarget;
                __result.plantDefToSow = __instance.plantDefToSow;
                __result.thingDefToCarry = __instance.thingDefToCarry;
                __result.verbToUse = __instance.verbToUse;
                __result.haulOpportunisticDuplicates = __instance.haulOpportunisticDuplicates;
                __result.exitMapOnArrival = __instance.exitMapOnArrival;
                __result.failIfCantJoinOrCreateCaravan = __instance.failIfCantJoinOrCreateCaravan;
                __result.killIncappedTarget = __instance.killIncappedTarget;
                __result.ignoreForbidden = __instance.ignoreForbidden;
                __result.ignoreDesignations = __instance.ignoreDesignations;
                __result.canBashDoors = __instance.canBashDoors;
                __result.canBashFences = __instance.canBashFences;
                __result.canUseRangedWeapon = __instance.canUseRangedWeapon;
                __result.haulDroppedApparel = __instance.haulDroppedApparel;
                __result.restUntilHealed = __instance.restUntilHealed;
                __result.ignoreJoyTimeAssignment = __instance.ignoreJoyTimeAssignment;
                __result.doUntilGatheringEnded = __instance.doUntilGatheringEnded;
                __result.overeat = __instance.overeat;
                __result.ingestTotalCount = __instance.ingestTotalCount;
                __result.attackDoorIfTargetLost = __instance.attackDoorIfTargetLost;
                __result.takeExtraIngestibles = __instance.takeExtraIngestibles;
                __result.expireRequiresEnemiesNearby = __instance.expireRequiresEnemiesNearby;
                __result.expireOnEnemiesNearby = __instance.expireOnEnemiesNearby;
                __result.instancedExpiryInterval = __instance.instancedExpiryInterval;
                __result.lord = __instance.lord;
                __result.collideWithPawns = __instance.collideWithPawns;
                __result.forceSleep = __instance.forceSleep;
                __result.interaction = __instance.interaction;
                __result.endIfCantShootTargetFromCurPos = __instance.endIfCantShootTargetFromCurPos;
                __result.endIfCantShootInMelee = __instance.endIfCantShootInMelee;
                __result.checkEncumbrance = __instance.checkEncumbrance;
                __result.followRadius = __instance.followRadius;
                __result.endAfterTendedOnce = __instance.endAfterTendedOnce;
                __result.quest = __instance.quest;
                __result.mote = __instance.mote;
                __result.psyfocusTargetLast = __instance.psyfocusTargetLast;
                __result.wasOnMeditationTimeAssignment = __instance.wasOnMeditationTimeAssignment;
                __result.reactingToMeleeThreat = __instance.reactingToMeleeThreat;
                __result.preventFriendlyFire = __instance.preventFriendlyFire;
                __result.ropingPriority = __instance.ropingPriority;
                __result.ropeToUnenclosedPens = __instance.ropeToUnenclosedPens;
                __result.showSpeechBubbles = __instance.showSpeechBubbles;
                __result.lookDirection = __instance.lookDirection;
                __result.overrideFacing = __instance.overrideFacing;
                __result.forceMaintainFacing = __instance.forceMaintainFacing;
                __result.dutyTag = __instance.dutyTag;
                __result.ritualTag = __instance.ritualTag;
                __result.controlGroupTag = __instance.controlGroupTag;
                __result.takeInventoryDelay = __instance.takeInventoryDelay;
                __result.draftedTend = __instance.draftedTend;
                __result.speechFaceSpectatorsIfPossible = __instance.speechFaceSpectatorsIfPossible;
                __result.speechSoundMale = __instance.speechSoundMale;
                __result.speechSoundFemale = __instance.speechSoundFemale;
                __result.biosculpterCycleKey = __instance.biosculpterCycleKey;
                __result.reportStringOverride = __instance.reportStringOverride;
                __result.crawlingReportStringOverride = __instance.crawlingReportStringOverride;
                __result.startInvoluntarySleep = __instance.startInvoluntarySleep;
                __result.isLearningDesire = __instance.isLearningDesire;
                __result.jobGiverThinkTree = __instance.jobGiverThinkTree;
                __result.jobGiver = __instance.jobGiver;
                __result.workGiverDef = __instance.workGiverDef;
                __result.ability = __instance.ability;
                __result.source = __instance.source;
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
