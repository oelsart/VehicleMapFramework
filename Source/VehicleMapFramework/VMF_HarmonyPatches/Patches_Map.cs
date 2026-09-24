using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using SmashTools;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(MapDrawLayer), "FinalizeMesh")]
[PatchLevel(Level.Mandatory)]
public static class Patch_MapDrawLayer_FinalizeMesh
{
  public static void Prefix(MapDrawLayer __instance, MeshParts tags, Map ___map, List<LayerSubMesh> ___subMeshes)
  {
    if (!___map.IsVehicleMap || (tags & MeshParts.Verts) == 0)
      return;

    var terrain = __instance is SectionLayer_TerrainOnVehicle;
    // AsAboveSoBelowでポーンをTerrainの下に表示するため少し上げる
    var altitude = terrain ? AltitudeLayer.Conduits.AltitudeFor(-0.1f).YOffset() : 0f;
    foreach (var subMesh in ___subMeshes)
    {
      for (var j = 0; j < subMesh.verts.Count; j++)
      {
        var vert = subMesh.verts[j];
        if (terrain)
          vert.y = altitude;
        else
          vert.y /= VehicleMapUtility.YCompress;
        subMesh.verts[j] = vert;
      }
    }
  }
}

[HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
public static class Patch_MechanitorUtility_InMechanitorCommandRange
{
  [PatchLevel(Level.Safe)]
  public static void Prefix(Pawn mech, ref LocalTargetInfo target)
  {
    target = target.TargetCellOnBaseMap(mech);
  }

  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMapOrCaravan);
  }
}

[HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.CanCommandTo))]
[PatchLevel(Level.Cautious)]
public static class Patch_Pawn_MechanitorTracker_CanCommandTo
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap));
  }
}

[HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReach), typeof(IntVec3), typeof(LocalTargetInfo),
  typeof(PathEndMode), typeof(TraverseParms))]
public static class Patch_Reachability_CanReach
{
  [PatchLevel(Level.Safe)]
  public static bool Prefix(ref IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams,
    Map ___map, ref bool __result)
  {
    if (CrossMapReachabilityUtility.working) return true;

    var pawn = traverseParams.pawn;

    var destMap = CrossMapReachabilityUtility.DestMapGlobal ??
                  pawn.DestMap ??
                  dest.Thing?.MapHeld ??
                  (pawn.IsTargeting(dest, out var target)
                    ? target.Map
                    : pawn?.GetLord() is { LordJob: LordJob_Ritual } lord
                      ? lord.Map
                      : ___map);
    if (destMap == null)
    {
      return true;
    }

    var departMap = CrossMapReachabilityUtility.DepartMapGlobal ??
                    (pawn is not null && ((start == pawn.DepartPosition || start == pawn.Position))
                      ? pawn.DepartMap ?? ___map
                      : ___map);
    if (departMap == null)
    {
      return true;
    }

    if (departMap == destMap && departMap == ___map) return true;

    __result = CrossMapReachabilityUtility.CanReach(departMap, pawn.DepartPosition ?? start, dest, peMode, traverseParams, destMap);
    return false;
  }

  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
      .SetInstruction(CachedMethodInfo.m_BaseMapOrCaravan_Thing.CallInstruction)
      .MatchStartForward(new CodeMatch(OpCodes.Beq_S))
      .Insert(CachedMethodInfo.m_BaseMapOrCaravan_Map.CallInstruction)
      .InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReachNonLocal), typeof(IntVec3), typeof(TargetInfo),
  typeof(PathEndMode), typeof(TraverseParms))]
[PatchLevel(Level.Safe)]
public static class Patch_Reachability_CanReachNonLocal
{
  public static bool Prefix(IntVec3 start, TargetInfo dest, PathEndMode peMode, TraverseParms traverseParams,
    Map ___map, ref bool __result)
  {
    var destMap = dest.Map;
    if (___map.BaseMapOrCaravan == destMap.BaseMapOrCaravan)
    {
      __result = CrossMapReachabilityUtility.CanReach(___map, start, (LocalTargetInfo)dest, peMode, traverseParams,
        destMap);
      return false;
    }

    return true;
  }
}

[HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReachMapEdge), typeof(IntVec3), typeof(TraverseParms))]
[PatchLevel(Level.Cautious)]
public static class Patch_Reachability_CanReachMapEdge
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_Reachability_CanReach.Transpiler(instructions)
      .MethodReplacer(CachedMethodInfo.m_BreadthFirstTraverse, CachedMethodInfo.m_BreadthFirstTraverseAcrossMaps);
  }
}

//VehicleMapの外気温はマップ上のその位置の気温、スポーンしてないなら今いるタイルの外気温
[HarmonyPatch(typeof(MapTemperature), nameof(MapTemperature.OutdoorTemp), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_MapTemperature_OutdoorTemp
{
  public static bool Prefix(Map ___map, ref float __result)
  {
    if (___map.IsVehicleMapOf(out var vehicle))
    {
      if (vehicle.Spawned)
      {
        __result = vehicle.Position.GetTemperature(vehicle.Map);
      }
      else if (vehicle.Tile.Valid)
      {
        __result = Find.World.tileTemperatures.GetOutdoorTemp(vehicle.Tile);
      }

      return false;
    }

    return true;
  }
}

[HarmonyPatch(typeof(MapTemperature), nameof(MapTemperature.SeasonalTemp), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_MapTemperature_SeasonalTemp
{
  public static bool Prefix(Map ___map, ref float __result)
  {
    if (___map.IsVehicleMapOf(out var vehicle))
    {
      if (vehicle.Spawned)
      {
        __result = vehicle.Position.GetTemperature(vehicle.Map);
      }
      else if (vehicle.Tile != -1)
      {
        __result = Find.World.tileTemperatures.GetSeasonalTemp(vehicle.Tile);
      }

      return false;
    }

    return true;
  }
}

//リソースカウンターに車上マップのリソースを追加
[HarmonyPatch(typeof(ResourceCounter), nameof(ResourceCounter.UpdateResourceCounts))]
[PatchLevel(Level.Safe)]
public static class Patch_ResourceCounter_UpdateResourceCounts
{
  public static void Postfix(Map ___map, Dictionary<ThingDef, int> ___countedAmounts)
  {
    foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOn(___map))
    {
      var allGroupsListForReading = vehicle.VehicleMap.haulDestinationManager.AllGroupsListForReading;
      foreach (var t in allGroupsListForReading)
      {
        foreach (var outerThing in t.HeldThings)
        {
          var innerIfMinified = outerThing.GetInnerIfMinified();
          if (innerIfMinified.def.CountAsResource && !innerIfMinified.IsNotFresh())
          {
            var def = innerIfMinified.def;
            ___countedAmounts[def] += innerIfMinified.stackCount;
          }
        }
      }
    }
  }
}

[HarmonyPatch(typeof(Map), nameof(Map.MapUpdate))]
public static class Patch_Map_MapUpdate
{
  [PatchLevel(Level.Safe)]
  public static void Postfix(Map __instance)
  {
    var focused = Find.CurrentMap == __instance;
    if (focused && __instance.IsVehicleMapOf(out var vehicle) && VehicleMapFramework.settings.drawPlanet &&
        WorldRendererUtility.DrawingMap)
    {
      if (VehicleMapView.Available)
        VehicleMapView.Draw(vehicle);
    }
  }

  [PatchLevel(Level.Sensitive)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    var codes = instructions.ToList();
    var g_DrawingMap =
      AccessTools.PropertyGetter(typeof(WorldRendererUtility), nameof(WorldRendererUtility.DrawingMap));
    var pos = codes.FindIndex(c => c.Calls(g_DrawingMap)) + 1;
    var label = generator.DefineLabel();
    var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

    codes[pos].labels.Add(label);
    codes.InsertRange(pos,
    [
      new CodeInstruction(OpCodes.Dup),
      new CodeInstruction(OpCodes.Brfalse_S, label),
      CodeInstruction.LoadField(typeof(VehicleMapFramework), nameof(VehicleMapFramework.settings)),
      CodeInstruction.LoadField(typeof(VehicleMapSettings), nameof(VehicleMapSettings.drawPlanet)),
      new CodeInstruction(OpCodes.Brfalse_S, label),
      CodeInstruction.LoadArgument(0),
      new CodeInstruction(OpCodes.Ldloca, vehicle),
      CachedMethodInfo.m_IsVehicleMapOf.CallInstruction,
      new CodeInstruction(OpCodes.Brfalse_S, label),
      new CodeInstruction(OpCodes.Pop),
      new CodeInstruction(OpCodes.Ldc_I4_0),
    ]);
    return codes;
  }
}

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AllPawns), MethodType.Getter)]
public static class Patch_MapPawns_AllPawns
{
  private static readonly CrossMapMapPawnsCache cache = new((instance, _) => AllPawns(instance));

