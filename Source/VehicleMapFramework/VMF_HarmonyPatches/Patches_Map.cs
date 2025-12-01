using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using SmashTools;
using UnityEngine;
using Vehicles;
using Vehicles.World;
using Verse;
using Verse.AI;
using Verse.Sound;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(MapDrawLayer), "FinalizeMesh")]
[PatchLevel(Level.Safe)]
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
        target = TargetMapManager.TargetCellOnBaseMap(ref target, mech);
    }

    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
    }
}

[HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.CanCommandTo))]
[PatchLevel(Level.Cautious)]
public static class Patch_Pawn_MechanitorTracker_CanCommandTo
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReach), typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms))]
public static class Patch_Reachability_CanReach
{
    [PatchLevel(Level.Safe)]
    public static bool Prefix(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams, Map ___map, ref bool __result)
    {
        if (CrossMapReachabilityUtility.working) return true;
        
        var pawn = traverseParams.pawn;

        var destMap = CrossMapReachabilityUtility.DestMapGlobal ??
                      pawn.DestMap ??
                      dest.Thing?.MapHeld ??
                      (TargetMapManager.HasTargetInfo(pawn, out var target) && 
                       (LocalTargetInfo)target == dest ? target.Map : ___map);
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
            .SetInstruction(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Thing))
            .MatchStartForward(new CodeMatch(OpCodes.Beq_S))
            .Insert(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map))
            .InstructionEnumeration();
    }
}

[HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReachNonLocal), typeof(IntVec3), typeof(TargetInfo), typeof(PathEndMode), typeof(TraverseParms))]
[PatchLevel(Level.Safe)]
public static class Patch_Reachability_CanReachNonLocal
{
    public static bool Prefix(IntVec3 start, TargetInfo dest, PathEndMode peMode, TraverseParms traverseParams, Map ___map, ref bool __result)
    {
        var destMap = dest.Map;
        if (___map.BaseMapOrCaravan == destMap.BaseMapOrCaravan)
        {
            __result = CrossMapReachabilityUtility.CanReach(___map, start, (LocalTargetInfo)dest, peMode, traverseParams, destMap);
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
        return Patch_Reachability_CanReach.Transpiler(instructions);
    }
}

[HarmonyPatch(typeof(Pawn_PlayerSettings), nameof(Pawn_PlayerSettings.EffectiveAreaRestrictionInPawnCurrentMap), MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_Pawn_PlayerSettings_EffectiveAreaRestrictionInPawnCurrentMap
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
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

    private const int textureSize = 2048;

    public static readonly Vector2 MeshSize = new(200f, 200f);

    private static readonly Mesh mesh200 = MeshPool.GridPlane(MeshSize);

    private static Material mat;

    private static readonly Material skyMat = SolidColorMaterials.NewSolidColorMaterial(Color.black, ShaderDatabase.SolidColor);

    public static int lastRenderedTick = -1;

    private static readonly AccessTools.FieldRef<WorldCameraDriver, float> desiredAltitude = AccessTools.FieldRefAccess<WorldCameraDriver, float>("desiredAltitude");
    [PatchLevel(Level.Safe)]
    public static void Postfix(Map __instance)
    {
        var focused = Find.CurrentMap == __instance;
        if (focused && __instance.IsVehicleMapOf(out var vehicle) && VehicleMapFramework.settings.drawPlanet && WorldRendererUtility.DrawingMap && !Find.World.renderer.RegenerateLayersIfDirtyInLongEvent())
        {
            var angle = vehicle.Transform.rotation + vehicle.Rotation.AsAngle;
            var vehicleCaravan = vehicle.GetVehicleCaravan();
            if (GenTicks.TicksGame != lastRenderedTick && Time.frameCount % 2 == 0 || mat != null && tmpRenderTex == null)
            {
                var worldObject = vehicleCaravan ?? GetWorldObject(vehicle);
                if (worldObject is null) return;
                lastRenderedTick = GenTicks.TicksGame;
                Find.World.renderer.wantedMode = WorldRenderMode.Planet;
                Find.WorldCameraDriver.JumpTo(worldObject.DrawPos);
                Find.WorldCameraDriver.altitude = 140f;
                desiredAltitude(Find.WorldCameraDriver) = 140f;
                Find.WorldCameraDriver.Update();
                Find.WorldCamera.gameObject.SetActive(true);
                WorldRendererUtility.UpdateGlobalShadersParams();
                ExpandableWorldObjectsUtility.ExpandableWorldObjectsUpdate();
                foreach (var layer in Find.World.renderer.AllVisibleDrawLayers.Where(l => l is not WorldDrawLayer_SingleTile && l is not WorldDrawLayer_Satellites))
                {
                    layer.Render();
                }
                Find.World.dynamicDrawManager.DrawDynamicWorldObjects();

                if (tmpRenderTex != null)
                {
                    RenderTexture.ReleaseTemporary(tmpRenderTex);
                }
                tmpRenderTex = RenderTexture.GetTemporary(textureSize, textureSize);
                var targetTexture = Find.WorldCamera.targetTexture;
                Find.WorldCamera.targetTexture = tmpRenderTex;
                Find.WorldCamera.Render();
                Find.WorldCamera.targetTexture = targetTexture;
                Find.World.renderer.wantedMode = WorldRenderMode.None;
                Find.WorldCamera.gameObject.SetActive(false);
                Find.Camera.gameObject.SetActive(true);
                Find.CameraDriver.Update();
                if (mat == null)
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

                angle =
                    worldObject switch
                    {
                        VehicleCaravan vehicleCaravan2 => AngleOnPlanetSurface(Find.WorldGrid.GetTileCenter(vehicleCaravan2.vehiclePather.NextTile.Valid ? vehicleCaravan2.vehiclePather.NextTile : vehicleCaravan2.Tile), Find.WorldGrid.GetTileCenter(vehicleCaravan2.Tile)),
                        Caravan caravan => AngleOnPlanetSurface(Find.WorldGrid.GetTileCenter(caravan.pather.nextTile.Valid ? caravan.pather.nextTile : caravan.Tile), Find.WorldGrid.GetTileCenter(caravan.Tile)),
                        AerialVehicleInFlight aerial => AngleOnPlanetSurface(aerial.DrawPos, aerial.position),
                        _ => 90f
                    };
                var rot = Rot4.FromAngleFlat(angle);
                if (vehicleCaravan != null)
                {
                    foreach (var vehicle2 in vehicleCaravan.Vehicles)
                    {
                        vehicle2.FullRotation = rot;
                    }
                }
                else vehicle.FullRotation = rot;
            }

            var center = new Vector3(MeshSize.x / 2f, 0f, MeshSize.y / 2f);
            // 背景
            Graphics.DrawMesh(mesh200, center, Quaternion.identity,
                mat != null ? mat : SolidColorMaterials.SimpleSolidColorMaterial(Color.black), 0);

            // 空の暗さ
            skyMat.color = Color.black.WithAlpha((1f - vehicle.VehicleMap.skyManager.CurSkyGlow) * 0.2f);
            skyMat.renderQueue = 3100;
            Graphics.DrawMesh(mesh200, center.WithY(AltitudeLayer.LightingOverlay.AltitudeFor()), Quaternion.identity, skyMat, 0);

            //　車両本体
            if (vehicleCaravan != null)
            {
                var drawPositions = vehicleCaravan.DrawPositions;
                if (!drawPositions.Keys.SequenceEqual(vehicleCaravan.Vehicles))
                    vehicleCaravan.RecalculateVehiclePositions();

                foreach (var vehicle2 in vehicleCaravan.Vehicles)
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
        else if(tmpRenderTex != null && focused)
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
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var g_DrawingMap = AccessTools.PropertyGetter(typeof(WorldRendererUtility), nameof(WorldRendererUtility.DrawingMap));
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
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsVehicleMapOf),
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
    private static readonly List<Pawn> tmpList = [];

    [PatchLevel(Level.Safe)]
    public static List<Pawn> Postfix(List<Pawn> __result, Map ___map)
    {
        if (___map.IsVehicleMapOf(out _)) return __result;

        tmpList.Clear();
        tmpList.AddRange(__result);
        foreach (var vehicle in VehiclePawnWithMapCache.TryGetAllVehiclesOn(___map))
        {
            tmpList.AddRange(vehicle.VehicleMap.mapPawns.AllPawns);
        }
        return tmpList;
    }

    [PatchLevel(Level.Mandatory)]
    [HarmonyReversePatch]
    public static List<Pawn> AllPawns(MapPawns instance) => throw new NotImplementedException();
}

[HarmonyBefore(VehicleFramework.HarmonyId)]
[HarmonyPatchCategory(EarlyPatchCore.Category)]
[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AllPawnsSpawned), MethodType.Getter)]
[PatchLevel(Level.Mandatory)]
public static class Patch_MapPawns_AllPawnsSpawned
{
    private static readonly List<Pawn> tmpList = [];

    public static IReadOnlyList<Pawn> Postfix(IReadOnlyList<Pawn> __result, Map ___map)
    {
        if (___map.IsVehicleMapOf(out _)) return __result;

        tmpList.Clear();
        tmpList.AddRange(__result);
        foreach (var vehicle in VehiclePawnWithMapCache.TryGetAllVehiclesOn(___map))
        {
            tmpList.AddRange(vehicle.VehicleMap.mapPawns.AllPawnsSpawned);
        }
        return tmpList;
    }

    [HarmonyReversePatch]
    public static List<Pawn> AllPawnsSpawned(MapPawns instance) => throw new NotImplementedException();
}

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.FreeHumanlikesSpawnedOfFaction))]
public static class Patch_MapPawns_FreeHumanlikesSpawnedOfFaction
{
    [PatchLevel(Level.Safe)]
    public static void Postfix(List<Pawn> __result, Map ___map, Faction faction)
    {
        __result.AddRange(VehiclePawnWithMapCache.TryGetAllVehiclesOn(___map).SelectMany(v => v.VehicleMap.mapPawns.FreeHumanlikesSpawnedOfFaction(faction)));
    }

