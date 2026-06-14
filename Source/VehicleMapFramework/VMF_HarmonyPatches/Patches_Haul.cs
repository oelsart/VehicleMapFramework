using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.PawnCanAutomaticallyHaul))]
[PatchLevel(Level.Sensitive)]
public static class Patch_HaulAIUtility_PawnCanAutomaticallyHaul
{
  public static void Prefix(Pawn p, Thing t, ref VirtualTeleporter? __state)
  {
    if (p.Map != t.Map)
    {
      __state = new VirtualTeleporter(p, t.Map);
    }
  }

  public static void Finalizer(VirtualTeleporter? __state)
  {
    __state?.Dispose();
  }
}

[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStorageFor))]
public static class Patch_StoreUtility_TryFindBestBetterStorageFor
{
  [PatchLevel(Level.Sensitive)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    var m_GetSlotGroup = ((Func<IntVec3, Map, SlotGroup>)StoreUtility.GetSlotGroup).Method;
    var f_tmpDestMap = AccessTools.Field(typeof(StoreAcrossMapsUtility), nameof(StoreAcrossMapsUtility.tmpDestMap));
    foreach (var instruction in instructions)
    {
      if (instruction.Calls(m_GetSlotGroup))
      {
        var label = generator.DefineLabel();
        yield return new CodeInstruction(OpCodes.Ldsfld, f_tmpDestMap);
        yield return new CodeInstruction(OpCodes.Brfalse_S, label);
        yield return new CodeInstruction(OpCodes.Pop);
        yield return new CodeInstruction(OpCodes.Ldsfld, f_tmpDestMap);
        yield return instruction.WithLabels(label);
      }
      else
      {
        yield return instruction;
      }
    }
  }

  [PatchLevel(Level.Safe)]
  public static void Postfix(Pawn carrier, IHaulDestination haulDestination, IntVec3 foundCell)
  {
    if (haulDestination?.Map != null)
    {
      carrier.TargetInfo = new TargetInfo(foundCell, haulDestination.Map);
    }
  }
}

[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStoreCellFor))]
[PatchLevel(Level.Safe)]
public static class Patch_StoreUtility_TryFindBestBetterStoreCellFor
{
  public static void Postfix(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction,
    ref IntVec3 foundCell, bool needAccurateResult, ref bool __result)
  {
    carrier.RemoveTargetInfo();
    var priority = foundCell.IsValid
      ? foundCell.GetSlotGroup(map)?.Settings?.Priority ?? currentPriority
      : currentPriority;
    __result |= StoreAcrossMapsUtility.TryFindBestBetterStoreCellFor(t, carrier, map, priority, faction, ref foundCell,
      needAccurateResult);
    if (StoreAcrossMapsUtility.tmpDestMap != null)
    {
      carrier.TargetInfo = new TargetInfo(foundCell, StoreAcrossMapsUtility.tmpDestMap ?? map);
    }
  }
}

[HarmonyPatch(typeof(StoreUtility), "TryFindBestBetterStoreCellForWorker")]
[PatchLevel(Level.Safe)]
public static class Patch_StoreUtility_TryFindBestBetterStoreCellForWorker
{
  public static bool Prefix(Thing t, Pawn carrier, Map map, Faction faction, ISlotGroup slotGroup,
    bool needAccurateResult, ref IntVec3 closestSlot, ref float closestDistSquared, ref StoragePriority foundPriority)
  {
    var owner = slotGroup?.Settings?.owner;
    var destMap = owner switch
    {
      StorageGroup storageGroup => storageGroup.Map,
      IHaulDestination haulDestination => haulDestination.Map,
      IHaulSource haulSource => haulSource.Map,
      _ => null
    };

    if (destMap is not null && destMap != map)
    {
      StoreAcrossMapsUtility.TryFindBestBetterStoreCellForWorker(t, carrier, destMap, faction, slotGroup,
        needAccurateResult, ref closestSlot, ref closestDistSquared, ref foundPriority);
      return false;
    }

    return true;
  }
}

// IsGoodStoreCell内ではtの場所からCanReachする。主にJobのcount計算用
[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell))]
[PatchLevel(Level.Safe)]
public static class Patch_StoreUtility_IsGoodStoreCell
{
  public static bool Prefix(IntVec3 c, Map map, Thing t, Pawn carrier, Faction faction, ref bool __result)
  {
    if (carrier is null) return true;
    var departMap = carrier.DepartMap;
    var targetMap = carrier.TargetMap;
    if (departMap is not null || targetMap is not null)
    {
      carrier.RemoveDepartMap();
      __result = StoreAcrossMapsUtility.IsGoodStoreCell(c, targetMap ?? map, t, carrier, faction);
      carrier.DepartMap = departMap;
      return false;
    }

    return true;
  }
}