  [PatchLevel(Level.Safe)]
  public static void Postfix(ref List<Pawn> __result, Map ___map)
  {
    if (VehiclePawnWithMapCache.AllVehiclesOn(___map).Count == 0)
      return;

    __result = cache.Get(___map, __result);
  }

  [PatchLevel(Level.Mandatory)]
  [HarmonyReversePatch]
  [MethodImpl(MethodImplOptions.NoInlining)]
  public static List<Pawn> AllPawns(MapPawns instance) => throw new NotImplementedException();
}

[HarmonyBefore(VehicleFramework.HarmonyId)]
[HarmonyPatchCategory(EarlyPatchCore.Category)]
[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AllPawnsSpawned), MethodType.Getter)]
[PatchLevel(Level.Mandatory)]
public static class Patch_MapPawns_AllPawnsSpawned
{
  private static readonly CrossMapMapPawnsCache cache = new((instance, _) => AllPawnsSpawned(instance));

  public static void Postfix(ref IReadOnlyList<Pawn> __result, Map ___map)
  {
    if (VehiclePawnWithMapCache.AllVehiclesOn(___map).Count == 0)
      return;

    __result = cache.Get(___map, __result);
  }

  [HarmonyReversePatch]
  [MethodImpl(MethodImplOptions.NoInlining)]
  public static List<Pawn> AllPawnsSpawned(MapPawns instance) => throw new NotImplementedException();
}

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.FreeHumanlikesSpawnedOfFaction))]
public static class Patch_MapPawns_FreeHumanlikesSpawnedOfFaction
{
  private static readonly CrossMapMapPawnsCache cache = new(FreeHumanlikesSpawnedOfFaction);

  [PatchLevel(Level.Safe)]
  public static void Postfix(ref List<Pawn> __result, Map ___map, Faction faction)
  {
    if (VehiclePawnWithMapCache.AllVehiclesOn(___map).Count == 0)
      return;

    __result = cache.Get(___map, __result, faction);
  }

  [PatchLevel(Level.Mandatory)]
  [HarmonyReversePatch]
  [MethodImpl(MethodImplOptions.NoInlining)]
  public static List<Pawn> FreeHumanlikesSpawnedOfFaction(MapPawns instance, Faction faction) =>
    throw new NotImplementedException();
}

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.PrisonersOfColonySpawned), MethodType.Getter)]
public static class Patch_MapPawns_PrisonersOfColonySpawned
{
  private static readonly CrossMapMapPawnsCache _cache = new((instance, _) => PrisonersOfColonySpawned(instance));
  
  [PatchLevel(Level.Safe)]
  public static void Postfix(ref List<Pawn> __result, Map ___map)
  {
    if (VehiclePawnWithMapCache.AllVehiclesOn(___map).Count == 0)
      return;

    __result = _cache.Get(___map, __result);
  }
  
