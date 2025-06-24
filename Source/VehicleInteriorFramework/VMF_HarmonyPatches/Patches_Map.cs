using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.Sound;
using static VehicleInteriors.MethodInfoCache;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    //VehicleMapの時はいくつかを専用のSectionLayerに置き換え、そうでなければそれらは除外する
    [HarmonyPatch(typeof(Section), MethodType.Constructor, typeof(IntVec3), typeof(Map))]
    public static class Patch_Section_Constructor
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var getAllSubclassesNA = AccessTools.Method(typeof(GenTypes), nameof(GenTypes.AllSubclassesNonAbstract));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(getAllSubclassesNA)) + 1;

            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(2),
                CodeInstruction.Call(typeof(VehicleMapUtility), nameof(VehicleMapUtility.SelectSectionLayers))
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
    public static class Patch_MechanitorUtility_InMechanitorCommandRange
    {
        public static void Prefix(Pawn mech, ref LocalTargetInfo target)
        {
            if (Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots.TryGetValue(mech, out var spots))
            {
                var destMap = spots.enterSpot.Map ?? spots.exitSpot.Map.BaseMap() ?? mech.MapHeld;
                if (destMap.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
                {
                    target = target.Cell.ToBaseMapCoord(vehicle);
                }
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
        }
    }

    [HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.CanCommandTo))]
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
        public static void Postfix(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams, Map ___map, ref bool __result)
        {
            if (__result || !VehiclePawnWithMapCache.AllVehiclesOn(___map.BaseMap()).Any()) return;

            Map destMap;
            if ((destMap = dest.Thing?.MapHeld) == null && traverseParams.pawn != null && TargetMapManager.HasTargetMap(traverseParams.pawn, out var map))
            {
                destMap = map;
            }
            if (!ReachabilityUtilityOnVehicle.working && traverseParams.pawn != null && destMap != null && traverseParams.pawn.Map != destMap)
            {
                __result = ReachabilityUtilityOnVehicle.CanReach(traverseParams.pawn.Map, start, dest, peMode, traverseParams, destMap, out _, out _);
            }
        }

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

    [HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReachMapEdge), typeof(IntVec3), typeof(TraverseParms))]
    public static class Patch_Reachability_CanReachMapEdge
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return Patch_Reachability_CanReach.Transpiler(instructions);
        }
    }

    [HarmonyPatch(typeof(Pawn_PlayerSettings), nameof(Pawn_PlayerSettings.EffectiveAreaRestrictionInPawnCurrentMap), MethodType.Getter)]
    public static class Patch_Pawn_PlayerSettings_EffectiveAreaRestrictionInPawnCurrentMap
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
        }
    }

    //主にlisterHaulablesの再計算の時のチェックでベースマップや他の車両マップを検索対象に含めるためメソッドを置き換え
    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.IsInValidBestStorage))]
    public static class Patch_StoreUtility_IsInValidBestStorage
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_StoreUtility_TryFindBestBetterStorageFor = AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStorageFor));
            var m_StoreAcrossMapsUtility_TryFindBestBetterStorageFor = AccessTools.Method(typeof(StoreAcrossMapsUtility), nameof(StoreAcrossMapsUtility.TryFindBestBetterStorageForReplace));
            return instructions.MethodReplacer(m_StoreUtility_TryFindBestBetterStorageFor, m_StoreAcrossMapsUtility_TryFindBestBetterStorageFor);
        }
    }

    //VehicleMapの外気温はマップ上のその位置の気温、スポーンしてないなら今いるタイルの外気温
    [HarmonyPatch(typeof(MapTemperature), nameof(MapTemperature.OutdoorTemp), MethodType.Getter)]
    public static class Patch_MapTemperature_OutdoorTemp
    {
        public static bool Prefix(Map ___map, ref float __result)
        {
            if (___map.IsVehicleMapOf(out var vehicle))
            {
                if (vehicle.Spawned)
                {
                    __result = vehicle.Position.GetTemperature(vehicle.Map);
                    return false;
                }
                else if (vehicle.Tile != -1)
                {
                    __result = Find.World.tileTemperatures.GetOutdoorTemp(vehicle.Tile);
                    return false;
                }
            }
            return true;
        }
    }

    //リソースカウンターに車上マップのリソースを追加
    [HarmonyPatch(typeof(ResourceCounter), nameof(ResourceCounter.UpdateResourceCounts))]
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
        public static void Postfix(Map __instance)
        {
            WorldObject GetWorldObject(IThingHolder holder)
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

            if (VehicleInteriors.settings.drawPlanet && Find.CurrentMap == __instance && __instance.IsVehicleMapOf(out var vehicle) &&
                !WorldRendererUtility.WorldRendered)
            {
                if (Find.World.renderer.RegenerateLayersIfDirtyInLongEvent())
                {
                    return;
                }

                if (Find.TickManager.TicksGame != lastRenderedTick && Time.frameCount % 2 == 0)
                {
                    var worldObject = GetWorldObject(vehicle);
                    if (worldObject == null) return;
                    lastRenderedTick = Find.TickManager.TicksGame;
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
                    vehicle.FullRotation = worldObject is VehicleCaravan vehicleCaravan ?
                        Rot8.FromAngle((Find.WorldGrid.GetTileCenter(vehicleCaravan.vehiclePather.nextTile != -1 ? vehicleCaravan.vehiclePather.nextTile : vehicleCaravan.Tile) - Find.WorldGrid.GetTileCenter(vehicleCaravan.Tile)).AngleFlat()) :
                        worldObject is Caravan caravan ?
                        Rot8.FromAngle((Find.WorldGrid.GetTileCenter(caravan.pather.nextTile != -1 ? caravan.pather.nextTile : caravan.Tile) - Find.WorldGrid.GetTileCenter(caravan.Tile)).AngleFlat()) :
                        worldObject is AerialVehicleInFlight aerial ? Rot8.FromAngle((aerial.DrawPos - aerial.position).AngleFlat()) : Rot8.East;
                }
                var longSide = Mathf.Max(vehicle.DrawSize.x / 2f, vehicle.DrawSize.y / 2f);
                var drawPos = new Vector3(longSide, 0f, longSide);
                if (mat != null)
                {
                    Graphics.DrawMesh(mesh200, drawPos, Quaternion.identity, mat, 0);
                }
                vehicle.DynamicDrawPhaseAt(DrawPhase.Draw, drawPos);
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var g_DrawingMap = AccessTools.PropertyGetter(typeof(WorldRendererUtility), nameof(WorldRendererUtility.DrawingMap));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(g_DrawingMap)) + 1;
            var label = generator.DefineLabel();
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Brtrue_S, label),
                new CodeInstruction(OpCodes.Pop),
                CodeInstruction.LoadField(typeof(VehicleInteriors), nameof(VehicleInteriors.settings)),
                CodeInstruction.LoadField(typeof(VehicleMapSettings), nameof(VehicleMapSettings.drawPlanet)),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Ldloca, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsVehicleMapOf),
            });
            return codes;
        }

        private static RenderTexture renderTexture = RenderTexture.GetTemporary(textureSize, textureSize);

        private const int textureSize = 2048;

        private static Mesh mesh200 = MeshPool.GridPlane(new Vector2(200f, 200f));

        private static Material mat;

        public static int lastRenderedTick = -1;

        private static AccessTools.FieldRef<WorldCameraDriver, float> desiredAltitude = AccessTools.FieldRefAccess<WorldCameraDriver, float>("desiredAltitude");
    }

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AllPawns), MethodType.Getter)]
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

        private static List<Pawn> tmpList = new List<Pawn>();
    }

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AllPawnsSpawned), MethodType.Getter)]
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

        private static List<Pawn> tmpList = new List<Pawn>();
    }

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.FreeHumanlikesSpawnedOfFaction))]
    public static class Patch_MapPawns_FreeHumanlikesSpawnedOfFaction
    {
        public static void Postfix(List<Pawn> __result, Map ___map, Faction faction)
        {
            __result.AddRange(VehiclePawnWithMapCache.TryGetAllVehiclesOn(___map).SelectMany(v => v.VehicleMap.mapPawns.FreeHumanlikesSpawnedOfFaction(faction)));
        }
    }

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.SpawnedBabiesInFaction))]
    public static class Patch_MapPawns_SpawnedBabiesInFaction
    {
        public static void Postfix(List<Pawn> __result, Map ___map, Faction faction)
        {
            __result.AddRange(VehiclePawnWithMapCache.TryGetAllVehiclesOn(___map).SelectMany(v => v.VehicleMap.mapPawns.SpawnedBabiesInFaction(faction)));
        }
    }

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AnyPawnBlockingMapRemoval), MethodType.Getter)]
    public static class Patch_MapPawns_AnyPawnBlockingMapRemoval
    {
        public static void Postfix(ref bool __result, Map ___map)
        {
            __result = __result || VehiclePawnWithMapCache.TryGetAllVehiclesOn(___map).Any(v => v.VehicleMap.mapPawns.AnyPawnBlockingMapRemoval);
        }
    }

    [HarmonyBefore("SettlementQuestsMod")]
    [HarmonyPatchCategory("VehicleInteriors.EarlyPatches")]
    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Tile), MethodType.Getter)]
    public static class Patch_WorldObject_Tile
    {
        public static bool Prefix(WorldObject __instance, ref PlanetTile __result)
        {
            if (__instance is MapParent_Vehicle mapParent_Vehicle)
            {
                if (mapParent_Vehicle.vehicle.Spawned)
                {
                    __result = mapParent_Vehicle.vehicle.Map.Tile;
                    return false;
                }

                WorldObject GetWorldObject(IThingHolder holder)
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
                var worldObject2 = GetWorldObject(mapParent_Vehicle.vehicle);
                if (worldObject2 is AerialVehicleInFlight aerial)
                {
                    __result = GetTile(aerial);
                    return false;
                }
                if (worldObject2 == null || worldObject2 is MapParent_Vehicle)
                {
                    return true;
                }
                __result = worldObject2.Tile;
                return false;
            }
            return true;
        }

        private static ConcurrentDictionary<AerialVehicleInFlight, int> tileCache = new ConcurrentDictionary<AerialVehicleInFlight, int>();

        private static int lastCachedTick = -1;

        public static PlanetTile GetTile(AerialVehicleInFlight aerial)
        {
            if (tileCache.TryGetValue(aerial, out var tile))
            {
                if (Find.TickManager.TicksAbs - lastCachedTick > 30)
                {
                    lastCachedTick = Find.TickManager.TicksAbs;
                    Task.Run(() =>
                    {
                        tileCache.RemoveRange(tileCache.Keys.Where(a => !Find.WorldObjects.Contains(a)).ToArray());
                        tileCache[aerial] = WorldHelper.GetNearestTile(aerial.DrawPos);
                    });
                }
                return tileCache[aerial];
            }
            tileCache[aerial] = WorldHelper.GetNearestTile(aerial.DrawPos);
            return tileCache[aerial];
        }
    }

    [HarmonyPatch(typeof(CameraJumper), nameof(CameraJumper.GetWorldTarget))]
    public static class Patch_CameraJumper_GetWorldTarget
    {
        public static void Prefix(ref GlobalTargetInfo target)
        {
            if (target.Thing?.IsOnVehicleMapOf(out var vehicle) ?? false)
            {
                target = vehicle;
            }
        }
    }

    [HarmonyPatch(typeof(DesignationManager), nameof(DesignationManager.DesignationOn))]
    public static class Patch_DesignationManager_DesignationOn
    {
        [HarmonyPatch(new Type[] { typeof(Thing) })]
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

        [HarmonyPatch(new Type[] { typeof(Thing), typeof(DesignationDef) })]
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
    public static class Patch_Room_DrawFieldEdges
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var m_DrawFieldEdges = AccessTools.Method(typeof(GenDraw), nameof(GenDraw.DrawFieldEdges), new Type[] { typeof(List<IntVec3>), typeof(Color), typeof(float?), typeof(HashSet<IntVec3>), typeof(int) });
            var m_DrawFieldEdgesOnVehicle = AccessTools.Method(typeof(GenDrawOnVehicle), nameof(GenDrawOnVehicle.DrawFieldEdges), new Type[] { typeof(List<IntVec3>), typeof(Color), typeof(float?), typeof(HashSet<IntVec3>), typeof(int), typeof(Map) });
            var pos = codes.FindIndex(c => c.Calls(m_DrawFieldEdges));
            codes[pos].operand = m_DrawFieldEdgesOnVehicle;
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Room), nameof(Room.Map)))
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.MapParentAt))]
    public static class Patch_WorldObjectsHolder_MapParentAt
    {
        public static void Postfix(ref MapParent __result, List<MapParent> ___mapParents, PlanetTile tile)
        {
            if (__result is MapParent_Vehicle)
            {
                __result = ___mapParents.FirstOrDefault(p => p.Tile == tile && p.Isnt<MapParent_Vehicle>());
            }
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.FindMap), typeof(PlanetTile))]
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

    [HarmonyPatch(typeof(WealthWatcher), nameof(WealthWatcher.ForceRecount))]
    public static class Patch_WealthWatcher_ForceRecount
    {
        public static void Postfix(Map ___map, ref float ___wealthItems, ref float ___wealthBuildings, ref float ___wealthFloorsOnly, float ___lastCountTick)
        {
            var state = Current.ProgramState;
            Current.ProgramState = ProgramState.Playing;
            foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOn(___map))
            {
                ___wealthItems += vehicle.VehicleMap.wealthWatcher.WealthItems;
                ___wealthBuildings += vehicle.VehicleMap.wealthWatcher.WealthBuildings;
                ___wealthFloorsOnly += vehicle.VehicleMap.wealthWatcher.WealthFloorsOnly;
            }
            Current.ProgramState = state;
        }
    }

    [HarmonyPatch(typeof(Hediff_MetalhorrorImplant), nameof(Hediff_MetalhorrorImplant.Emerge))]
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
    public static class Patch_HaulDestinationManager_AddHaulSource
    {
        public static void Postfix(Map ___map, IHaulSource source)
        {
            ___map.GetCachedMapComponent<CrossMapHaulDestinationManager>().AddHaulSource(source);
        }
    }

    [HarmonyPatch(typeof(HaulDestinationManager), nameof(HaulDestinationManager.AddHaulDestination))]
    public static class Patch_HaulDestinationManager_AddHaulDestination
    {
        public static void Postfix(Map ___map, IHaulDestination haulDestination)
        {
            ___map.GetCachedMapComponent<CrossMapHaulDestinationManager>().AddHaulDestination(haulDestination);
        }
    }

    [HarmonyPatch(typeof(HaulDestinationManager), nameof(HaulDestinationManager.RemoveHaulSource))]
    public static class Patch_HaulDestinationManager_RemoveHaulSource
    {
        public static void Postfix(Map ___map, IHaulSource source)
        {
            ___map.GetCachedMapComponent<CrossMapHaulDestinationManager>().RemoveHaulSource(source);
        }
    }

    [HarmonyPatch(typeof(HaulDestinationManager), nameof(HaulDestinationManager.RemoveHaulDestination))]
    public static class Patch_HaulDestinationManager_RemoveHaulDestination
    {
        public static void Postfix(Map ___map, IHaulDestination haulDestination)
        {
            ___map.GetCachedMapComponent<CrossMapHaulDestinationManager>().RemoveHaulDestination(haulDestination);
        }
    }

    [HarmonyPatch(typeof(HaulDestinationManager), nameof(HaulDestinationManager.Notify_HaulDestinationChangedPriority))]
    public static class Patch_HaulDestinationManager_Notify_HaulDestinationChangedPriority
    {
        public static void Postfix(Map ___map)
        {
            ___map.GetCachedMapComponent<CrossMapHaulDestinationManager>().Notify_HaulDestinationChangedPriority();
        }
    }

    //極端に小さいマップではCeilToIntのせいで毎tick必ずどこかのセルの物が劣化する処理だったんでこれを車両マップ上では緩和
    [HarmonyPatch(typeof(SteadyEnvironmentEffects), nameof(SteadyEnvironmentEffects.SteadyEnvironmentEffectsTick))]
    public static class Patch_SteadyEnvironmentEffects_SteadyEnvironmentEffectsTick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var m_CeilToInt = AccessTools.Method(typeof(Mathf), nameof(Mathf.CeilToInt));
            var pos = codes.FindIndex(c => c.Calls(m_CeilToInt));

            codes[pos].operand = AccessTools.Method(typeof(Patch_SteadyEnvironmentEffects_SteadyEnvironmentEffectsTick), nameof(ChanceToInt));
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(SteadyEnvironmentEffects), "map")
            });
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

    [HarmonyPatch(typeof(MapParent), nameof(MapParent.GetTransportersFloatMenuOptions))]
    public static class Patch_MapParent_GetTransportersFloatMenuOptions
    {
        public static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> values, MapParent __instance, IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction)
        {
            foreach (var value in values)
            {
                yield return value;
            }

            IEnumerable<VehiclePawnWithMap> vehicles = null;
            if (__instance.HasMap)
            {
                vehicles = VehiclePawnWithMapCache.AllVehiclesOn(__instance.Map);
            }

            if (vehicles.NullOrEmpty()) yield break;

            foreach (var vehicle in vehicles)
            {
                var mapParent = vehicle.VehicleMap.Parent;
                var aerial = vehicle.GetAerialVehicle();
                var tile = aerial != null ? Patch_WorldObject_Tile.GetTile(aerial) : vehicle.GetRootTile();

                bool CanLandInSpecificCell()
                {
                    if (mapParent == null || !mapParent.HasMap)
                    {
                        return false;
                    }
                    if (mapParent.EnterCooldownBlocksEntering())
                    {
                        return FloatMenuAcceptanceReport.WithFailMessage("MessageEnterCooldownBlocksEntering".Translate(mapParent.EnterCooldownTicksLeft().ToStringTicksToPeriod()));
                    }
                    return true;
                }

                if (!CanLandInSpecificCell())
                {
                    continue;
                }
                yield return new FloatMenuOption("VMF_LandInSpecificMap".Translate(vehicle.VehicleMap.Parent.Label, __instance.Label), delegate
                {
                    Map map = vehicle.VehicleMap;
                    Current.Game.CurrentMap = map;
                    CameraJumper.TryHideWorld();
                    MapComponentCache<VehiclePawnWithMapCache>.GetComponent(map).ForceResetCache();
                    Find.Targeter.BeginTargeting(TargetingParameters.ForDropPodsDestination(), x =>
                    {
                        launchAction(__instance.Tile, new TransportersArrivalAction_LandInSpecificCell(mapParent, x.Cell, Rot4.North, landInShuttle: false));
                    }, null, null, CompLaunchable.TargeterMouseAttachment);
                });
            }
        }
    }
}