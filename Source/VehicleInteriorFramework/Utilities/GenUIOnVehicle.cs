﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public static class GenUIOnVehicle
    {
        public static List<Thing> ThingsUnderMouse(Vector3 clickPos, float pawnWideClickRadius, TargetingParameters clickParams, ITargetingSource source)
        {
            return GenUIOnVehicle.ThingsUnderMouse(clickPos, pawnWideClickRadius, clickParams, source, GenUIOnVehicle.vehicleForSelector);
        }

        public static List<Thing> ThingsUnderMouse(Vector3 clickPos, float pawnWideClickRadius, TargetingParameters clickParams, ITargetingSource source, VehiclePawnWithMap vehicle)
        {
            var clickPosVehicleCor = clickPos.ToVehicleMapCoord(vehicle);
            IntVec3 intVec = IntVec3.FromVector3(clickPosVehicleCor);
            tmpList.Clear();
            IReadOnlyList<Pawn> allPawnsSpawned = vehicle.VehicleMap.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawnsSpawned.Count; i++)
            {
                Pawn pawn = allPawnsSpawned[i];
                if ((pawn.DrawPos - clickPos).MagnitudeHorizontal() < 0.4f && clickParams.CanTarget(pawn, source))
                {
                    tmpList.Add(pawn);
                    tmpList.AddRange(ContainingSelectionUtility.SelectableContainedThings(pawn));
                }
            }
            tmpList.Sort(new Comparison<Thing>(GenUIOnVehicle.CompareThingsByDistanceToMousePointer));
            cellThings.Clear();
            foreach (Thing thing4 in vehicle.VehicleMap.thingGrid.ThingsAt(intVec))
            {
                if (!tmpList.Contains(thing4) && clickParams.CanTarget(thing4, source))
                {
                    cellThings.Add(thing4);
                    cellThings.AddRange(ContainingSelectionUtility.SelectableContainedThings(thing4));
                }
            }
            IntVec3[] adjacentCells = GenAdj.AdjacentCells;
            for (int j = 0; j < adjacentCells.Length; j++)
            {
                IntVec3 c = adjacentCells[j] + intVec;
                if (c.InBounds(vehicle.VehicleMap) && c.GetItemCount(vehicle.VehicleMap) > 1)
                {
                    foreach (Thing thing2 in vehicle.VehicleMap.thingGrid.ThingsAt(c))
                    {
                        if (thing2.def.category == ThingCategory.Item && (thing2.TrueCenter() - clickPos).MagnitudeHorizontalSquared() <= 0.25f && !tmpList.Contains(thing2) && clickParams.CanTarget(thing2, source))
                        {
                            cellThings.Add(thing2);
                        }
                    }
                }
            }
            List<Thing> list2 = vehicle.VehicleMap.listerThings.ThingsInGroup(ThingRequestGroup.WithCustomRectForSelector);
            for (int k = 0; k < list2.Count; k++)
            {
                Thing thing3 = list2[k];
                if (thing3.CustomRectForSelector != null && thing3.CustomRectForSelector.Value.Contains(intVec) && !tmpList.Contains(thing3) && clickParams.CanTarget(thing3, source))
                {
                    cellThings.Add(thing3);
                }
            }
            cellThings.Sort(new Comparison<Thing>(GenUIOnVehicle.CompareThingsByDrawAltitudeOrDistToItem));
            tmpList.AddRange(cellThings);
            cellThings.Clear();
            IReadOnlyList<Pawn> allPawnsSpawned2 = vehicle.VehicleMap.mapPawns.AllPawnsSpawned;
            for (int l = 0; l < allPawnsSpawned2.Count; l++)
            {
                Pawn pawn2 = allPawnsSpawned2[l];
                if ((pawn2.DrawPos - clickPos).MagnitudeHorizontal() < pawnWideClickRadius && clickParams.CanTarget(pawn2, source))
                {
                    cellThings.Add(pawn2);
                }
            }
            cellThings.Sort(new Comparison<Thing>(GenUIOnVehicle.CompareThingsByDistanceToMousePointer));
            for (int m = 0; m < cellThings.Count; m++)
            {
                if (!tmpList.Contains(cellThings[m]))
                {
                    tmpList.Add(cellThings[m]);
                    tmpList.AddRange(ContainingSelectionUtility.SelectableContainedThings(cellThings[m]));
                }
            }
            tmpList.RemoveAll((Thing thing) => !clickParams.CanTarget(thing, source));
            tmpList.RemoveAll(delegate (Thing thing)
            {
                return thing is Pawn pawn3 && pawn3.IsHiddenFromPlayer();
            });
            return tmpList;
        }

        private static List<Thing> tmpList = new List<Thing>();

        private static List<Thing> cellThings = new List<Thing>(32);

        private static int CompareThingsByDistanceToMousePointer(Thing a, Thing b)
        {
            var b2 = UI.MouseMapPosition();
            float num = (a.DrawPosHeld.Value - b2).MagnitudeHorizontalSquared();
            float num2 = (b.DrawPosHeld.Value - b2).MagnitudeHorizontalSquared();
            if (num < num2)
            {
                return -1;
            }
            if (num == num2)
            {
                return b.Spawned.CompareTo(a.Spawned);
            }
            return 1;
        }

        private static int CompareThingsByDrawAltitudeOrDistToItem(Thing A, Thing B)
        {
            var mousePos = UI.MouseMapPosition();
            if (A.def.category == ThingCategory.Item && B.def.category == ThingCategory.Item)
            {
                return (A.TrueCenter() - mousePos).MagnitudeHorizontalSquared().CompareTo((B.TrueCenter() - mousePos).MagnitudeHorizontalSquared());
            }
            Thing spawnedParentOrMe = A.SpawnedParentOrMe;
            Thing spawnedParentOrMe2 = B.SpawnedParentOrMe;
            if (spawnedParentOrMe.def.Altitude != spawnedParentOrMe2.def.Altitude)
            {
                return spawnedParentOrMe2.def.Altitude.CompareTo(spawnedParentOrMe.def.Altitude);
            }
            return B.Spawned.CompareTo(A.Spawned);
        }

        public static IEnumerable<LocalTargetInfo> TargetsAtMouse(TargetingParameters clickParams, bool thingsOnly = false, ITargetingSource source = null)
        {
            var clickPos = UI.MouseMapPosition();
            Thing caster;
            if ((caster = source?.Caster) != null)
            {
                TargetMapManager.TargetMap[caster] = Find.CurrentMap;
            }
            bool convToVehicleMap;
            if (!(convToVehicleMap = Find.CurrentMap.IsVehicleMapOf(out var vehicle)))
            {
                if (clickPos.TryGetVehicleMap(Find.CurrentMap, out vehicle, false))
                {
                    if (source is Verb_Jump || source is Verb_CastAbilityJump || source is Verb_LaunchZipline)
                    {
                        convToVehicleMap = true;
                        if (caster != null)
                        {
                            TargetMapManager.TargetMap[caster] = vehicle.VehicleMap;
                        }
                    }
                }
            }
            return GenUIOnVehicle.TargetsAt(clickPos, clickParams, thingsOnly, source, vehicle, convToVehicleMap).ToArray();
        }

        public static IEnumerable<LocalTargetInfo> TargetsAt(Vector3 clickPos, TargetingParameters clickParams, bool thingsOnly, ITargetingSource source = null, bool convToVehicleMap = true)
        {
            return GenUIOnVehicle.TargetsAt(clickPos, clickParams, thingsOnly, source, GenUIOnVehicle.vehicleForSelector, convToVehicleMap);
        }

        public static IEnumerable<LocalTargetInfo> TargetsAt(Vector3 clickPos, TargetingParameters clickParams, bool thingsOnly, ITargetingSource source, VehiclePawnWithMap vehicle, bool convToVehicleMap = true)
        {
            List<Thing> clickableList;
            if (vehicle != null)
            {
                clickableList = GenUIOnVehicle.ThingsUnderMouse(clickPos, 0.8f, clickParams, source, vehicle);
            }
            else
            {
                clickableList = GenUI.ThingsUnderMouse(clickPos, 0.8f, clickParams, source);
            }
            Thing caster = source?.Caster;
            int num;
            for (int i = 0; i < clickableList.Count; i = num + 1)
            {
                if (clickableList[i] is VehiclePawn vehicle2 && vehicle2 == FloatMenuMakerMap.makingFor)
                {
                    num = i;
                    continue;
                }

                if (!(clickableList[i] is Pawn pawn) || !pawn.IsPsychologicallyInvisible() || caster == null || caster.Faction == pawn.Faction)
                {
                    yield return clickableList[i];
                }
                num = i;
            }
            if (!thingsOnly)
            {
                IntVec3 intVec = (convToVehicleMap && vehicle != null) ? clickPos.ToVehicleMapCoord(vehicle).ToIntVec3() : clickPos.ToIntVec3();
                Map map = (convToVehicleMap && vehicle != null) ? vehicle.VehicleMap : Find.CurrentMap;
                if (intVec.InBounds(map, clickParams.mapBoundsContractedBy) && clickParams.CanTarget(new TargetInfo(intVec, map, false), source))
                {
                    yield return intVec;
                }
            }
        }

        public static VehiclePawnWithMap vehicleForSelector;
    }
}
