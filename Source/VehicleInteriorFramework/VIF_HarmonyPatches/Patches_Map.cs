using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.Noise;
using static Vehicles.VehicleRegion;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    //VehicleMapの時はSectionLayer_VehicleMapの継承クラスを使い、そうでなければそれらは除外する
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

    [HarmonyPatch(typeof(DynamicDrawManager), "ComputeCulledThings")]
    public static class Patch_DynamicDrawManager_ComputeCulledThings
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_CellRect_ClipInsideMap, MethodInfoCache.m_ClipInsideVehicleMap);
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
    public static class Patch_MechanitorUtility_InMechanitorCommandRange
    {
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
        public static bool Prefix(Reachability __instance, IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams, ref bool __result)
        {
            if (traverseParams.pawn != null && traverseParams.pawn.jobs.DeterminingNextJob && dest.HasThing && dest.Thing.MapHeld.reachability != __instance)
            {
                __result = ReachabilityUtilityOnVehicle.CanReach(traverseParams.pawn.Map, start, dest, peMode, traverseParams, dest.Thing.MapHeld, out _, out _);
                return false;
            }
            return true;
        }

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
            var m_StoreAcrossMapsUtility_TryFindBestBetterStorageFor = AccessTools.Method(typeof(StoreAcrossMapsUtility), nameof(StoreAcrossMapsUtility.TryFindBestBetterStorageFor),
                new Type[] { typeof(Thing), typeof(Pawn), typeof(Map), typeof(StoragePriority), typeof(Faction), typeof(IntVec3).MakeByRefType(), typeof(IHaulDestination).MakeByRefType(), typeof(bool) });
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
            List<SlotGroup> allGroupsListForReading = VehiclePawnWithMapCache.allVehicles[___map].SelectMany(v => v.VehicleMap.haulDestinationManager.AllGroupsListForReading).ToList(); ;
            for (int i = 0; i < allGroupsListForReading.Count; i++)
            {
                foreach (Thing outerThing in allGroupsListForReading[i].HeldThings)
                {
                    Thing innerIfMinified = outerThing.GetInnerIfMinified();
                    if (innerIfMinified.def.CountAsResource && !innerIfMinified.IsNotFresh())
                    {
                        Dictionary<ThingDef, int> dictionary = ___countedAmounts;
                        ThingDef def = innerIfMinified.def;
                        dictionary[def] += innerIfMinified.stackCount;
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
                if (Find.TickManager.TicksGame != lastRenderedTick)
                {
                    lastRenderedTick = Find.TickManager.TicksGame;
                    var worldObject = GetWorldObject(vehicle);
                    var targetTexture = Find.WorldCamera.targetTexture;
                    Find.World.renderer.wantedMode = WorldRenderMode.Planet;
                    Find.WorldCameraDriver.JumpTo(worldObject.DrawPos);
                    Find.WorldCameraDriver.ResetAltitude();
                    Find.WorldCameraDriver.Update();
                    Find.WorldCamera.gameObject.SetActive(true);
                    WorldRendererUtility.UpdateWorldShadersParams();
                    foreach (var layer in layers(Find.World.renderer).Where(l => l.Isnt<WorldLayer_SingleTile>()))
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
                    RenderTexture.active = renderTexture;
                    texture.ReadPixels(new Rect(0f, 0f, 2048, 2048), 0, 0);
                    texture.Apply();
                    RenderTexture.active = null;
                    if (mat == null)
                    {
                        mat = MaterialPool.MatFrom(texture);
                    }
                    else
                    {
                        mat.SetTexture(0, texture);
                    }
                    vehicle.FullRotation = worldObject is VehicleCaravan vehicleCaravan ?
                        Rot8.FromAngle((Find.WorldGrid.GetTileCenter(vehicleCaravan.vehiclePather.nextTile != -1 ? vehicleCaravan.vehiclePather.nextTile : vehicleCaravan.Tile) - Find.WorldGrid.GetTileCenter(vehicleCaravan.Tile)).AngleFlat()) :
                        worldObject is Caravan caravan ?
                        Rot8.FromAngle((Find.WorldGrid.GetTileCenter(caravan.pather.nextTile != -1 ? caravan.pather.nextTile : caravan.Tile) - Find.WorldGrid.GetTileCenter(caravan.Tile)).AngleFlat()) :
                        worldObject is AerialVehicleInFlight aerial ? Rot8.FromAngle((aerial.DrawPos - aerial.position).AngleFlat()) : Rot8.East;
                }
                var longSide = Mathf.Max(vehicle.DrawSize.x / 2f, vehicle.DrawSize.y / 2f);
                var drawPos = new Vector3(longSide, 0f, longSide);
                Graphics.DrawMesh(mesh200, drawPos, Quaternion.identity, mat, 0);
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

        private static RenderTexture renderTexture = RenderTexture.GetTemporary(2048, 2048);

        private static Texture2D texture = new Texture2D(2048, 2048);

        private static Mesh mesh200 = MeshMakerPlanes.NewPlaneMesh(200f);

        private static Material mat;

        private static int lastRenderedTick = 0;

        private const int tickInterval = 60;

        private static AccessTools.FieldRef<WorldRenderer, List<WorldLayer>> layers = AccessTools.FieldRefAccess<WorldRenderer, List<WorldLayer>>("layers");
    }
}