  [PatchLevel(Level.Mandatory)]
  [HarmonyReversePatch]
  [MethodImpl(MethodImplOptions.NoInlining)]
  public static List<Pawn> PrisonersOfColonySpawned(MapPawns instance) => throw new NotImplementedException();
}

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AnyPawnBlockingMapRemoval), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_MapPawns_AnyPawnBlockingMapRemoval
{
  public static void Postfix(ref bool __result, Map ___map)
  {
    if (__result) return;
    foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOnAsReadOnlySpan(___map))
    {
      if (vehicle.VehicleMap.mapPawns.AnyPawnBlockingMapRemoval)
      {
        __result = true;
        return;
      }
    }
  }
}

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.RegisterPawn))]
[PatchLevel(Level.Safe)]
public static class Patch_MapPawns_RegisterPawn
{
  public static void Postfix() => CrossMapMapPawnsCache.ClearAll();
}

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.DeRegisterPawn))]
[PatchLevel(Level.Safe)]
public static class Patch_MapPawns_DeRegisterPawn
{
  public static void Postfix() => CrossMapMapPawnsCache.ClearAll();
}

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.UpdateRegistryForPawn))]
[PatchLevel(Level.Safe)]
public static class Patch_MapPawns_UpdateRegistryForPawn
{
  public static void Postfix() => CrossMapMapPawnsCache.ClearAll();
}

[HarmonyPatch(typeof(PawnsFinder), nameof(PawnsFinder.AllMaps), MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_PawnsFinder_AllMaps
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_AllPawns, CachedMethodInfo.m_AllPawns_Reverse);
  }
}

[HarmonyPatch(typeof(PawnsFinder), nameof(PawnsFinder.AllMaps_Spawned), MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_PawnsFinder_AllMaps_Spawned
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_AllPawnsSpawned, CachedMethodInfo.m_AllPawnsSpawned_Reverse);
  }
}

[HarmonyPatch(typeof(PawnsFinder), nameof(PawnsFinder.AllMaps_PrisonersOfColonySpawned), MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_PawnsFinder_AllMaps_PrisonersOfColonySpawned
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var g_PrisonersOfColonySpawned = AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.PrisonersOfColonySpawned));
    var m_PrisonersOfColonySpawned_Reverse = ((Delegate)Patch_MapPawns_PrisonersOfColonySpawned.PrisonersOfColonySpawned).Method;
    return instructions.MethodReplacer(g_PrisonersOfColonySpawned, m_PrisonersOfColonySpawned_Reverse);
  }
}

[HarmonyPatch]
[PatchLevel(Level.Safe)]
public static class Patch_DesignationManager_DesignationOn
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    return AccessTools.GetDeclaredMethods(typeof(DesignationManager))
      .Where(m => m.Name == nameof(DesignationManager.DesignationOn));
  }

  public static void Prefix(ref DesignationManager __instance, Thing t)
  {
    var thingMap = t.MapHeld;
    if (thingMap == null || thingMap == __instance.map) return;
    __instance = thingMap.designationManager;
  }
}

[HarmonyPatch(typeof(SoundStarter), nameof(SoundStarter.PlayOneShot))]
[PatchLevel(Level.Safe)]
public static class Patch_SoundStarter_PlayOneShot
{
  public static void Prefix(ref SoundInfo info)
  {
    if (info.Maker.IsValid && info.Maker.Map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
    {
      info = SoundInfo.InMap(new TargetInfo(info.Maker.Cell.ToBaseMapCoord(vehicle), vehicle.Map), info.Maintenance);
    }
  }
}

[HarmonyPatch(typeof(Room), nameof(Room.DrawFieldEdges))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Room_DrawFieldEdges
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_GenDraw_DrawFieldEdges2))
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(0),
        AccessTools.PropertyGetter(typeof(Room), nameof(Room.Map)).CallvirtInstruction)
      .SetOperandAndAdvance(CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges2)
      .InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(HaulDestinationManager), nameof(HaulDestinationManager.AddHaulDestination))]
[PatchLevel(Level.Mandatory)]
public static class Patch_HaulDestinationManager_AddHaulDestination
{
  public static void Postfix(Map ___map, IHaulDestination haulDestination)
  {
    ___map.GetCachedMapComponent<CrossMapHaulDestinationManager>().AddHaulDestination(haulDestination);
  }
}

