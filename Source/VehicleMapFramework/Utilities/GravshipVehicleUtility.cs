using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;
using Vehicles;
using Verse;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework
{
    public static class GravshipVehicleUtility
    {
        public static bool placingGravshipVehicle;

        private static readonly Func<IntVec3, Map, AcceptanceReport> IsValidCell = (Func<IntVec3, Map, AcceptanceReport>)AccessTools.Method(typeof(Designator_MoveGravship), "IsValidCell").CreateDelegate(typeof(Func<IntVec3, Map, AcceptanceReport>));

        private static readonly Action<Def, Type, HashSet<ushort>> GiveShortHash = (Action<Def, Type, HashSet<ushort>>)AccessTools.Method(typeof(ShortHashGiver), "GiveShortHash").CreateDelegate(typeof(Action<Def, Type, HashSet<ushort>>));

        private static readonly Dictionary<Type, HashSet<ushort>> takenHashesPerDeftype = AccessTools.StaticFieldRefAccess<Dictionary<Type, HashSet<ushort>>>(typeof(ShortHashGiver), "takenHashesPerDeftype");

        private static readonly Func<WorldComponent_GravshipController, Building_GravEngine, Gravship> RemoveGravshipFromMap =
            (Func<WorldComponent_GravshipController, Building_GravEngine, Gravship>)AccessTools.Method(typeof(WorldComponent_GravshipController), "RemoveGravshipFromMap")
            .CreateDelegate(typeof(Func<WorldComponent_GravshipController, Building_GravEngine, Gravship>));

        public static readonly Func<WorldComponent_GravshipController, Gravship, IntVec3, Map, Building_GravEngine> PlaceGravship =
            (Func<WorldComponent_GravshipController, Gravship, IntVec3, Map, Building_GravEngine>)AccessTools.Method(typeof(WorldComponent_GravshipController), "PlaceGravship")
            .CreateDelegate(typeof(Func<WorldComponent_GravshipController, Gravship, IntVec3, Map, Building_GravEngine>));

        public static bool GravshipProcessInProgress => GravshipUtility.generatingGravship || GravshipPlacementUtility.placingGravship || placingGravshipVehicle;

        public static void PlaceGravshipVehicleUnSpawned(Building_GravEngine engine, IntVec3 loc, Rot4 rot, VehiclePawnWithMap vehicle, bool forced = false)
        {
            if (!ModsConfig.OdysseyActive || GravshipProcessInProgress) return;

            var spawned = engine.Spawned;
            var destroyed = engine.Destroyed;
            var minified = engine.SpawnedParentOrMe as MinifiedThing;
            if (!spawned)
            {
                engine.ForceSetStateToUnspawned();
                engine.stackCount = 1;
                GenSpawn.Spawn(engine, loc, vehicle.VehicleMap, rot);
            }
            PlaceGravshipVehicle(engine, vehicle, forced);
            if (destroyed)
            {
                engine.Destroy();
            }
            else if (!spawned)
            {
                engine.DeSpawn();
            }
            minified?.InnerThing = engine;
        }

        public static AcceptanceReport PlaceGravshipVehicle(Building_GravEngine engine, VehiclePawnWithMap vehicle, bool forced = false)
        {
            if (!ModsConfig.OdysseyActive || GravshipProcessInProgress) return false;

            if (engine is null)
            {
                return "CannotLaunchNoEngine".Translate();
            }
            if (vehicle.FullRotation.IsDiagonal && !forced)
            {
                return "VMF_CannotSetDownDiagonal".Translate(vehicle.LabelCap);
            }

            placingGravshipVehicle = true;
            var currentGravship = Current.Game.Gravship;
            try
            {
                var map = vehicle.Map;
                foreach (var c in engine.AllConnectedSubstructure)
                {
                    var report = IsValidCell(c.ToBaseMapCoord(vehicle), map);
                    if (report.Accepted) continue;
                    if (!forced)
                    {
                        return report;
                    }
                    var terrainGrid = vehicle.VehicleMap.terrainGrid;
                    if (terrainGrid.CanRemoveFoundationAt(c))
                    {
                        terrainGrid.RemoveFoundation(c);
                    }
                    c.GetThingList(vehicle.VehicleMap)
                        .SelectMany(t => t.OccupiedRect().Cells)
                        .Distinct()
                        .DoIf(terrainGrid.CanRemoveFoundationAt, intVec3 => terrainGrid.RemoveFoundation(intVec3));
                }
                vehicle.DisembarkAll();

                var rot = vehicle.Rotation;

                var roomStats = vehicle.VehicleMap.regionGrid.AllRooms
                    .Where(r => !r.ExposedToSpace && r.AnyPassable)
                    .Select(r => (r.Cells.FirstOrDefault().ToBaseMapCoord(vehicle), r.Temperature, r.Vacuum)).ToList();

                //MultiFloorsのパッチを発火させるためGenerateGravshipのほぼWrapであるRemoveGravshipFromMapをコール
                var gravship = RemoveGravshipFromMap(null, engine);
                gravship.Rotation = rot;
                var root = gravship.originalPosition.ToBaseMapCoord(vehicle);

                //先にPlaceしないとvehicleがDestroyした瞬間にマップが閉じてしまう可能性がある
                PlaceGravship(null, gravship, root, map);
                //通常はGravshipに潰されてDestroyしているはず
                if (!vehicle.Destroyed)
                {
                    vehicle.Destroy();
                }
                foreach (var r in roomStats)
                {
                    var room = r.Item1.GetRoom(map);
                    if (room is not { ExposedToSpace: false, AnyPassable: true }) continue;
                    room.Temperature = r.Temperature;
                    room.Vacuum = r.Vacuum;
                }
            }
            finally
            {
                Current.Game.Gravship = currentGravship;
                placingGravshipVehicle = false;
            }
            return true;
        }

        public static AcceptanceReport GenerateGravshipVehicle(Building_GravEngine engine, VehicleDef baseDef, bool checkStability = true)
        {
            if (!ModsConfig.OdysseyActive || GravshipProcessInProgress) return false;
            if (engine is null || !engine.Spawned)
            {
                return "CannotLaunchNoEngine".Translate();
            }
            var map = engine.Map;
            var console = engine.GravshipComponents.FirstOrDefault(c => c is CompPilotConsole);
            if (console is null || !console.CanBeActive)
            {
                return "PilotConsoleInaccessible".Translate();
            }
            var rot = console.parent.Rotation;
            var rotCounter = rot.IsHorizontal ? rot.Opposite : rot;

            // wheelsRectを車両に確実に含めるためcheckStabilityがfalseの場合もチェック自体はする
            var stability = CheckGravshipVehicleStability(engine, rot, out var wheelsRect);
            if (checkStability && !stability.Accepted)
            {
                return stability.Reason;
            }

            var cells = engine.ValidSubstructure;
            if (!engine.OccupiedRect().All(cells.Contains))
            {
                return "CannotLaunchNoEngine".Translate();
            }
            if (cells.Any(c => c.TryGetFirstThing<VehiclePawnWithMap>(map, out _)))
            {
                return "VMF_ContainsMapVehicle".Translate();
            }
            Thing thing = null;
            if (cells.Any(c => c.GetThingList(map).Any(t => (thing = t).def.PlaceWorkers?.Any(p => p is PlaceWorker_ForbidOnVehicle) ?? false)))
            {
                return "VMF_ContainsForbidOnVehicle".Translate(thing?.LabelCapNoCount);
            }
            var bounds = CellRect.FromCellList(cells);
            var cellRect = bounds;
            if (wheelsRect != CellRect.Empty)
            {
                cellRect = cellRect.Encapsulate(wheelsRect);
            }
            var outOfBoundsCells = cellRect.Except(cells);
            var pathGrid = map.GetCachedMapComponent<VehiclePathingSystem>()[baseDef].VehiclePathGrid;
            var unwalkableCells = outOfBoundsCells.Where(c => !pathGrid.WalkableFast(c)).ToList();
            if (unwalkableCells.Any())
            {
                unwalkableCells.Do(c => map.debugDrawer.FlashCell(c, 0.5f));
                return "VMF_RectContainsImpassable".Translate();
            }
            var min = cellRect.GetCorner(rot.Opposite);

            VehicleMapProps_Gravship props = new()
            {
                defName = $"GravshipVehicle{engine.GetHashCode()}_",
                baseDef = baseDef,
                size = rot.IsHorizontal ? cellRect.Size.Rotated() : cellRect.Size,
                offset = new Vector3(0f, 0f, 0.25f),
                outOfBoundsCells = [.. cellRect.Except(cells).Select(c => (c - min).RotatedBy(rotCounter).ToIntVec2)]
            };

            var currentGravship = Current.Game.Gravship;
            try
            {
                VMF_Log.DebugMessage($"Create or get VehicleDef: {props.defName}");
                var vehicleDef = DefDatabase<VehicleDef>.GetNamedSilentFail(props.defName);
                vehicleDef ??= GenerateGravshipVehicleDef(props);
                vehicleDef.size = props.size;
                vehicleDef.modExtensions = [props];

                var vehiclePawn = (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(vehicleDef, Faction.OfPlayer);
                if (vehiclePawn?.VehicleMap is null) return false;

                var roomStats = map.regionGrid.AllRooms
                .Where(r => r.Cells.Any(cells.Contains))
                .Where(r => !r.ExposedToSpace && r.AnyPassable)
                .Select(r => (r.Cells.FirstOrDefault(), r.Temperature, r.Vacuum)).ToList();

                //MultiFloorsのパッチを発火させるためGenerateGravshipのほぼWrapであるRemoveGravshipFromMapをコール
                var gravship = RemoveGravshipFromMap(Find.World.GetComponent<WorldComponent_GravshipController>(), engine);
                if (MultiFloors.Active)
                {
                    MultiFloors.RevalidateLaunchSiteState(map);
                }

                map.GetCachedMapComponent<VehiclePathingSystem>().RequestGridsFor(vehiclePawn);
                Thing spawnedVehicle = null;
                try
                {
                    spawnedVehicle = GenSpawn.Spawn(vehiclePawn, cellRect.CenterCell, map, rot);
                }
                catch (Exception ex)
                {
                    VMF_Log.Error($"Error while spawning gravship vehicle.\n{ex.Message}");
                }
                if (spawnedVehicle is null)
                {
                    PlaceGravship(null, gravship, gravship.originalPosition, map);
                }

                gravship.Rotation = rotCounter;
                var minOffset = gravship.originalPosition - min;
                VMF_Log.DebugMessage($"Place gravship to {minOffset.RotatedBy(rotCounter) + IntVec3.NorthEast}");
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    var transform = new TransformData(vehiclePawn.DrawPos, vehiclePawn.FullRotation, vehiclePawn.Transform.rotation);
                    var result = vehiclePawn.VehicleGraphic.ParallelGetPreRenderResults(ref transform, false, vehiclePawn);
                    vehiclePawn.cachedDrawPos = result.position;
                });
                Delay.AfterNSeconds(0, () =>
                {
                    PlaceGravship(null, gravship, minOffset.RotatedBy(rotCounter) + IntVec3.NorthEast, vehiclePawn.VehicleMap);
                    Delay.AfterNSeconds(0, () =>
                    {
                        var compFueledTravel = vehiclePawn.CompFueledTravel;
                        compFueledTravel?.CompTick();
                        vehiclePawn.VehicleMap.mapDrawer.RegenerateLayerNow(typeof(SectionLayer_LightingOnVehicle));
                    });
                });

                var buildRoof = map.areaManager.BuildRoof;
                var buildRoofCells = buildRoof.ActiveCells;
                var buildRoofOnVehicle = vehiclePawn.VehicleMap.areaManager.BuildRoof;
                foreach (var c in buildRoofCells.Intersect(cells))
                {
                    buildRoof[c] = false;
                    buildRoofOnVehicle[(c - min).RotatedBy(rotCounter) + IntVec3.NorthEast] = true;
                }
                foreach (var r in roomStats)
                {
                    var c = (r.Item1 - min).RotatedBy(rotCounter) + IntVec3.NorthEast;
                    var room = c.GetRoom(vehiclePawn.VehicleMap);
                    if (room is { ExposedToSpace: false, AnyPassable: true })
                    {
                        room.Temperature = r.Temperature;
                        room.Vacuum = r.Vacuum;
                    }
                }
            }
            finally
            {
                Current.Game.Gravship = currentGravship;
            }

            return true;
        }

        public static VehicleDef GenerateGravshipVehicleDef(VehicleMapProps_Gravship props)
        {
            if (!ModsConfig.OdysseyActive) return null;

            VMF_Log.DebugMessage($"Generate VehicleDef: {props.defName}");
            var vehicleDef = GenerateInner(props);
            VehicleMod.GenerateImpliedDefs(vehicleDef, false);
            DefGenerator.AddImpliedDef(vehicleDef);
            DefDatabase<ThingDef>.Add(vehicleDef);
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                VehicleTex.CachedTextureIconPaths[vehicleDef] = WorldObjectDefOf.Gravship.expandingIconTexture;
                VehicleTex.CachedTextureIcons[vehicleDef] = WorldObjectDefOf.Gravship.ExpandingIconTexture;
                VehicleFramework.CachedVehicleTextures[(vehicleDef, Rot4.North)] = VehicleTex.VehicleTexture(props.baseDef, Rot4.North, out _);
            });
            return vehicleDef;
        }

        public static AcceptanceReport CheckGravshipVehicleStability(Building_GravEngine engine, Rot4 rot, out CellRect wheelsRect)
        {
            var cells = engine.ValidSubstructure;
            var wheels = engine.GravshipComponents.Select(c => c.parent).OfType<Building_GravshipWheel>()
                .Where(w => w.ValidFor(rot))
                .Where(w =>
                {
                    var wall = GenConstruct.GetWallAttachedTo(w);
                    return wall?.OccupiedRect().Any(cells.Contains) ?? false;
                })
                .Where(w => !w.OccupiedRect().Intersect(cells).Any()).ToList();
            if (wheels.Empty())
            {
                wheelsRect = CellRect.Empty;
                return "VMF_WheelsUnstable".Translate(0);
            }
            var wheelCells = wheels.SelectMany(w => w.OccupiedRect());
            wheelsRect = CellRect.FromCellList(wheelCells);
            var bounds = CellRect.FromCellList(cells);

            var wheelsRect2 = wheelsRect;
            wheelsRect2.ClipInsideRect(bounds);
            // ReSharper disable once InvertIf
            if (wheels.Count < 3 || (float)wheelsRect2.Area / bounds.Area < 0.5f)
            {
                wheelsRect2.Do(c => engine.Map.debugDrawer.FlashCell(c, 0.25f, null, 5));
                return "VMF_WheelsUnstable".Translate(wheels.Count);
            }
            return true;
        }

        private static VehicleDef GenerateInner(VehicleMapProps_Gravship props)
        {
            var def = new VehicleDef();
            foreach (var field in typeof(VehicleDef).GetFields())
            {
                if (!field.IsLiteral) field.SetValue(def, field.GetValue(props.baseDef));
            }

            def.defName = props.defName;
            def.label = "Gravship".Translate();
            def.size = props.size;
            def.graphicData = new GraphicDataRGB();
            def.graphicData.CopyFrom(props.baseDef.graphicData);
            def.graphicData.texPath = "VehicleMapFramework/ClearTex";
            def.graphicData.drawSize = props.size.ToVector2();
            def.modContentPack = VehicleMapFramework.mod.Content;
            def.modExtensions = [props];
            def.shortHash = 0;
            GiveShortHash(def, typeof(ThingDef), takenHashesPerDeftype[typeof(ThingDef)]);
            return def;
        }
    }
}
