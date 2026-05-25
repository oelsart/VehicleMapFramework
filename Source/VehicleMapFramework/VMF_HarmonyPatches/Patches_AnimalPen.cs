using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(AnimalPenUtility), nameof(AnimalPenUtility.NeedsToBeManagedByRope))]
[PatchLevel(Level.Cautious)]
public static class Patch_AnimalPenUtility_NeedsToBeManagedByRope
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatch(typeof(AnimalPenUtility), nameof(AnimalPenUtility.AnySuitablePens))]
[PatchLevel(Level.Safe)]
public static class Patch_AnimalPenUtility_AnySuitablePens
{
  public static void Postfix(Pawn animal, bool allowUnenclosedPens, ref bool __result)
  {
    if (!__result)
    {
      var animalMap = animal.Map;
      _ = animal.Position;
      var maps = animalMap.BaseMapAndVehicleMaps(false);
      foreach (var map in maps)
      {
        foreach (var thing in map.listerBuildings.allBuildingsAnimalPenMarkers)
        {
          var penMarker = thing.TryGetComp<CompAnimalPenMarker>();
          if (AnimalPenUtilityOnVehicle.CanUseAndReach(animal, penMarker, allowUnenclosedPens))
          {
            __result = true;
            return;
          }
        }
      }
    }
  }
}

[HarmonyPatch(typeof(AnimalPenUtility), nameof(AnimalPenUtility.AnySuitableHitch))]
[PatchLevel(Level.Safe)]
public static class Patch_AnimalPenUtility_AnySuitableHitch
{
  public static void Postfix(Pawn animal, ref bool __result)
  {
    if (!__result)
    {
      var animalMap = animal.Map;
      var maps = animalMap.BaseMapAndVehicleMaps(false);
      foreach (var map in maps)
      {
        foreach (var thing in map.listerBuildings.allBuildingsAnimalPenMarkers)
        {
          if (animal.CanReach(thing, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, thing.Map))
          {
            __result = true;
            return;
          }
        }
      }
    }
  }
}

[HarmonyPatch(typeof(AnimalPenUtility), nameof(AnimalPenUtility.ClosestSuitablePen))]
[PatchLevel(Level.Safe)]
public static class Patch_AnimalPenUtility_ClosestSuitablePen
{
  public static void Postfix(Pawn animal, bool allowUnenclosedPens, ref CompAnimalPenMarker __result)
  {
    if (__result == null)
    {
      var animalMap = animal.Map;
      var num = 0f;
      foreach (var map in animalMap.BaseMapAndVehicleMaps(false))
      {
        foreach (var thing in map.listerBuildings.allBuildingsAnimalPenMarkers)
        {
          var compAnimalPenMarker2 = thing.TryGetComp<CompAnimalPenMarker>();
          if (AnimalPenUtilityOnVehicle.CanUseAndReach(animal, compAnimalPenMarker2, allowUnenclosedPens))
          {
            var num2 = animal.PositionOnBaseMap.DistanceToSquared(compAnimalPenMarker2.parent.PositionOnBaseMap);
            if (__result == null || num2 < num)
            {
              __result = compAnimalPenMarker2;
              num = num2;
            }
          }
        }
      }
    }
  }
}

[HarmonyPatch(typeof(AnimalPenUtility), nameof(AnimalPenUtility.GetPenAnimalShouldBeTakenTo))]
[PatchLevel(Level.Sensitive)]
public static class Patch_AnimalPenUtility_GetPenAnimalShouldBeTakenTo
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var f_allPenMarkers = AccessTools.Field(typeof(ListerBuildings), nameof(ListerBuildings.allBuildingsAnimalPenMarkers));
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_allPenMarkers)) + 1;

    codes.InsertRange(pos,
    [
      CodeInstruction.LoadLocal(1),
      CodeInstruction.Call(typeof(Patch_AnimalPenUtility_GetPenAnimalShouldBeTakenTo), nameof(AddPenMarkers))
    ]);

    return codes.MethodReplacer(AccessTools.Method(typeof(AnimalPenUtility), "CheckUseAndReach"),
      AccessTools.Method(typeof(AnimalPenUtilityOnVehicle), nameof(AnimalPenUtilityOnVehicle.CheckUseAndReach)));
  }

  private static HashSet<Building> AddPenMarkers(HashSet<Building> hashSet, Map map)
  {
    var result = new HashSet<Building>(hashSet);
    var maps = map.BaseMapAndVehicleMaps(false);
    foreach (var map2 in maps)
    {
      result.AddRange(map2.listerBuildings.allBuildingsAnimalPenMarkers);
    }
    return result;
  }
}

