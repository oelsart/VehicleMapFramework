using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors;

public static class GenUIOnVehicle
{
    public static List<Thing> ThingsUnderMouse(Vector3 clickPos, float pawnWideClickRadius, TargetingParameters clickParams, ITargetingSource source)
    {
        return GenUIOnVehicle.ThingsUnderMouse(clickPos, pawnWideClickRadius, clickParams, source, vehicleForSelector);
    }

    public static List<Thing> ThingsUnderMouse(Vector3 clickPos, float pawnWideClickRadius, TargetingParameters clickParams, ITargetingSource source, VehiclePawnWithMap vehicle)
    {
        var mouseMapPosition = UI.MouseMapPosition();
        IntVec3 intVec = IntVec3.FromVector3(clickPos);
        Map map = vehicle != null ? vehicle.VehicleMap : Find.CurrentMap;
        var list = new List<Thing>();
        IReadOnlyList<Pawn> allPawnsSpawned = Find.CurrentMap.mapPawns.AllPawnsSpawned;
        for (int i = 0; i < allPawnsSpawned.Count; i++)
        {
            Pawn pawn = allPawnsSpawned[i];
            if ((pawn.DrawPos - mouseMapPosition).MagnitudeHorizontal() < 0.4f && clickParams.CanTarget(pawn, source))
            {
                list.Add(pawn);
                list.AddRange(ContainingSelectionUtility.SelectableContainedThings(pawn));
            }
        }
        list.Sort(CompareThingsByDistanceToMousePointer);
        cellThings.Clear();
        foreach (Thing thing4 in map.thingGrid.ThingsAt(intVec))
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
            if (c.InBounds(map) && c.GetItemCount(map) > 1)
            {
                foreach (Thing thing2 in map.thingGrid.ThingsAt(c))
                {
                    if (thing2.def.category == ThingCategory.Item && (thing2.TrueCenter() - mouseMapPosition).MagnitudeHorizontalSquared() <= 0.25f && !list.Contains(thing2) && clickParams.CanTarget(thing2, source))
                    {
                        cellThings.Add(thing2);
                    }
                }
            }
        }
        List<Thing> list2 = map.listerThings.ThingsInGroup(ThingRequestGroup.WithCustomRectForSelector);
        for (int k = 0; k < list2.Count; k++)
        {
            Thing thing3 = list2[k];
            if (thing3.CustomRectForSelector != null && thing3.CustomRectForSelector.Value.Contains(intVec) && !list.Contains(thing3) && clickParams.CanTarget(thing3, source))
            {
                cellThings.Add(thing3);
            }
        }
        cellThings.Sort(CompareThingsByDrawAltitudeOrDistToItem);
        list.AddRange(cellThings);
        cellThings.Clear();
        for (int l = 0; l < allPawnsSpawned.Count; l++)
        {
            Pawn pawn2 = allPawnsSpawned[l];
            if ((pawn2.DrawPos - mouseMapPosition).MagnitudeHorizontal() < pawnWideClickRadius && clickParams.CanTarget(pawn2, source))
            {
                cellThings.Add(pawn2);
            }
        }
        cellThings.Sort(CompareThingsByDistanceToMousePointer);
        for (int m = 0; m < cellThings.Count; m++)
        {
            if (!list.Contains(cellThings[m]))
            {
                list.Add(cellThings[m]);
                list.AddRange(ContainingSelectionUtility.SelectableContainedThings(cellThings[m]));
            }
        }
        list.RemoveAll(thing => !clickParams.CanTarget(thing, source));
        list.RemoveAll(thing =>
        {
            return thing is Pawn pawn3 && pawn3.IsHiddenFromPlayer();
        });
        list.Remove(vehicleForSelector);
        return list;

        int CompareThingsByDistanceToMousePointer(Thing a, Thing b)
        {
            float num = (a.DrawPosHeld.Value - mouseMapPosition).MagnitudeHorizontalSquared();
            float num2 = (b.DrawPosHeld.Value - mouseMapPosition).MagnitudeHorizontalSquared();
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

        int CompareThingsByDrawAltitudeOrDistToItem(Thing A, Thing B)
        {
            if (A.def.category == ThingCategory.Item && B.def.category == ThingCategory.Item)
            {
                return (A.TrueCenter() - mouseMapPosition).MagnitudeHorizontalSquared().CompareTo((B.TrueCenter() - mouseMapPosition).MagnitudeHorizontalSquared());
            }
            Thing spawnedParentOrMe = A.SpawnedParentOrMe;
            Thing spawnedParentOrMe2 = B.SpawnedParentOrMe;
            if (spawnedParentOrMe.def.Altitude != spawnedParentOrMe2.def.Altitude)
            {
                return spawnedParentOrMe2.def.Altitude.CompareTo(spawnedParentOrMe.def.Altitude);
            }
            return B.Spawned.CompareTo(A.Spawned);
        }
    }

    private static List<Thing> cellThings = new(32);

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
        return [.. GenUIOnVehicle.TargetsAt(clickPos, clickParams, thingsOnly, source, vehicle, convToVehicleMap)];
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

            if (clickableList[i] is not Pawn pawn || !pawn.IsPsychologicallyInvisible() || caster == null || caster.Faction == pawn.Faction)
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