    [PatchLevel(Level.Mandatory)]
    [HarmonyReversePatch]
    public static List<Pawn> FreeHumanlikesSpawnedOfFaction(MapPawns instance, Faction faction) => throw new NotImplementedException();
}

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.SpawnedBabiesInFaction))]
[PatchLevel(Level.Safe)]
public static class Patch_MapPawns_SpawnedBabiesInFaction
{
    public static void Postfix(List<Pawn> __result, Map ___map, Faction faction)
    {
        __result.AddRange(VehiclePawnWithMapCache.TryGetAllVehiclesOn(___map).SelectMany(v => v.VehicleMap.mapPawns.SpawnedBabiesInFaction(faction)));
    }
}

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AnyPawnBlockingMapRemoval), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_MapPawns_AnyPawnBlockingMapRemoval
{
    public static void Postfix(ref bool __result, Map ___map)
    {
        __result = __result || VehiclePawnWithMapCache.TryGetAllVehiclesOn(___map).Any(v => v.VehicleMap.mapPawns.AnyPawnBlockingMapRemoval);
    }
}

[HarmonyPatch(typeof(PawnsFinder), nameof(PawnsFinder.AllMaps), MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_PawnsFinder_AllMaps
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var g_AllPawns = AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.AllPawns));
        var m_AllPawns_Reverse = AccessTools.Method(typeof(Patch_MapPawns_AllPawns), nameof(Patch_MapPawns_AllPawns.AllPawns));
        return instructions.MethodReplacer(g_AllPawns, m_AllPawns_Reverse);
    }
}

