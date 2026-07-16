using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_Royalty
{
  static Patches_Royalty()
  {
    if (ModsConfig.RoyaltyActive)
    {
      VMF_Harmony.PatchCategory(PatchCategories.Royalty);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.Royalty)]
[HarmonyPatch(typeof(MeditationUtility), nameof(MeditationUtility.DrawMeditationSpotOverlay))]
[PatchLevel(Level.Sensitive)]
public static class Patch_MeditationUtility_DrawMeditationSpotOverlay
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.Calls(CachedMethodInfo.m_GenThing_TrueCenter1)) - 1;
    codes.InsertRange(pos,
    [
      CodeInstruction.LoadArgument(0),
      new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_FocusedOrSelectedDrawPosOffset)
    ]);
    return codes;
  }
}

[HarmonyPatchCategory(PatchCategories.Royalty)]
[HarmonyPatch(typeof(MeditationUtility), nameof(MeditationUtility.GetMeditationJob))]
[PatchLevel(Level.Safe)]
public static class Patch_MeditationUtility_GetMeditationJob
{
  public static void Postfix(Pawn pawn, bool forJoy, ref Job __result)
  {
    var score = __result is not null
      ? GetMeditationScore(__result.targetA, __result.targetC, pawn, pawn.ownership.OwnedRoom)
      : 0f;
    foreach (var map in pawn.Map.BaseMapAndVehicleMaps(false))
    {
      using var _ = new VirtualTeleporter(pawn, map, pawn.PositionOnAnotherMap(map), true);
      
      // 元のGetMeditationJobからWanderSpotの取得を除いたJob取得を行う
      var job = GetMeditationJob(pawn, forJoy, score, out var score2);
      if (job is not null &&
          score2 > score &&
          pawn.CanReach(job.targetA, PathEndMode.OnCell, pawn.NormalMaxDanger(), false, false,
            TraverseMode.ByPawn, map,
            out var exitSpot, out var enterSpot, out var spotsQueue))
      {
        score = score2;
        __result = JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, spotsQueue, job);
      }
    }
  }
  
  private static Job GetMeditationJob(Pawn pawn, bool forJoy, float initialScore, out float score)
  {
    var meditationSpotAndFocus = FindMeditationSpot(pawn, initialScore, out score);
    if (meditationSpotAndFocus.IsValid &&
        pawn.CanReserveAndReach(meditationSpotAndFocus.spot, PathEndMode.OnCell, pawn.NormalMaxDanger()))
    {
      Job job;
      if (meditationSpotAndFocus.focus.Thing is Building_Throne building_Throne)
      {
        job = JobMaker.MakeJob(JobDefOf.Reign, building_Throne, null, building_Throne);
      }
      else
      {
        var def = JobDefOf.Meditate;
        if (forJoy && ModsConfig.IdeologyActive &&
            pawn.Ideo is { foundation: IdeoFoundation_Deity ideoFoundation_Deity } &&
            ideoFoundation_Deity.DeitiesListForReading.Any())
        {
          def = JobDefOf.MeditatePray;
        }
        job = JobMaker.MakeJob(def, meditationSpotAndFocus.spot, null, meditationSpotAndFocus.focus);
      }
      job.ignoreJoyTimeAssignment = !forJoy;
      return job;
    }
    return null;
  }
  
  private static MeditationSpotAndFocus FindMeditationSpot(Pawn pawn, float initialScore, out float score)
  {
    score = initialScore;
    var spot = LocalTargetInfo.Invalid;
    var focus = LocalTargetInfo.Invalid;
    if (!ModLister.CheckRoyalty("Psyfocus"))
    {
      return new MeditationSpotAndFocus(spot, focus);
    }
    var ownedRoom = pawn.ownership.OwnedRoom;
    foreach (var spot2 in AllMeditationSpotCandidates(pawn))
    {
      if (!MeditationUtility.SafeEnvironmentalConditions(pawn, spot2.Cell, pawn.Map) ||
          !spot2.Cell.Standable(pawn.Map) || spot2.Cell.IsForbidden(pawn))
        continue;
      
      var focus2 = spot2.Thing is Building_Throne
        ? (LocalTargetInfo)spot2.Thing
        : MeditationUtility.BestFocusAt(spot2, pawn);
      var score2 = GetMeditationScore(spot2, focus2, pawn, ownedRoom);
      if (score2 > score)
      {
        spot = spot2;
        focus = focus2;
        score = score2;
      }
    }
    return new MeditationSpotAndFocus(spot, focus);
  }

  private static float GetMeditationScore(LocalTargetInfo spot, LocalTargetInfo focus, Pawn pawn, Room ownedRoom)
  {
    var score = 1f / Mathf.Max(spot.Cell.DistanceToSquared(pawn.Position), 0.1f);
    if (pawn.HasPsylink && focus.IsValid)
    {
      score += focus.Thing.GetStatValueForPawn(StatDefOf.MeditationFocusStrength, pawn) * 100f;
    }
    var room = spot.Cell.GetRoom(pawn.Map);
    if (room != null && ownedRoom == room)
      score += 1f;
      
    if (spot.Thing is Building building && building.GetAssignedPawn() == pawn)
      score += building.def == ThingDefOf.MeditationSpot ? 200f : 100f;
      
    if (room != null && ModsConfig.IdeologyActive && room.Role == RoomRoleDefOf.WorshipRoom)
    {
      score += 100f;
      foreach (var containedAndAdjacentThing in room.ContainedAndAdjacentThings)
      {
        score += containedAndAdjacentThing.GetStatValue(StatDefOf.StyleDominance);
      }
    }

    return score;
  }
  
  // 元メソッドにあったWanderによる瞑想候補地は除外
  private static IEnumerable<LocalTargetInfo> AllMeditationSpotCandidates(Pawn pawn, bool allowFallbackSpots = true)
	{
		var flag = false;
		if (pawn.royalty is not null && pawn.royalty.AllTitlesInEffectForReading.Count > 0 && !pawn.IsPrisonerOfColony)
		{
			var building_Throne = RoyalTitleUtility.FindBestUsableThrone(pawn);
			if (building_Throne is not  null)
			{
				yield return building_Throne;
				flag = true;
			}
		}
		if (!pawn.IsPrisonerOfColony)
		{
      foreach (var item in
               from s in pawn.Map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.MeditationSpot)
               where MeditationUtility.IsValidMeditationBuildingForPawn(s, pawn)
               select s)
      {
        yield return item;
        flag = true;
      }
    }
		if (flag || !allowFallbackSpots)
			yield break;
    
		var list = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.MeditationFocus);
		foreach (var item2 in list)
		{
			if (item2.def == ThingDefOf.Wall)
				continue;
      
			var room = item2.GetRoom();
			if ((room == null || MeditationUtility.CanUseRoomToMeditate(room, pawn)) &&
          item2.GetStatValueForPawn(StatDefOf.MeditationFocusStrength, pawn) > 0f)
			{
				var localTargetInfo = MeditationUtility.MeditationSpotForFocus(item2, pawn);
				if (localTargetInfo.IsValid)
					yield return localTargetInfo;
			}
		}
		var bed = pawn.ownership.OwnedBed;
		if (bed?.GetRoom() is { PsychologicallyOutdoors: false } room2 &&
        pawn.CanReserveAndReach(bed, PathEndMode.OnCell, pawn.NormalMaxDanger()))
		{
			foreach (var item3 in MeditationUtility.FocusSpotsInTheRoom(pawn, room2))
			{
				yield return item3;
			}
		}
		foreach (var room3 in MeditationUtility.UsableWorshipRooms(pawn))
		{
			foreach (var item4 in MeditationUtility.FocusSpotsInTheRoom(pawn, room3))
			{
				if (pawn.CanReach(item4, PathEndMode.Touch, pawn.NormalMaxDanger()))
					yield return item4;
			}
		}
	}
}