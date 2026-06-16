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
using Vehicles.World;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(MapDrawLayer), "FinalizeMesh")]
[PatchLevel(Level.Mandatory)]
public static class Patch_MapDrawLayer_FinalizeMesh
{
  public static void Prefix(MeshParts tags, Map ___map, List<LayerSubMesh> ___subMeshes)
  {
    if (!___map.IsVehicleMapOf(out _) || !tags.HasFlag(MeshParts.Verts))
      return;

    foreach (var subMesh in ___subMeshes)
    {
      for (var j = 0; j < subMesh.verts.Count; j++)
      {
        var vert = subMesh.verts[j];
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
  public static bool Prefix(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams,
    Map ___map, ref bool __result)
  {
    if (CrossMapReachabilityUtility.working) return true;

    var pawn = traverseParams.pawn;

    var destMap = pawn.DestMap ??
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
                    pawn.DepartMap ?? ___map;
    if (departMap == null)
    {
      return true;
    }

    if (departMap == destMap && departMap == ___map) return true;

    __result = CrossMapReachabilityUtility.CanReach(departMap, start, dest, peMode, traverseParams, destMap);
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

[StaticConstructorOnStartup]
[HarmonyPatch(typeof(Map), nameof(Map.MapUpdate))]
public static class Patch_Map_MapUpdate
{
  private static RenderTexture tmpRenderTex;
  public const float Altitude = 140f;
  public const int TextureSize = 2048;
  public const float MeshSizeX = 200f;
  public static readonly Vector2 MeshSize = new(MeshSizeX, MeshSizeX);
  private static Mesh mesh200;
  private static Material mat;
  private static Material skyMat;

  public static int lastRenderedTick = -1;

  private static readonly AccessTools.FieldRef<WorldCameraDriver, float> desiredAltitude =
    AccessTools.FieldRefAccess<WorldCameraDriver, float>("desiredAltitude");

  static Patch_Map_MapUpdate()
  {
    if (UnitTestDetector.IsTestingContext) return;
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      mesh200 = MeshPool.GridPlane(MeshSize);
      skyMat = SolidColorMaterials.NewSolidColorMaterial(Color.black, ShaderDatabase.SolidColor);
    });
  }

  public static void JumpTo(Vector3 pos, float altitude)
  {
    Find.WorldCameraDriver.JumpTo(pos);
    Find.WorldCameraDriver.altitude = altitude;
    desiredAltitude(Find.WorldCameraDriver) = altitude;
    Find.WorldCameraDriver.Update();
  }

  [PatchLevel(Level.Safe)]
  public static void Postfix(Map __instance)
  {
    var focused = Find.CurrentMap == __instance;
    if (focused && __instance.IsVehicleMapOf(out var vehicle) && VehicleMapFramework.settings.drawPlanet &&
        WorldRendererUtility.DrawingMap && !Find.World.renderer.RegenerateLayersIfDirtyInLongEvent())
    {
      var angle = vehicle.Transform.rotation + vehicle.Rotation.AsAngle;
      var vehicleCaravanOrStashedVehicle = vehicle.VehicleCaravanOrStashedVehicle;
      if ((GenTicks.TicksGame != lastRenderedTick || Find.TickManager.Paused) && Time.frameCount % 2 == 0 ||
          mat != null && tmpRenderTex == null)
      {
        var worldObject = vehicleCaravanOrStashedVehicle ?? GetWorldObject(vehicle);
        if (worldObject is null) return;
        lastRenderedTick = GenTicks.TicksGame;
        Find.World.renderer.wantedMode = WorldRenderMode.Planet;
        JumpTo(worldObject.DrawPos, Altitude);
        WorldRendererUtility.UpdateGlobalShadersParams();
        ExpandableWorldObjectsUtility.ExpandableWorldObjectsUpdate();
        foreach (var layer in Find.World.renderer.AllVisibleDrawLayers.Where(l =>
                   l is not WorldDrawLayer_SingleTile && l is not WorldDrawLayer_Satellites))
        {
          layer.Render();
        }

        Find.World.dynamicDrawManager.DrawDynamicWorldObjects();
        if (worldObject is VehicleCaravan vehicleCaravan)
        {
          vehicleCaravan.gotoMote.RenderMote();
          vehicleCaravan.vehiclePather?.curPath?.DrawPath(vehicleCaravan);
        }

        if (tmpRenderTex is not null)
        {
          RenderTexture.ReleaseTemporary(tmpRenderTex);
        }

        tmpRenderTex = RenderTexture.GetTemporary(TextureSize, TextureSize);
        var targetTexture = Find.WorldCamera.targetTexture;
        Find.WorldCamera.targetTexture = tmpRenderTex;
        Find.WorldCamera.orthographic = true;
        Find.WorldCamera.Render();
        Find.WorldCamera.targetTexture = targetTexture;
        Find.WorldCamera.orthographic = false;
        Find.World.renderer.wantedMode = WorldRenderMode.None;
        Find.CameraDriver.Update();
        if (mat is null)
        {
          mat = MaterialPool.MatFrom(new MaterialRequest(tmpRenderTex));
        }
        else
        {
          mat.mainTexture = tmpRenderTex;
        }

        var planetLayer = __instance.Tile.Layer;

        float AngleOnPlanetSurface(Vector3 root, Vector3 to)
        {
          if (planetLayer == null || (to - root).magnitude <= Mathf.Epsilon)
          {
            return 0f;
          }

          var normal = root - planetLayer.Origin;
          var planeFrom = Vector3.ProjectOnPlane(planetLayer.NorthPolePos, normal);
          var planeTo = Vector3.ProjectOnPlane(to, normal);
          var signedAngle = Vector3.SignedAngle(planeFrom, planeTo, normal);
          return Mathf.Repeat(signedAngle + 180f, 360f);
        }

        if (!vehicle.Spawned)
        {
          angle =
            worldObject switch
            {
              VehicleCaravan vehicleCaravan2 => AngleOnPlanetSurface(
                Find.WorldGrid.GetTileCenter(vehicleCaravan2.vehiclePather.NextTile.Valid
                  ? vehicleCaravan2.vehiclePather.NextTile
                  : vehicleCaravan2.Tile), Find.WorldGrid.GetTileCenter(vehicleCaravan2.Tile)),
              Caravan caravan => AngleOnPlanetSurface(
                Find.WorldGrid.GetTileCenter(caravan.pather.nextTile.Valid ? caravan.pather.nextTile : caravan.Tile),
                Find.WorldGrid.GetTileCenter(caravan.Tile)),
              AerialVehicleInFlight aerial => AngleOnPlanetSurface(aerial.DrawPos, aerial.position),
              _ => 0f
            };
          var rot = Rot4.FromAngleFlat(angle);
          if (vehicleCaravanOrStashedVehicle != null)
          {
            foreach (var vehicle2 in vehicleCaravanOrStashedVehicle.Vehicles)
            {
              vehicle2.FullRotation = rot;
            }
          }
          else vehicle.FullRotation = rot;
        }
      }

      var center = new Vector3(MeshSize.x / 2f, 0f, MeshSize.y / 2f);
      // 背景
      Graphics.DrawMesh(mesh200, center, Quaternion.identity,
        mat != null ? mat : SolidColorMaterials.SimpleSolidColorMaterial(Color.black), 0);

      // 空の暗さ
      skyMat.color = Color.black.WithAlpha((1f - vehicle.VehicleMap.skyManager.CurSkyGlow) * 0.2f);
      skyMat.renderQueue = 3100;
      Graphics.DrawMesh(mesh200, center.WithY(AltitudeLayer.LightingOverlay.AltitudeFor()), Quaternion.identity, skyMat,
        0);

      //　車両本体
      if (vehicleCaravanOrStashedVehicle != null)
      {
        var drawPositions = vehicleCaravanOrStashedVehicle.DrawPositions;
        if (!drawPositions.Keys.SequenceEqual(vehicleCaravanOrStashedVehicle.Vehicles))
          vehicleCaravanOrStashedVehicle.RecalculateVehiclePositions();

        foreach (var vehicle2 in vehicleCaravanOrStashedVehicle.Vehicles)
        {
          var drawPos2 = center + drawPositions[vehicle2].RotatedBy(angle);
          vehicle2.DrawAt(in drawPos2, vehicle2.FullRotation, angle - vehicle2.FullRotation.AsAngle);
        }
      }
      else
      {
        var drawPos = center.WithY(AltitudeLayer.LayingPawn.AltitudeFor());
        vehicle.DrawAt(in drawPos, vehicle.FullRotation, angle - vehicle.FullRotation.AsAngle);
      }
    }
    else if (tmpRenderTex != null && focused)
    {
      RenderTexture.ReleaseTemporary(tmpRenderTex);
      tmpRenderTex = null;
    }

    return;

    static WorldObject GetWorldObject(IThingHolder holder)
    {
      while (holder != null)
      {
        if (holder is WorldObject worldObject)
        {
          return worldObject;
        }

        holder = holder.ParentHolder;
      }

      return null;
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

[HarmonyPatch(typeof(CameraJumper), nameof(CameraJumper.GetWorldTarget))]
[PatchLevel(Level.Safe)]
public static class Patch_CameraJumper_GetWorldTarget
{
  public static void Prefix(ref GlobalTargetInfo target)
  {
    if (target.Thing.IsOnVehicleMapOf(out var vehicle))
    {
      target = vehicle;
    }
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

[HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.MapParentAt))]
[PatchLevel(Level.Sensitive)]
public static class Patch_WorldObjectsHolder_MapParentAt
{
  public static void Postfix(ref MapParent __result, List<MapParent> ___mapParents, PlanetTile tile)
  {
    if (__result is MapParent_Vehicle)
    {
      __result = ___mapParents.FirstOrDefault(p => p.Tile == tile && p is not MapParent_Vehicle);
    }
  }
}

[HarmonyPatch(typeof(Game), nameof(Game.FindMap), typeof(PlanetTile))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Game_FindMap
{
  public static void Postfix(ref Map __result, List<Map> ___maps, PlanetTile tile)
  {
    if (__result.IsVehicleMap)
    {
      __result = ___maps.FirstOrDefault(m => m.Tile == tile && !m.IsVehicleMap);
    }
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
    if (__instance.IsVehicleMapOf(out _) && Find.Maps.Contains(__instance) &&
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

[HarmonyPatch(typeof(StorytellerUtility), nameof(StorytellerUtility.DefaultThreatPointsNow))]
[PatchLevel(Level.Cautious)]
public static class Patch_StorytellerUtility_DefaultThreatPointsNow
{
  public static bool Prefix(IIncidentTarget target, ref float __result)
  {
    if (target is Map { IsVehicleMap: true } || target.PlayerPawnsForStoryteller.Any(p => p is VehiclePawnWithMap))
    {
      __result = VehicleMapUtility.DefaultThreatPointsNowForMapVehicles(target);
      return false;
    }

    return true;
  }
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