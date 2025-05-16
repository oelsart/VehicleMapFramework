using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Vehicles;
using Verse;
using Verse.Noise;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    //車上マップにそれぞれVirtualMapTransferしてColonyThingsWillingToBuyを集める
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ColonyThingsWillingToBuy))]
    public static class Patch_Pawn_ColonyThingsWillingToBuy
    {
        public static void Prefix(Pawn playerNegotiator)
        {
            ReachabilityUtilityOnVehicle.tmpDepartMap = playerNegotiator.Map;
        }

        public static IEnumerable<Thing> Postfix(IEnumerable<Thing> values, Pawn playerNegotiator, Pawn __instance)
        {
            if (values != null)
            {
                foreach (var thing in values)
                {
                    yield return thing;
                }
            }
            var maps = __instance.Map.BaseMapAndVehicleMaps().Except(__instance.Map);
            if (!maps.Any()) yield break;
            var departMap = playerNegotiator.Map;
            try
            {
                foreach (var map in maps)
                {
                    __instance.VirtualMapTransfer(map);
                    foreach (var thing in __instance.trader.ColonyThingsWillingToBuy(playerNegotiator))
                    {
                        yield return thing;
                    }
                }
            }
            finally
            {
                __instance.VirtualMapTransfer(departMap);
                ReachabilityUtilityOnVehicle.tmpDepartMap = null;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_TraderTracker), "ReachableForTrade")]
    public static class Patch_Pawn_TraderTracker_ReachableForTrade
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_Reachability_CanReach1, MethodInfoCache.m_CanReachReaplaceable1);
        }
    }

    //キャラバンのメンバーにVehiclePawnWithMapが含まれる場合そのVehicleMap上の物も取引できるようにする
    [HarmonyPatch(typeof(Caravan), nameof(Caravan.ColonyThingsWillingToBuy))]
    public static class Patch_Caravan_ColonyThingsWillingToBuy
    {
        public static IEnumerable<Thing> Postfix(IEnumerable<Thing> values, Pawn playerNegotiator)
        {
            var vehicles = playerNegotiator.GetCaravan()?.PawnsListForReading?.OfType<VehiclePawnWithMap>() ?? playerNegotiator.GetVehicleCaravan()?.Vehicles?.OfType<VehiclePawnWithMap>();

            if (values != null)
            {
                foreach (var thing in values)
                {
                    yield return thing;
                }
            }
            if (!vehicles.NullOrEmpty())
            {
                foreach (var vehicle in vehicles)
                {
                    foreach (var thing in vehicle.ColonyThingsWillingToBuyOnVehicle(playerNegotiator))
                    {
                        yield return thing;
                    }
                }
            }
            else if (playerNegotiator is VehiclePawnWithMap vehicle2)
            {
                foreach (var thing in vehicle2.ColonyThingsWillingToBuyOnVehicle(playerNegotiator))
                {
                    yield return thing;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Settlement), nameof(Settlement.ColonyThingsWillingToBuy))]
    public static class Patch_Settlement_ColonyThingsWillingToBuy
    {
        public static IEnumerable<Thing> Postfix(IEnumerable<Thing> values, Pawn playerNegotiator) => Patch_Caravan_ColonyThingsWillingToBuy.Postfix(values, playerNegotiator);
    }

    //トレードビーコンの検索時車上マップのビーコンを含める
    [HarmonyPatch(typeof(Building_OrbitalTradeBeacon), nameof(Building_OrbitalTradeBeacon.AllPowered))]
    public static class Patch_Building_OrbitalTradeBeacon_AllPowered
    {
        public static IEnumerable<Building_OrbitalTradeBeacon> Postfix(IEnumerable<Building_OrbitalTradeBeacon> values, Map map)
        {
            foreach (var b in values) yield return b;

            var maps = map.BaseMapAndVehicleMaps().Except(map);
            var buildings = maps.SelectMany(m => m.listerBuildings.AllBuildingsColonistOfClass<Building_OrbitalTradeBeacon>().Where(b =>
            {
                var comp = b.GetComp<CompPowerTrader>();
                return comp == null || comp.PowerOn;
            }));

            foreach (var b in buildings) yield return b;
        }
    }

    //ビーコンを含めただけでは売却可能なポーンが追加されないのでこれも追加する
    [HarmonyPatch(typeof(TradeShip), nameof(TradeShip.ColonyThingsWillingToBuy))]
    public static class Patch_TradeShip_ColonyThingsWillingToBuy
    {
        public static IEnumerable<Thing> Postfix(IEnumerable<Thing> values, Pawn playerNegotiator)
        {
            var result = values.ToList();
            var maps = playerNegotiator.Map.BaseMapAndVehicleMaps().Except(playerNegotiator.Map);

            foreach (var map in maps)
            {
                result.AddRange(TradeUtility.AllSellableColonyPawns(map, false));
            }
            return result;
        }
    }

    //車上マップのビーコンが含まれているのでMapは引数じゃなくそこから取る
    //c.GetThingList(map) -> c.GetThingList(building_OrbitalTradeBeacon.Map)
    [HarmonyPatch]
    public static class Patch_TradeUtility_AllLaunchableThingsForTrade
    {
        private static MethodInfo TargetMethod()
        {
            var type = AccessTools.FirstInner(typeof(TradeUtility), t => t.Name.Contains("AllLaunchableThingsForTrade"));
            return AccessTools.Method(type, "MoveNext");
        }

        //ローカル変数からビーコンを取ろうとするとforeachのMoveNextタイミングによってなんかがなんかしてたまにnullになるのでstaticフィールドでやりとりします
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_GetThingList = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetThingList));
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(m_GetThingList))
                {
                    yield return CodeInstruction.Call(typeof(Patch_TradeUtility_AllLaunchableThingsForTrade), nameof(BuildingMap));
                }

                yield return instruction;

                if (instruction.opcode == OpCodes.Stloc_2)
                {
                    yield return CodeInstruction.LoadLocal(2);
                    yield return CodeInstruction.StoreField(typeof(Patch_TradeUtility_AllLaunchableThingsForTrade), nameof(beacon));
                }
            }
        }

        public static Building_OrbitalTradeBeacon beacon;

        private static Map BuildingMap(Map map)
        {
            return beacon?.Map ?? map;
        }
    }

    //posのInBoundsチェックはやってるのに範囲内のセルのInBoundsはチェックしてないのぉ？なんでよ……まあ建築限界線があるからだろうけども。チェックを追加します。
    [HarmonyPatch(typeof(Building_OrbitalTradeBeacon), nameof(Building_OrbitalTradeBeacon.TradeableCellsAround))]
    public static class Patch_Building_OrbitalTradeBeacon_TradeableCellsAround
    {
        public static void Postfix(Map map, List<IntVec3> __result)
        {
            __result.RemoveAll(c => !c.InBounds(map));
        }
    }

    //map.thingGrid.ThingsAt(c) -> building_OrbitalTradeBeacon.Map.thingGrid.ThingsAt(c)
    [HarmonyPatch(typeof(TradeUtility), nameof(TradeUtility.LaunchThingsOfType))]
    public static class Patch_TradeUtility_LaunchThingsOfType
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var f_Map_thingGrid = AccessTools.Field(typeof(Map), nameof(Map.thingGrid));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_Map_thingGrid)) - 1;

            codes.RemoveAt(pos);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadLocal(2),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IEnumerator<Building_OrbitalTradeBeacon>), nameof(IEnumerator.Current))),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Thing_Map)
            });
            return codes;
        }
    }

    //CommsConsoleを車両マップに建てている場合でもトレーダー船が現れるようにする
    [HarmonyPatch(typeof(IncidentWorker_OrbitalTraderArrival), "TryExecuteWorker")]
    public static class Patch_IncidentWorker_OrbitalTraderArrival_TryExecuteWorker
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                yield return instruction;

                if (instruction.LoadsField(AccessTools.Field(typeof(ListerBuildings), nameof(ListerBuildings.allBuildingsColonist))))
                {
                    yield return CodeInstruction.LoadArgument(1);
                    yield return CodeInstruction.Call(typeof(Patch_IncidentWorker_OrbitalTraderArrival_TryExecuteWorker), nameof(AddBuildings));
                }
            }
        }

        private static List<Building> AddBuildings(List<Building> buildings, IncidentParms parms)
        {
            buildings.Clear();
            buildings.AddRange(buildings);
            buildings.AddRange(VehiclePawnWithMapCache.AllVehiclesOn((Map)parms.target).SelectMany(v => v.VehicleMap.listerBuildings.allBuildingsColonist));
            return buildings;
        }

        private static List<Building> buildings = new List<Building>();
    }
}