[HarmonyPatch(typeof(HaulDestinationManager), nameof(HaulDestinationManager.RemoveHaulDestination))]
[PatchLevel(Level.Mandatory)]
public static class Patch_HaulDestinationManager_RemoveHaulDestination
{
  public static void Postfix(Map ___map, IHaulDestination haulDestination)
  {
    ___map.GetCachedMapComponent<CrossMapHaulDestinationManager>().RemoveHaulDestination(haulDestination);
  }
}

[HarmonyPatch(typeof(HaulDestinationManager), nameof(HaulDestinationManager.Notify_HaulDestinationChangedPriority))]
[PatchLevel(Level.Mandatory)]
public static class Patch_HaulDestinationManager_Notify_HaulDestinationChangedPriority
{
  public static void Postfix(Map ___map)
  {
    ___map.GetCachedMapComponent<CrossMapHaulDestinationManager>().Notify_HaulDestinationChangedPriority();
  }
}

//極端に小さいマップではCeilToIntのせいで毎tick必ずどこかのセルの物が劣化する処理だったんでこれを車両マップ上では緩和
[HarmonyPatch(typeof(SteadyEnvironmentEffects), nameof(SteadyEnvironmentEffects.SteadyEnvironmentEffectsTick))]
[PatchLevel(Level.Sensitive)]
public static class Patch_SteadyEnvironmentEffects_SteadyEnvironmentEffectsTick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var m_CeilToInt = ((Delegate)Mathf.CeilToInt).Method;
    var pos = codes.FindIndex(c => c.Calls(m_CeilToInt));

    codes[pos].operand = ((Delegate)ChanceToInt).Method;
    codes.InsertRange(pos,
    [
      CodeInstruction.LoadArgument(0),
      CodeInstruction.LoadField(typeof(SteadyEnvironmentEffects), "map")
    ]);
    return codes;
  }

  public static int ChanceToInt(float chance, Map map)
  {
    if (map.IsVehicleMapOf(out _))
    {
      var floor = Mathf.FloorToInt(chance);
      chance -= floor;
      if (Rand.Chance(chance)) floor++;
      return floor;
    }

    return Mathf.CeilToInt(chance);
  }
}

[HarmonyPatch(typeof(Map), nameof(Map.TileInfo), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_Map_TileInfo
{
  public static void Postfix(Map __instance, ref Tile __result)
  {
    if (__instance.IsVehicleMap && Find.Maps.Contains(__instance) &&
        __instance.Tile.Valid && Find.WorldGrid.InBounds(__instance.Tile))
    {
      __result = Find.WorldGrid[__instance.Tile];
    }
  }
}

[HarmonyPatch(typeof(QuestPart_SpawnThing), nameof(QuestPart_SpawnThing.MapParent), MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_QuestPart_SpawnThing_MapParent
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
  }
}

[HarmonyPatch(typeof(AreaSource), nameof(AreaSource.DataForArea))]
[PatchLevel(Level.Safe)]
public static class Patch_AreaSource_DataForArea
{
  public static void Prefix(ref AreaSource __instance, Area area, Map ___map)
  {
    Map baseMap;
    if (area.Map != ___map && area.Map == (baseMap = ___map.BaseMap()))
    {
      __instance = areas(baseMap.pathFinder.MapData);
    }
  }

  private static readonly AccessTools.FieldRef<PathFinderMapData, AreaSource> areas =
    AccessTools.FieldRefAccess<PathFinderMapData, AreaSource>("areas");
}

[HarmonyPatch(typeof(QuestGen_TransportShip), nameof(QuestGen_TransportShip.AddShipJob_Arrive))]
[PatchLevel(Level.Cautious)]
public static class Patch_QuestGen_TransportShip_AddShipJob_Arrive
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = new CodeMatcher(instructions);
    codes.MatchStartForward(new CodeMatch(OpCodes.Isinst, typeof(PocketMapParent)));
    codes.MatchStartForward(new CodeMatch(OpCodes.Brfalse_S));
    var label = codes.Operand;
    codes.InsertAfter(
      CodeInstruction.LoadLocal(0),
      CodeInstruction.LoadField(typeof(PocketMapParent), nameof(PocketMapParent.sourceMap)),
      new CodeInstruction(OpCodes.Brfalse_S, label));
    return codes.Instructions();
  }
}

