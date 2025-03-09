using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.Sound;

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
            if (Patch_MultiPawnGotoController_RecomputeDestinations.tmpEnterSpots.TryGetValue((mech, target.Cell), out var spots))
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
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap);
        }
    }

    [HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.CanCommandTo))]
    public static class Patch_Pawn_MechanitorTracker_CanCommandTo
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReach), typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms))]
    public static class Patch_Reachability_CanReach
    {
        [HarmonyPriority(Priority.Low)]
        public static bool Prefix(Reachability __instance, IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams, ref bool __result)
        {
            if (traverseParams.pawn != null && dest.HasThing && dest.Thing.MapHeld != null && dest.Thing.MapHeld.reachability != __instance)
            {
                __result = ReachabilityUtilityOnVehicle.CanReach(traverseParams.pawn.Map, start, dest, peMode, traverseParams, dest.Thing.MapHeld, out _, out _);
                return false;
            }
            return true;
        }

        [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
        [HarmonyPriority(Priority.Normal)]
        public static bool CanReachPatched(this Reachability instance, IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams)
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();

                var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Map));
                codes[pos] = new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Thing);

                var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Beq_S);
                codes.Insert(pos2, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Map));
                return codes;
            }
            _ = Transpiler(null);
            throw new NotImplementedException();
        }
    }

    [HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReachMapEdge), typeof(IntVec3), typeof(TraverseParms))]
    public static class Patch_Reachability_CanReachMapEdge
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Map));
            codes[pos] = new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Thing);

            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Beq_S);
            codes.Insert(pos2, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Map));
            return codes;
        }
    }

    [HarmonyPatch(typeof(Pawn_PlayerSettings), nameof(Pawn_PlayerSettings.EffectiveAreaRestrictionInPawnCurrentMap), MethodType.Getter)]
    public static class Patch_Pawn_PlayerSettings_EffectiveAreaRestrictionInPawnCurrentMap
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap);
        }
    }

    ////ストレージの優先度変更の時ベースマップや他の車両マップのlisterHaulablesにも通知
    //[HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.Priority), MethodType.Setter)]
    //public static class Patch_StorageSettings_Priority
    //{
    //    public static void Postfix(StorageSettings __instance)
    //    {
    //        if (Current.ProgramState != ProgramState.Playing)
    //        {
    //            return;
    //        }
    //        if (__instance.owner is StorageGroup storageGroup && storageGroup.Map != null)
    //        {
    //            var baseMap = storageGroup.Map.BaseMap();
    //            foreach (var map in VehiclePawnWithMapCache.allVehicles[baseMap].Select(v => v.interiorMap).Concat(baseMap).Where(m => m != storageGroup.Map))
    //            {
    //                map.listerHaulables.RecalculateAllInHaulSources(storageGroup.HaulSourcesList);
    //            }
    //        }
    //    }
    //}

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
                !WorldRendererUtility.WorldRenderedNow)
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
                    WorldRendererUtility.UpdateWorldShadersParams();
                    ExpandableWorldObjectsUtility.ExpandableWorldObjectsUpdate();
                    foreach (var layer in layers(Find.World.renderer).Where(l => l.Isnt<WorldLayer_SingleTile>() && l.Isnt<WorldLayer_Sun>() && l.Isnt<WorldLayer_Stars>()))
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
                vehicle.DrawAt(drawPos, vehicle.FullRotation, 0f);
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var g_WorldRenderedNow = AccessTools.PropertyGetter(typeof(WorldRendererUtility), nameof(WorldRendererUtility.WorldRenderedNow));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(g_WorldRenderedNow)) + 1;
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
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsVehicleMapOf),
            });
            return codes;
        }

        private static RenderTexture renderTexture = RenderTexture.GetTemporary(textureSize, textureSize);

        private const int textureSize = 2048;

        private static Mesh mesh200 = MeshPool.GridPlane(new Vector2(200f, 200f));

        private static Material mat;

        public static int lastRenderedTick = -1;

        private static AccessTools.FieldRef<WorldRenderer, List<WorldLayer>> layers = AccessTools.FieldRefAccess<WorldRenderer, List<WorldLayer>>("layers");

        private static AccessTools.FieldRef<WorldCameraDriver, float> desiredAltitude = AccessTools.FieldRefAccess<WorldCameraDriver, float>("desiredAltitude");
    }

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AllPawns), MethodType.Getter)]
    public static class Patch_MapPawns_AllPawns
    {
        public static List<Pawn> Postfix(List<Pawn> __result, Map ___map)
        {
            return __result.Concat(VehiclePawnWithMapCache.AllVehiclesOn(___map).SelectMany(v => v.VehicleMap.mapPawns.AllPawnsSpawned)).ToList();
        }
    }

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AllPawnsSpawned), MethodType.Getter)]
    public static class Patch_MapPawns_AllPawnsSpawned
    {
        public static IReadOnlyList<Pawn> Postfix(IReadOnlyList<Pawn> __result, Map ___map)
        {
            return __result.Concat(VehiclePawnWithMapCache.AllVehiclesOn(___map).SelectMany(v => v.VehicleMap.mapPawns.AllPawnsSpawned)).ToArray();
        }
    }

    //[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AllPawnsUnspawned), MethodType.Getter)]
    //public static class Patch_MapPawns_AllPawnsUnspawned
    //{
    //    public static void Postfix(List<Pawn> __result, Map ___map)
    //    {
    //        __result.AddRange(VehiclePawnWithMapCache.allVehicles[___map].SelectMany(v => v.VehicleMap.mapPawns.AllPawnsUnspawned));
    //    }
    //}

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.FreeHumanlikesSpawnedOfFaction))]
    public static class Patch_MapPawns_FreeHumanlikesSpawnedOfFaction
    {
        public static void Postfix(List<Pawn> __result, Map ___map, Faction faction)
        {
            __result.AddRange(VehiclePawnWithMapCache.AllVehiclesOn(___map).SelectMany(v => v.VehicleMap.mapPawns.FreeHumanlikesSpawnedOfFaction(faction)));
        }
    }

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.SpawnedBabiesInFaction))]
    public static class Patch_MapPawns_SpawnedBabiesInFaction
    {
        public static void Postfix(List<Pawn> __result, Map ___map, Faction faction)
        {
            __result.AddRange(VehiclePawnWithMapCache.AllVehiclesOn(___map).SelectMany(v => v.VehicleMap.mapPawns.SpawnedBabiesInFaction(faction)));
        }
    }

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AnyPawnBlockingMapRemoval), MethodType.Getter)]
    public static class Patch_MapPawns_AnyPawnBlockingMapRemoval
    {
        public static void Postfix(ref bool __result, Map ___map)
        {
            __result = __result || VehiclePawnWithMapCache.AllVehiclesOn(___map).Any(v => v.VehicleMap.mapPawns.AnyPawnBlockingMapRemoval);
        }
    }


    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Tile), MethodType.Getter)]
    public static class Patch_WorldObject_Tile
    {
        public static bool Prefix(WorldObject __instance, ref int __result)
        {
            if (__instance is MapParent_Vehicle mapParent_Vehicle)
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
                var worldObject2 = GetWorldObject(mapParent_Vehicle.vehicle);
                if (worldObject2 is AerialVehicleInFlight aerial)
                {
                    __result = WorldHelper.GetNearestTile(aerial.DrawPos);
                    return false;
                }
                if (worldObject2 == null)
                {
                    return true;
                }
                __result = worldObject2.Tile;
                return false;
            }
            return true;
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
            if (thingMap != __instance.map)
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
            if (thingMap != __instance.map)
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
            var m_DrawFieldEdges = AccessTools.Method(typeof(GenDraw), nameof(GenDraw.DrawFieldEdges), new Type[] { typeof(List<IntVec3>), typeof(Color), typeof(float?) });
            var m_DrawFieldEdgesOnVehicle = AccessTools.Method(typeof(GenDrawOnVehicle), nameof(GenDrawOnVehicle.DrawFieldEdges), new Type[] { typeof(List<IntVec3>), typeof(Color), typeof(float?), typeof(Map) });
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(m_DrawFieldEdges));
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
        public static void Postfix(ref MapParent __result, List<MapParent> ___mapParents, int tile)
        {
            if (__result is MapParent_Vehicle)
            {
                __result = ___mapParents.FirstOrDefault(p => p.Tile == tile && p.Isnt<MapParent_Vehicle>());
            }
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.FindMap), typeof(int))]
    public static class Patch_Game_FindMap
    {
        public static void Postfix(ref Map __result, List<Map> ___maps, int tile)
        {
            if (__result.IsVehicleMapOf(out _))
            {
                __result = ___maps.FirstOrDefault(m => m.Tile == tile && !m.IsVehicleMapOf(out _));
            }
        }
    }

    //ForceRecountへのパッチだと実行タイミングがずれてProgramStateがMapInitializingになるようだったので、RecountIfNeededにパッチ
    [HarmonyPatch(typeof(WealthWatcher), "RecountIfNeeded")]
    public static class Patch_WealthWatcher_RecountIfNeeded
    {
        public static void Postfix(Map ___map, ref float ___wealthItems, ref float ___wealthBuildings, ref float ___wealthFloorsOnly, float ___lastCountTick)
        {
            if (Find.TickManager.TicksGame == ___lastCountTick)
            {
                foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOn(___map))
                {
                    ___wealthItems += vehicle.VehicleMap.wealthWatcher.WealthItems;
                    ___wealthBuildings += vehicle.VehicleMap.wealthWatcher.WealthBuildings;
                    ___wealthFloorsOnly += vehicle.VehicleMap.wealthWatcher.WealthFloorsOnly;
                }
            }
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
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_MapHeld, MethodInfoCache.m_MapHeldBaseMap);
        }
    }
}