[HarmonyPatch(typeof(AnimalPenUtility), nameof(AnimalPenUtility.GetHitchingPostAnimalShouldBeTakenTo))]
[PatchLevel(Level.Sensitive)]
public static class Patch_AnimalPenUtility_GetHitchingPostAnimalShouldBeTakenTo
{
  private static readonly HashSet<Building> tmpBuildings = [];

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.LoadsField(AccessTools.Field(typeof(ListerBuildings),
        nameof(ListerBuildings.allBuildingsHitchingPosts))))
      .InsertAfter(
        CodeInstruction.LoadArgument(1),
        CodeInstruction.Call(typeof(Patch_AnimalPenUtility_GetHitchingPostAnimalShouldBeTakenTo), nameof(AddHitchingPosts)))
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position)).Advance()
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position))
      .Repeat(matcher => matcher.Set(OpCodes.Call, CachedMethodInfo.m_PositionOnBaseMap))
      .InstructionEnumeration();
  }

  private static HashSet<Building> AddHitchingPosts(HashSet<Building> hashSet, Pawn animal)
  {
    tmpBuildings.Clear();
    tmpBuildings.AddRange(hashSet);
    tmpBuildings.AddRange(animal.Map.BaseMapAndVehicleMaps(false).SelectMany(m => m.listerBuildings.allBuildingsHitchingPosts));
    return tmpBuildings;
  }
}

[HarmonyPatch(typeof(AnimalPenUtility), "PenIsCloser")]
[PatchLevel(Level.Cautious)]
public static class Patch_AnimalPenUtility_PenIsCloser
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}

[HarmonyPatch(typeof(JobDriver_RopeToDestination), "MakeNewToils")]
[PatchLevel(Level.Safe)]
public static class Patch_JobDriver_RopeToDestination_MakeNewToils
{
  public static IEnumerable<Toil> Postfix(IEnumerable<Toil> values)
  {
    foreach (var toil in values)
    {
      if (toil.debugName == "GotoThing")
      {
        toil.AddPreInitAction(() =>
        {
          var actor = toil.actor;
          if (actor.CurJob.targetC.HasThing && actor.Map != actor.CurJob.targetC.Thing.Map &&
              actor.CanReach(actor.CurJob.targetC.Thing,
                PathEndMode.Touch,
                Danger.Deadly,
                false,
                false,
                TraverseMode.ByPawn,
                actor.CurJob.targetC.Thing.Map,
                out var exitSpot,
                out var enterSpot,
                out var spotsQueue))
          {
            JobAcrossMapsUtility.StartGotoDestMapJob(actor, exitSpot, enterSpot, spotsQueue);
          }
        });
      }
      yield return toil;
    }
  }
}

[HarmonyPatch(typeof(Toils_Rope), nameof(Toils_Rope.GotoRopeAttachmentInteractionCell))]
[PatchLevel(Level.Safe)]
public static class Patch_Toils_Rope_GotoRopeAttachmentInteractionCell
{
  public static void Postfix(Toil __result, TargetIndex ropeeIndex)
  {
    __result.AddPreInitAction(() =>
    {
      var actor = __result.actor;
      if (actor.CurJob.GetTarget(ropeeIndex).Thing is not Pawn ropee) return;
      if (actor.Map != ropee.Map && actor.CanReach(ropee,
            PathEndMode.Touch,
            Danger.Deadly,
            false,
            false,
            TraverseMode.ByPawn,
            ropee.Map,
            out var exitSpot,
            out var enterSpot,
            out var spotsQueue))
      {
        JobAcrossMapsUtility.StartGotoDestMapJob(actor, exitSpot, enterSpot, spotsQueue);
      }
    });
  }
}

[HarmonyPatch(typeof(Pawn_RopeTracker), "IsStillDoingRopingJob")]
[PatchLevel(Level.Safe)]
public static class Patch_Pawn_RopeTracker_IsStillDoingRopingJob
{
  public static void Postfix(Pawn roper, ref bool __result)
  {
    __result = __result || roper.jobs.curDriver is JobDriver_GotoDestMap gotoDestMap && gotoDestMap.nextJob.GetCachedDriver(roper) is JobDriver_RopeToDestination;
  }
}

[HarmonyPatch(typeof(JobGiver_FollowRoper), "TryGiveJob")]
[PatchLevel(Level.Safe)]
public static class Patch_JobGiver_FollowRoper_TryGiveJob
{
  public static void Postfix(Pawn pawn, ref Job __result)
  {
    if (__result == null)
    {
      Pawn roper;
      if ((roper = pawn?.roping?.RopedByPawn)?.jobs?.curDriver is JobDriver_GotoDestMap gotoDestMap &&
          gotoDestMap.nextJob.GetCachedDriver(roper) is JobDriver_RopeToDestination &&
          gotoDestMap.nextJob.targetB.Cell.IsValid && roper.Spawned && pawn.CanReach(roper, PathEndMode.Touch, Danger.Deadly))
      {
        var job = JobMaker.MakeJob(JobDefOf.FollowRoper, roper);
        job.expiryInterval = 140;
        job.checkOverrideOnExpire = true;
        __result = job;
      }
    }
  }
}

[HarmonyPatch(typeof(AnimalPenUtility), nameof(AnimalPenUtility.FindPlaceInPenToStand))]
[PatchLevel(Level.Sensitive)]
public static class Patch_AnimalPenUtility_FindPlaceInPenToStand
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Map));
    codes[pos - 2] = CodeInstruction.LoadArgument(0);
    codes[pos - 1] = CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent));
    return codes;
  }
}