[HarmonyPatch(typeof(GenHostility), nameof(GenHostility.AnyHostileActiveThreatTo))]
[HarmonyPatch([typeof(Map), typeof(Faction), typeof(IAttackTarget), typeof(bool), typeof(bool)],
  [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
[PatchLevel(Level.Safe)]
public static class Patch_GenHostility_AnyHostileActiveThreatTo
{
  public static void Postfix(Map map, Faction faction, ref IAttackTarget threat, bool countDormantPawnsAsHostile,
    bool canBeFogged, ref bool __result)
  {
    if (__result) return;

    foreach (var map2 in map.BaseMapAndVehicleMaps(false))
    {
      foreach (var attackTarget in map2.attackTargetsCache.TargetsHostileToFaction(faction))
      {
        if (GenHostility.IsActiveThreatTo(attackTarget, faction, true, canBeFogged))
        {
          threat = attackTarget;
          __result = true;
          return;
        }

        if (countDormantPawnsAsHostile && attackTarget.Thing.HostileTo(faction) &&
            (canBeFogged || !attackTarget.Thing.Fogged()) && !attackTarget.ThreatDisabled(null))
        {
          if (attackTarget.Thing is Pawn pawn)
          {
            var comp = pawn.GetComp<CompCanBeDormant>();
            if (comp is { Awake: false })
            {
              threat = attackTarget;
              __result = true;
              return;
            }
          }
        }
      }
    }
  }
}

// 車両マップではマップサイズによってウェザーイベント（雷）のチャンスを減らす
[HarmonyPatch(typeof(WeatherEventMaker), nameof(WeatherEventMaker.WeatherEventMakerTick))]
[PatchLevel(Level.Safe)]
public static class Patch_WeatherEventMaker_WeatherEventMakerTick
{
  public static void Prefix(Map map, ref float strength)
  {
    if (map.IsVehicleMapOf(out _))
    {
      strength *= map.Area / 40000f;
    }
  }
}

// 別マップの素材がある場合MissingIngredientsから除外する。主に車両マップでの手術などでレシピが表示されるようにする
[HarmonyPatch(typeof(RecipeDef), nameof(RecipeDef.PotentiallyMissingIngredients))]
[PatchLevel(Level.Safe)]
public static class Patch_RecipeDef_PotentiallyMissingIngredients
{
  public static IEnumerable<ThingDef> Postfix(IEnumerable<ThingDef> values, Pawn billDoer, Map map,
    RecipeDef __instance)
  {
    return from thingDef in values
      let found = __instance.ingredients
        .Where(ing => ing.IsFixedIngredient && thingDef == ing.FixedIngredient || ing.filter.Allows(thingDef))
        .Any(ing => (map.BaseMapAndVehicleMaps(false))
          .Any(map2 =>
          {
            var list = map2.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
            return list.Exists(t =>
              t.def == thingDef &&
              (billDoer == null || !t.IsForbidden(billDoer)) &&
              !t.Position.Fogged(map2) &&
              (ing.IsFixedIngredient || __instance.fixedIngredientFilter.Allows(t)) &&
              ing.filter.Allows(t));
          }))
      where !found
      select thingDef;
  }
}

// CreateNoPawnsWithSkillDialogの抑制
[HarmonyPatch(typeof(HealthCardUtility), nameof(HealthCardUtility.CreateSurgeryBill))]
[PatchLevel(Level.Safe)]
public static class Patch_HealthCardUtility_CreateSurgeryBill
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
  }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
[HarmonyAfter(GestaltEngine.HarmonyId)]
public static class Patch_ITab_Bills_FillTab_Delegate
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes(typeof(ITab_Bills), t =>
    {
      return t.GetDeclaredMethods().FirstOrDefault(m =>
      {
        if (!m.Name.Contains("<FillTab>")) return false;
        return PatchHelper.ReadMethodBodyWrapper(m).Any(i =>
          CachedMethodInfo.g_Thing_Map.Equals(i.Value));
      });
    });
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatch(typeof(CaravanFormingUtility), nameof(CaravanFormingUtility.AllSendablePawns))]
[PatchLevel(Level.Cautious)]
public static class Patch_CaravanFormingUtility_AllSendablePawns
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_AllPawnsSpawned, CachedMethodInfo.m_AllPawnsSpawned_Reverse);
  }
}