[HarmonyPatch(typeof(PawnsFinder), nameof(PawnsFinder.AllMaps_Spawned), MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_PawnsFinder_AllMaps_Spawned
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var g_AllPawnsSpawned = AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.AllPawnsSpawned));
        var m_AllPawnsSpawned_Reverse = AccessTools.Method(typeof(Patch_MapPawns_AllPawnsSpawned), nameof(Patch_MapPawns_AllPawnsSpawned.AllPawnsSpawned));
        return instructions.MethodReplacer(g_AllPawnsSpawned, m_AllPawnsSpawned_Reverse);
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
        var codes = instructions.ToList();
        var m_DrawFieldEdges = AccessTools.Method(typeof(GenDraw), nameof(GenDraw.DrawFieldEdges), [typeof(List<IntVec3>), typeof(Color), typeof(float?), typeof(HashSet<IntVec3>), typeof(int)]);
        var m_DrawFieldEdgesOnVehicle = AccessTools.Method(typeof(GenDrawOnVehicle), nameof(GenDrawOnVehicle.DrawFieldEdges), [typeof(List<IntVec3>), typeof(Color), typeof(float?), typeof(HashSet<IntVec3>), typeof(int), typeof(Map)]);
        var pos = codes.FindIndex(c => c.Calls(m_DrawFieldEdges));
        codes[pos].operand = m_DrawFieldEdgesOnVehicle;
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Room), nameof(Room.Map)))
        ]);
        return codes;
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
        if (__result.IsVehicleMapOf(out _))
        {
            __result = ___maps.FirstOrDefault(m => m.Tile == tile && !m.IsVehicleMapOf(out _));
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
        var m_CeilToInt = AccessTools.Method(typeof(Mathf), nameof(Mathf.CeilToInt));
        var pos = codes.FindIndex(c => c.Calls(m_CeilToInt));

        codes[pos].operand = AccessTools.Method(typeof(Patch_SteadyEnvironmentEffects_SteadyEnvironmentEffectsTick), nameof(ChanceToInt));
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
        if (__instance.IsVehicleMapOf(out _) && Find.Maps.Contains(__instance))
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

    private static readonly AccessTools.FieldRef<PathFinderMapData, AreaSource> areas = AccessTools.FieldRefAccess<PathFinderMapData, AreaSource>("areas");
}

//キャラバン壊滅時
[HarmonyPatch(typeof(Caravan), nameof(Caravan.Notify_PawnRemoved))]
[PatchLevel(Level.Safe)]
public static class Patch_Caravan_Notify_PawnRemoved
{
    public static void Postfix(Pawn p)
    {
        if (p is VehiclePawnWithMap vehicle)
        {
            Delay.AfterNTicks(5, () =>
            {
                if (vehicle.IsWorldPawn() && vehicle.ParentHolder is null) vehicle.RemoveVehicleMap();
            });
        }
    }
}

[HarmonyPatch(typeof(StorytellerUtility), nameof(StorytellerUtility.DefaultThreatPointsNow))]
[PatchLevel(Level.Cautious)]
public static class Patch_StorytellerUtility_DefaultThreatPointsNow
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var g_IsPocketMap = AccessTools.PropertyGetter(typeof(Map), nameof(Map.IsPocketMap));
        var m_IsPocketMapReplace = AccessTools.Method(typeof(Patch_StorytellerUtility_DefaultThreatPointsNow), nameof(IsPocketMapReplace));
        return instructions.MethodReplacer(g_IsPocketMap, m_IsPocketMapReplace);
    }

    private static bool IsPocketMapReplace(Map map)
    {
        if (!map.IsPocketMap) return false;
        return map.PocketMapParent?.sourceMap is not null;
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
    public static void Postfix(Map map, Faction faction, ref IAttackTarget threat, bool countDormantPawnsAsHostile, bool canBeFogged, ref bool __result)
    {
        if (__result) return;

        foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOn(map))
        {
            foreach (var attackTarget in vehicle.VehicleMap.attackTargetsCache.TargetsHostileToFaction(faction))
            {
                if (GenHostility.IsActiveThreatTo(attackTarget, faction, true, canBeFogged))
                {
                    threat = attackTarget;
                    __result = true;
                    return;
                }
                if (countDormantPawnsAsHostile && attackTarget.Thing.HostileTo(faction) && (canBeFogged || !attackTarget.Thing.Fogged()) && !attackTarget.ThreatDisabled(null))
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