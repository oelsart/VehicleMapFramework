using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
            var clickPosVehicleCor = clickPos.VehicleMapToOrig(vehicle);
            IntVec3 intVec = IntVec3.FromVector3(clickPosVehicleCor);
            List<Thing> list = new List<Thing>();
            IReadOnlyList<Pawn> allPawnsSpawned = vehicle.VehicleMap.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawnsSpawned.Count; i++)
            {
                Pawn pawn = allPawnsSpawned[i];
                if ((pawn.DrawPos - clickPos).MagnitudeHorizontal() < 0.4f && clickParams.CanTarget(pawn, source))
                {
                    list.Add(pawn);
                    list.AddRange(ContainingSelectionUtility.SelectableContainedThings(pawn));
                }
            }
            list.Sort(new Comparison<Thing>(GenUIOnVehicle.CompareThingsByDistanceToMousePointer));
            var cellThings = new List<Thing>(32);
            foreach (Thing thing4 in vehicle.VehicleMap.thingGrid.ThingsAt(intVec))
            {
                if (!list.Contains(thing4) && clickParams.CanTarget(thing4, source))
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
                        if (thing2.def.category == ThingCategory.Item && (thing2.TrueCenter() - clickPos).MagnitudeHorizontalSquared() <= 0.25f && !list.Contains(thing2) && clickParams.CanTarget(thing2, source))
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
                if (thing3.CustomRectForSelector != null && thing3.CustomRectForSelector.Value.Contains(intVec) && !list.Contains(thing3) && clickParams.CanTarget(thing3, source))
                {
                    cellThings.Add(thing3);
                }
            }
            cellThings.Sort(new Comparison<Thing>(GenUIOnVehicle.CompareThingsByDrawAltitudeOrDistToItem));
            list.AddRange(cellThings);
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
                if (!list.Contains(cellThings[m]))
                {
                    list.Add(cellThings[m]);
                    list.AddRange(ContainingSelectionUtility.SelectableContainedThings(cellThings[m]));
                }
            }
            list.RemoveAll((Thing thing) => !clickParams.CanTarget(thing, source));
            list.RemoveAll(delegate (Thing thing)
            {
                return thing is Pawn pawn3 && pawn3.IsHiddenFromPlayer();
            });
            return list;
        }

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
            clickPos.TryGetVehiclePawnWithMap(Find.CurrentMap, out var vehicle);
            var list = GenUIOnVehicle.TargetsAt(clickPos, clickParams, thingsOnly, source, vehicle, false).ToArray();
            return list;
        }

        public static IEnumerable<LocalTargetInfo> TargetsAt(Vector3 clickPos, TargetingParameters clickParams, bool thingsOnly, ITargetingSource source, bool convToVehicleMap = true)
        {
            return GenUIOnVehicle.TargetsAt(clickPos, clickParams, thingsOnly, source, GenUIOnVehicle.vehicleForSelector, convToVehicleMap);
        }

        public static IEnumerable<LocalTargetInfo> TargetsAt(Vector3 clickPos, TargetingParameters clickParams, bool thingsOnly, ITargetingSource source, VehiclePawnWithMap vehicle, bool convToVehicleMap = true)
        {
            List<Thing> clickableList = vehicle != null ?
                GenUIOnVehicle.ThingsUnderMouse(clickPos, 0.8f, clickParams, source, vehicle) :
                GenUI.ThingsUnderMouse(clickPos, 0.8f, clickParams, source);
            Thing caster = source?.Caster;
            int num;
            for (int i = 0; i < clickableList.Count; i = num + 1)
            {
                if (!(clickableList[i] is Pawn pawn) || !pawn.IsPsychologicallyInvisible() || caster == null || caster.Faction == pawn.Faction)
                {
                    yield return clickableList[i];
                }
                num = i;
            }
            if (!thingsOnly)
            {
                IntVec3 intVec = (convToVehicleMap && vehicle != null) ? clickPos.VehicleMapToOrig(vehicle).ToIntVec3() : clickPos.ToIntVec3();
                Map map = (convToVehicleMap && vehicle != null) ? vehicle.VehicleMap : Find.CurrentMap;
                if (intVec.InBounds(map, clickParams.mapBoundsContractedBy) && clickParams.CanTarget(new TargetInfo(intVec, map, false), source))
                {
                    yield return intVec;
                }
            }
            yield break;
        }

        public static VehiclePawnWithMap vehicleForSelector;
    }
}
