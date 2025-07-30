using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles.World;
using Verse;
using Verse.AI;
using Verse.Sound;
using static VehicleMapFramework.MethodInfoCache;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework.VMF_HarmonyPatches;

//VehicleMapの時はいくつかを専用のSectionLayerに置き換え、そうでなければそれらは除外する
[HarmonyPatch(typeof(Section), MethodType.Constructor, typeof(IntVec3), typeof(Map))]
[PatchLevel(Level.Mandatory)]
public static class Patch_Section_Constructor
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var getAllSubclassesNA = AccessTools.Method(typeof(GenTypes), nameof(GenTypes.AllSubclassesNonAbstract));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(getAllSubclassesNA)) + 1;

        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(2),
            CodeInstruction.Call(typeof(VehicleMapUtility), nameof(VehicleMapUtility.SelectSectionLayers))
        ]);
        return codes;
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
    public static bool Prefix(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams, ref bool __result)
    {
        if (CrossMapReachabilityUtility.working) return true;

        Map destMap = CrossMapReachabilityUtility.DestMap ??
            dest.Thing?.MapHeld ??
            (TargetMapManager.HasTargetInfo(traverseParams.pawn, out var target) && (LocalTargetInfo)target == dest ? target.Map : traverseParams.pawn?.Map);
        if (destMap == null)
        {
            return true;
        }
        Map departMap = CrossMapReachabilityUtility.DepartMap ?? traverseParams.pawn?.Map;
        if (departMap == null)
        {
            return true;
        }
        if (departMap != destMap)
        {
            __result = CrossMapReachabilityUtility.CanReach(departMap, start, dest, peMode, traverseParams, destMap);
            return false;
        }
        return true;
    }

    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Map));
        codes[pos] = new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing);

        var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Beq_S);
        codes.Insert(pos2, new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMap_Map));
        return codes;
    }
}

[HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReachNonLocal), typeof(IntVec3), typeof(TargetInfo), typeof(PathEndMode), typeof(TraverseParms))]
[PatchLevel(Level.Safe)]
public static class Patch_Reachability_CanReachNonLocal
{
    public static bool Prefix(IntVec3 start, TargetInfo dest, PathEndMode peMode, TraverseParms traverseParams, Map ___map, ref bool __result)
    {
        var destMap = dest.Map;
        if (___map.BaseMap() == destMap.BaseMap())
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
            else if (vehicle.Tile != -1)
            {
                __result = Find.World.tileTemperatures.GetOutdoorTemp(vehicle.Tile);
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
            for (int i = 0; i < allGroupsListForReading.Count; i++)
            {
                foreach (Thing outerThing in allGroupsListForReading[i].HeldThings)
                {
                    Thing innerIfMinified = outerThing.GetInnerIfMinified();
                    if (innerIfMinified.def.CountAsResource && !innerIfMinified.IsNotFresh())
                    {
                        ThingDef def = innerIfMinified.def;
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
    [PatchLevel(Level.Safe)]
    public static void Postfix(Map __instance)
    {
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

        if (VehicleMapFramework.settings.drawPlanet && Find.CurrentMap == __instance && __instance.IsVehicleMapOf(out var vehicle) &&
            WorldRendererUtility.DrawingMap)
        {
            if (Find.World.renderer.RegenerateLayersIfDirtyInLongEvent())
            {
                return;
            }

            float angle = vehicle.Transform.rotation + vehicle.Rotation.AsAngle;
            if (GenTicks.TicksGame != lastRenderedTick && Time.frameCount % 2 == 0)
            {
                var worldObject = GetWorldObject(vehicle);
                if (worldObject == null) return;
                lastRenderedTick = GenTicks.TicksGame;
                var targetTexture = Find.WorldCamera.targetTexture;
                Find.World.renderer.wantedMode = WorldRenderMode.Planet;
                Find.WorldCameraDriver.JumpTo(worldObject.DrawPos);
                Find.WorldCameraDriver.altitude = 140f;
                desiredAltitude(Find.WorldCameraDriver) = 140f;
                Find.WorldCameraDriver.Update();
                Find.WorldCamera.gameObject.SetActive(true);
                WorldRendererUtility.UpdateGlobalShadersParams();
                ExpandableWorldObjectsUtility.ExpandableWorldObjectsUpdate();
                foreach (var layer in Find.World.renderer.AllVisibleDrawLayers.Where(l => l.Isnt<WorldDrawLayer_SingleTile>() && l.Isnt<WorldDrawLayer_Satellites>()))
                {
                    layer.Render();
                }
                Find.World.dynamicDrawManager.DrawDynamicWorldObjects();
                Find.WorldCamera.targetTexture = renderTexture;
                Find.WorldCamera.Render();
                Find.WorldCamera.targetTexture = targetTexture;
                Find.World.renderer.wantedMode = WorldRenderMode.None;
                Find.WorldCamera.gameObject.SetActive(false);
                Find.Camera.gameObject.SetActive(true);
                Find.CameraDriver.Update();
                if (mat == null)
                {
                    mat = MaterialPool.MatFrom(new MaterialRequest(renderTexture));
                }
                else
                {
                    mat.SetTexture(0, renderTexture);
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

                if (GenTicks.TicksGame % 4 == 0)
                {
                    angle =
                        worldObject is VehicleCaravan vehicleCaravan ?
                        AngleOnPlanetSurface(Find.WorldGrid.GetTileCenter(vehicleCaravan.vehiclePather.nextTile.Valid ? vehicleCaravan.vehiclePather.nextTile : vehicleCaravan.Tile), Find.WorldGrid.GetTileCenter(vehicleCaravan.Tile)) :
                        worldObject is Caravan caravan ?
                        AngleOnPlanetSurface(Find.WorldGrid.GetTileCenter(caravan.pather.nextTile.Valid ? caravan.pather.nextTile : caravan.Tile), Find.WorldGrid.GetTileCenter(caravan.Tile)) :
                        worldObject is AerialVehicleInFlight aerial ?
                        AngleOnPlanetSurface(aerial.DrawPos, aerial.position) : 90f;
                    vehicle.FullRotation = Rot4.FromAngleFlat(angle);
                }
            }
            var longSide = Mathf.Max(vehicle.DrawSize.x / 2f, vehicle.DrawSize.y / 2f);
            var drawPos = new Vector3(longSide, 0f, longSide);
            if (mat != null)
            {
                Graphics.DrawMesh(mesh200, drawPos, Quaternion.identity, mat, 0);
            }
            
            skyMat.color = Color.black.WithAlpha((1f - vehicle.VehicleMap.skyManager.CurSkyGlow) * 0.2f);
            skyMat.renderQueue = 3100;
            Graphics.DrawMesh(mesh200, drawPos.WithY(AltitudeLayer.LightingOverlay.AltitudeFor()), Quaternion.identity, skyMat, 0);
            drawPos = drawPos.SetToAltitude(AltitudeLayer.LayingPawn);
            vehicle.DrawAt(in drawPos, vehicle.FullRotation, angle - vehicle.Rotation.AsAngle);
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

    private static RenderTexture renderTexture = RenderTexture.GetTemporary(textureSize, textureSize);

    private const int textureSize = 2048;

    public readonly static Vector2 MeshSize = new(200f, 200f);

    private static Mesh mesh200 = MeshPool.GridPlane(MeshSize);

    private static Material mat;

    private static Material skyMat = SolidColorMaterials.NewSolidColorMaterial(Color.black, ShaderDatabase.SolidColor);

    public static int lastRenderedTick = -1;

    private static AccessTools.FieldRef<WorldCameraDriver, float> desiredAltitude = AccessTools.FieldRefAccess<WorldCameraDriver, float>("desiredAltitude");
}

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AllPawns), MethodType.Getter)]
[PatchLevel(Level.Sensitive)]
public static class Patch_MapPawns_AllPawns
{
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

    private static List<Pawn> tmpList = [];
}

[HarmonyBefore(VehicleFramework.HarmonyId)]
[HarmonyPatchCategory(EarlyPatchCore.Category)]
[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AllPawnsSpawned), MethodType.Getter)]
[PatchLevel(Level.Mandatory)]
public static class Patch_MapPawns_AllPawnsSpawned
{
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

    private static List<Pawn> tmpList = [];
}

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.FreeHumanlikesSpawnedOfFaction))]
[PatchLevel(Level.Sensitive)]
public static class Patch_MapPawns_FreeHumanlikesSpawnedOfFaction
{
    public static void Postfix(List<Pawn> __result, Map ___map, Faction faction)
    {
        __result.AddRange(VehiclePawnWithMapCache.TryGetAllVehiclesOn(___map).SelectMany(v => v.VehicleMap.mapPawns.FreeHumanlikesSpawnedOfFaction(faction)));
    }
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

[HarmonyPatch(typeof(DesignationManager), nameof(DesignationManager.DesignationOn))]
[PatchLevel(Level.Safe)]
public static class Patch_DesignationManager_DesignationOn
{
    [HarmonyPatch([typeof(Thing)])]
    [HarmonyPrefix]
    public static bool Prefix1(Thing t, DesignationManager __instance, ref Designation __result)
    {
        var thingMap = t.MapHeld;
        if (thingMap != null && thingMap != __instance.map)
        {
            __result = thingMap.designationManager.DesignationOn(t);
            return false;
        }
        return true;
    }

    [HarmonyPatch([typeof(Thing), typeof(DesignationDef)])]
    [HarmonyPrefix]
    public static bool Prefix2(Thing t, DesignationDef def, DesignationManager __instance, ref Designation __result)
    {
        var thingMap = t.MapHeld;
        if (thingMap != null && thingMap != __instance.map)
        {
            __result = thingMap.designationManager.DesignationOn(t, def);
            return false;
        }
        return true;
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

[HarmonyPatch(typeof(Hediff_MetalhorrorImplant), nameof(Hediff_MetalhorrorImplant.Emerge))]
[PatchLevel(Level.Cautious)]
public static class Patch_Hediff_MetalhorrorImplant_Emerge
{
    private static bool Prepare()
    {
        return ModsConfig.AnomalyActive;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
    }
}

[HarmonyPatch(typeof(HaulDestinationManager), nameof(HaulDestinationManager.AddHaulSource))]
[PatchLevel(Level.Mandatory)]
public static class Patch_HaulDestinationManager_AddHaulSource
{
    public static void Postfix(Map ___map, IHaulSource source)
    {
        ___map.GetCachedMapComponent<CrossMapHaulDestinationManager>().AddHaulSource(source);
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

[HarmonyPatch(typeof(HaulDestinationManager), nameof(HaulDestinationManager.RemoveHaulSource))]
[PatchLevel(Level.Mandatory)]
public static class Patch_HaulDestinationManager_RemoveHaulSource
{
    public static void Postfix(Map ___map, IHaulSource source)
    {
        ___map.GetCachedMapComponent<CrossMapHaulDestinationManager>().RemoveHaulSource(source);
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
        if (__instance.IsVehicleMapOf(out _))
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