[HarmonyPatch(typeof(MapDeiniter), "PassPawnsToWorld")]
[PatchLevel((Level.Cautious))]
public static class Patch_MapDeiniter_PassPawnsToWorld
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_AllPawns, CachedMethodInfo.m_AllPawns_Reverse);
  }
}

// 地上マップの制限ゾーン変更により車両マップの制限ゾーンが地上マップのゾーンで上書きされる問題の修正
[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_Pawn_PlayerSettings_AreaRestrictionInPawnCurrentMap
{
  private static readonly AccessTools.FieldRef<Pawn_PlayerSettings, Pawn> pawn =
    AccessTools.FieldRefAccess<Pawn_PlayerSettings, Pawn>("pawn");
  
  private static IEnumerable<MethodBase> TargetMethods()
  {
    yield return AccessTools.Method(typeof(AreaAllowedGUI), "DoAreaSelector");
    yield return AccessTools.Method(typeof(InspectPaneFiller), "DrawAreaAllowed");
    yield return AccessTools.FindIncludingInnerTypes(typeof(InspectPaneFiller), t =>
      t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<DrawAreaAllowed>")));
    yield return AccessTools.Method(typeof(PawnColumnWorker_AllowedArea), "HeaderClicked");
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    if (UnitTestDetector.IsTestingContext) return instructions;
    var g_AreaRestrictionInPawnCurrentMap =
      AccessTools.PropertyGetter(typeof(Pawn_PlayerSettings), nameof(Pawn_PlayerSettings.AreaRestrictionInPawnCurrentMap));
    var g_AreaRestrictionInPawnBaseMap = ((Delegate)get_AreaRestrictionInPawnBaseMap).Method;
    var s_AreaRestrictionInPawnCurrentMap =
      AccessTools.PropertySetter(typeof(Pawn_PlayerSettings), nameof(Pawn_PlayerSettings.AreaRestrictionInPawnCurrentMap));
    var s_AreaRestrictionInPawnBaseMap = ((Delegate)set_AreaRestrictionInPawnBaseMap).Method;
    var m_AreaAllowedLabel = ((Delegate)AreaUtility.AreaAllowedLabel).Method;
    var m_AreaAllowedLabelBaseMap = ((Delegate)AreaAllowedLabelBaseMap).Method;
    var m_MakeAllowedAreaListFloatMenu = ((Delegate)AreaUtility.MakeAllowedAreaListFloatMenu).Method;
    var m_MakeAllowedAreaListFloatMenuBaseMap = ((Delegate)MakeAllowedAreaListFloatMenuBaseMap).Method;
    return instructions.MethodReplacer(
      (g_AreaRestrictionInPawnCurrentMap, g_AreaRestrictionInPawnBaseMap),
      (s_AreaRestrictionInPawnCurrentMap, s_AreaRestrictionInPawnBaseMap),
      (m_AreaAllowedLabel, m_AreaAllowedLabelBaseMap),
      (m_MakeAllowedAreaListFloatMenu, m_MakeAllowedAreaListFloatMenuBaseMap));
  }

  extension(Pawn_PlayerSettings playerSettings)
  {
    private Area AreaRestrictionInPawnBaseMap
    {
      get
      {
        var _pawn = pawn(playerSettings);
        var mapHeldBaseMap = _pawn.MapHeldBaseMap();
        if (Find.CurrentMap == mapHeldBaseMap && _pawn.MapHeld != mapHeldBaseMap)
        {
          using var _ = new VirtualTeleporter(_pawn, mapHeldBaseMap, _pawn.PositionOnBaseMap, true);
          return playerSettings.AreaRestrictionInPawnCurrentMap;
        }
        return playerSettings.AreaRestrictionInPawnCurrentMap;
      }
      set
      {
        var _pawn = pawn(playerSettings);
        var mapHeld = _pawn.MapHeld;
        var mapHeldBaseMap = mapHeld.BaseMap();
        if (Find.CurrentMap == mapHeldBaseMap && mapHeld != mapHeldBaseMap)
        {
          using var _ = new VirtualTeleporter(_pawn, mapHeldBaseMap, _pawn.PositionOnBaseMap, true);
          playerSettings.AreaRestrictionInPawnCurrentMap = value;
          CrossMapReachabilityCache.ClearCacheFor(mapHeldBaseMap);
          return;
        }
        playerSettings.AreaRestrictionInPawnCurrentMap = value;
        CrossMapReachabilityCache.ClearCacheFor(mapHeld);
      }
    }
  }

  private static string AreaAllowedLabelBaseMap(Pawn _pawn)
  {
    return AreaUtility.AreaAllowedLabel_Area(_pawn.playerSettings?.AreaRestrictionInPawnBaseMap);
  }

  private static void MakeAllowedAreaListFloatMenuBaseMap(Action<Area> selAction,
    bool addNullAreaOption, bool addManageOption, Map map)
  {
    var baseMap = map.GroundMap;
    if (Find.CurrentMap == baseMap && map != baseMap)
    {
      AreaUtility.MakeAllowedAreaListFloatMenu(selAction, addNullAreaOption, addManageOption, baseMap);
      return;
    }
    AreaUtility.MakeAllowedAreaListFloatMenu(selAction, addNullAreaOption, addManageOption, map);
  }
}

[HarmonyPatch(typeof(Map), nameof(Map.IsPlayerHome), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_Map_IsPlayerHome
{
  private static bool Prepare() => VehicleMapFramework.settings is { treatAsPlayerHome: true };

  public static void Postfix(Map __instance, ref bool __result)
  {
    __result = __result || __instance.IsVehicleMapOf(out var vehicle) && vehicle.Faction == Faction.OfPlayer;
  }
}

[HarmonyPatch(typeof(QuestNode_GetMap), "IsAcceptableMap")]
[PatchLevel(Level.Cautious)]
public static class Patch_QuestNode_GetMap_IsAcceptableMap
{
  private static bool Prepare() => VehicleMapFramework.settings is { treatAsPlayerHome: true };

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      AccessTools.PropertyGetter(typeof(Map), nameof(Map.IsPocketMap)), ((Delegate)IsPocketMap).Method);
  }

  private static bool IsPocketMap(Map map)
  {
    if (map is not { IsPocketMap: true })
      return false;
    
    if (!map.IsVehicleMapOf(out var vehicle))
      return true;
    
    // 車両がプレイヤーホームにスポーンしている時車両マップでクエストは起きない
    return vehicle.Spawned && vehicle.Map.IsPlayerHome;
  }
}

// 車両がワールドマップにいる時エラーの可能性があった。
[HarmonyPatch(typeof(WildAnimalSpawner), nameof(WildAnimalSpawner.WildAnimalSpawnerTick))]
[PatchLevel(Level.Safe)]
public static class Patch_WildAnimalSpawner_WildAnimalSpawnerTick
{
  public static bool Prefix(Map ___map) => !___map.IsVehicleMap;
}

[HarmonyPatch(typeof(QuestNode_GetWalkInSpot), "TryFindWalkInSpot")]
[PatchLevel(Level.Safe)]
public static class Patch_QuestNode_GetWalkInSpot_TryFindWalkInSpot
{
  public static void Postfix(ref Map map, ref IntVec3 spawnSpot, ref bool __result)
  {
    if (!__result || !map.IsVehicleMapOf(out var vehicle))
      return;

    if (VehicleMapCellFinder.TryFindRandomEdgeCellWith(c => vehicle.VehicleMap.reachability.CanReachColony(c),
          vehicle, out spawnSpot))
    {
      __result = true;
      return;
    }

    __result = VehicleMapCellFinder.TryFindRandomEdgeCellWith(null, vehicle, out spawnSpot);
  }
}