using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_WhileYoureUp
    {
        static Patches_WhileYoureUp()
        {
            if (ModCompat.WhileYoureUp)
            {
                MethodBase original;
                MethodInfo patch;

                //if (ModsConfig.IsActive("Mehni.PickUpAndHaul"))
                //{
                    //original = AccessTools.Method(typeof(StoreAcrossMapsUtility), nameof(StoreAcrossMapsUtility.TryFindBestBetterStoreCellFor));
                    //patch = AccessTools.Method("WhileYoureUp.Mod+StoreUtility__TryFindBestBetterStoreCellFor_Patch:DetourAware_TryFindStore");
                    //VMF_Harmony.Instance.Patch(original, prefix: patch);

                    //var type = AccessTools.TypeByName("VMF_PUAHPatch.WorkGiver_HaulToInventoryAcrossMaps");
                    //AccessTools.Field("WhileYoureUp.Mod:PuahType_WorkGiver_HaulToInventory").SetValue(null, type);
                    //AccessTools.Field("WhileYoureUp.Mod:PuahMethod_WorkGiver_HaulToInventory_HasJobOnThing").SetValue(null, AccessTools.Field(type, "HasJobOnThing"));
                    //AccessTools.Field("WhileYoureUp.Mod:PuahMethod_WorkGiver_HaulToInventory_JobOnThing").SetValue(null, AccessTools.Field(type, "JobOnThing"));
                    //AccessTools.Field("WhileYoureUp.Mod:PuahMethod_WorkGiver_HaulToInventory_TryFindBestBetterStoreCellFor").SetValue(null, AccessTools.Field(type, "TryFindBestBetterStoreCellFor"));
                    //AccessTools.Field("WhileYoureUp.Mod:PuahMethod_WorkGiver_HaulToInventory_AllocateThingAt").SetValue(null, AccessTools.Field(type, "AllocateThingAtCell"));
                    //AccessTools.Field("WhileYoureUp.Mod:PuahField_WorkGiver_HaulToInventory_SkipCells").SetValue(null, AccessTools.Field(type, "skipCells"));
                    //type = AccessTools.TypeByName("VMF_PUAHPatch.JobDriver_HaulToInventoryAcrossMaps");
                    //AccessTools.Field("WhileYoureUp.Mod:PuahType_JobDriver_HaulToInventory").SetValue(null, type);
                    //var harmony = (Harmony)AccessTools.Field("WhileYoureUp.Mod:harmony").GetValue(null);
                    //harmony.UnpatchAll("CodeOptimist.WhileYoureUp");
                    //harmony.PatchAll(AccessTools.TypeByName("WhileYoureUp.Mod").Assembly);
                //}

                original = AccessTools.Method(typeof(JobDriver_HaulToCellAcrossMaps), nameof(JobDriver_HaulToCellAcrossMaps.GetReport));
                patch = AccessTools.Method("WhileYoureUp.Mod+JobDriver_HaulToCell__GetReport_Patch:GetDetourReport");
                VMF_Harmony.Instance.Patch(original, postfix: patch);

                original = AccessTools.Method(typeof(JobDriver_HaulToCellAcrossMaps), "MakeNewToils");
                patch = AccessTools.Method("WhileYoureUp.Mod+JobDriver_HaulToCell__MakeNewToils_Patch:ClearDetourOnFinish");
                VMF_Harmony.Instance.Patch(original, postfix: patch);

                VMF_Harmony.Instance.PatchCategory("VMF_Patches_WhileYoureUp");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_WhileYoureUp")]
    [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResourcesAcrossMaps), "ResourceDeliverJobFor")]
    public static class Patch_WorkGiver_ConstructDeliverResourcesAcrossMaps_ResourceDeliverJobFor
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var m_FindAvailableNearbyResources = AccessTools.Method(typeof(WorkGiver_ConstructDeliverResourcesAcrossMaps), "FindAvailableNearbyResources");
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(m_FindAvailableNearbyResources));
            var label = generator.DefineLabel();
            codes[pos + 1].labels.Add(label);
            var f_need = AccessTools.FindIncludingInnerTypes(typeof(WorkGiver_ConstructDeliverResourcesAcrossMaps), (Type type) => AccessTools.DeclaredField(type, "need"));
            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_need));
            var f_foundRes = AccessTools.FindIncludingInnerTypes(typeof(WorkGiver_ConstructDeliverResourcesAcrossMaps), (Type type) => AccessTools.DeclaredField(type, "foundRes"));
            var pos3 = codes.FindLastIndex(pos, c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_foundRes));
            var l_job = codes.Find(c => c.opcode == OpCodes.Stloc_S && (c.operand as LocalBuilder).LocalIndex == 19).operand;

            codes.InsertRange(pos + 1, new[]
            {
                CodeInstruction.LoadArgument(1),
                new CodeInstruction(codes[pos2 - 1]),
                new CodeInstruction(codes[pos2]),
                CodeInstruction.LoadArgument(2),
                new CodeInstruction(OpCodes.Castclass, typeof(Thing)),
                new CodeInstruction(codes[pos3 - 1]),
                new CodeInstruction(codes[pos3]),
                CodeInstruction.Call(typeof(Patch_WorkGiver_ConstructDeliverResources__ResourceDeliverJobFor_Patch_BeforeSupplyDetour_Job), "BeforeSupplyDetour_Job"),
                new CodeInstruction(OpCodes.Stloc_S, l_job),
                new CodeInstruction(OpCodes.Ldloc_S, l_job),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, l_job),
                new CodeInstruction(OpCodes.Ret)
            });
            return codes;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_WhileYoureUp")]
    [HarmonyPatch("WhileYoureUp.Mod+WorkGiver_ConstructDeliverResources__ResourceDeliverJobFor_Patch", "BeforeSupplyDetour_Job")]
    public static class Patch_WorkGiver_ConstructDeliverResources__ResourceDeliverJobFor_Patch_BeforeSupplyDetour_Job
    {
        [HarmonyReversePatch]
        public static Job BeforeSupplyDetour_Job(Pawn pawn, ThingDefCountClass need, Thing constructible, Thing th)
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var f_WorkGiver_ConstructDeliverResources_resourcesAvailable = AccessTools.Field(typeof(WorkGiver_ConstructDeliverResources), "resourcesAvailable");
                var f_WorkGiver_ConstructDeliverResourcesAcrossMaps_resourcesAvailable = AccessTools.Field(typeof(WorkGiver_ConstructDeliverResourcesAcrossMaps), "resourcesAvailable");
                return instructions.Manipulator(c => c.opcode == OpCodes.Ldsfld && c.OperandIs(f_WorkGiver_ConstructDeliverResources_resourcesAvailable),
                    c => c.operand = f_WorkGiver_ConstructDeliverResourcesAcrossMaps_resourcesAvailable);

            }
            _ = Transpiler(null);
            throw new NotImplementedException();
        }
    }
}