[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterNonSlotGroupStorageFor))]
[PatchLevel(Level.Safe)]
public static class Patch_StoreUtility_TryFindBestBetterNonSlotGroupStorageFor
{
  public static void Postfix(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction,
    ref IHaulDestination haulDestination, bool acceptSamePriority, bool requiresDestReservation, ref bool __result)
  {
    var priority = haulDestination is not null
      ? haulDestination.GetParentStoreSettings()?.Priority ?? currentPriority
      : currentPriority;
    __result |= StoreAcrossMapsUtility.TryFindBestBetterNonSlotGroupStorageFor(t, carrier, map, priority, faction,
      ref haulDestination, acceptSamePriority, requiresDestReservation);
  }
}

[HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.HaulToCellStorageJob))]
public static class Patch_HaulAIUtility_HaulToCellStorageJob
{
  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrPawnMap);
  }

  [HarmonyBefore(StackGap.HarmonyId)]
  [PatchLevel(Level.Safe)]
  public static void Postfix(Pawn p, IntVec3 storeCell, Job __result)
  {
    if (p.TryGetTargetMap(out var map))
    {
      __result?.globalTarget = new GlobalTargetInfo(storeCell, map);
    }
  }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Toils_Haul_CarryHauledThingToCell
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    return AccessTools.InnerTypes(typeof(Toils_Haul))
      .Select(t => t.GetDeclaredMethods())
      .Select(methods => methods.FirstOrDefault(m =>
      {
        if (m.Name.Contains("DupeValidator"))
        {
          return true;
        }

        return m.Name.Contains("<CarryHauledThingToCell>") &&
               m.GetMethodBody()!.LocalVariables
                 .Select(l => l.LocalType)
                 .SequenceEqual(
                   [typeof(Pawn), typeof(IntVec3), typeof(CompPushable), typeof(LocalTargetInfo)]);
      })).Where(method => method != null);
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrPawnMap);
  }
}

[HarmonyPatch(typeof(LoadTransportersJobUtility), nameof(LoadTransportersJobUtility.FindThingToLoad))]
public static class Patch_LoadTransportersJobUtility_FindThingToLoad
{
  [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
  [PatchLevel(Level.Mandatory)]
  public static ThingCount FindThingToLoad(Pawn p, CompTransporter transporter)
  {
    _ = Transpiler(null);
    throw new NotImplementedException();

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      var codes = new CodeMatcher(instructions);
      codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position));
      codes.Insert(
        new CodeInstruction(OpCodes.Pop),
        CodeInstruction.LoadArgument(1),
        CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent)));

      codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map));
      codes.Insert(
        new CodeInstruction(OpCodes.Pop),
        CodeInstruction.LoadArgument(1),
        CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent)));

      var m_ClosestThingReachable = ((Delegate)GenClosest.ClosestThingReachable).Method;
      var m_ClosestThingReachableOriginal = ((Delegate)Patch_GenClosest_ClosestThingReachable.ClosestThingReachableOriginal).Method;
      return codes.Instructions().MethodReplacer(m_ClosestThingReachable, m_ClosestThingReachableOriginal);
    }
  }

  [PatchLevel(Level.Safe)]
  public static bool Prefix(Pawn p, CompTransporter transporter, ref ThingCount __result)
  {
    if (transporter is CompBuildableContainer { GatherFromBaseMap: false })
    {
      __result = FindThingToLoad(p, transporter);
      return false;
    }

    return true;
  }
}

[HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.FindFixedIngredientCount))]
public static class Patch_HaulAIUtility_FindFixedIngredientCount
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_DepartMapOrPawnMap),
      (CachedMethodInfo.m_BreadthFirstTraverse, CachedMethodInfo.m_BreadthFirstTraverseAcrossMaps));
  }
}

[HarmonyPatch(typeof(JobDriver_HaulToContainer), "TryReplaceWithFrame")]
[PatchLevel(Level.Cautious)]
public static class Patch_JobDriver_HaulToContainer_TryReplaceWithFrame
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrPawnMap);
  }
}