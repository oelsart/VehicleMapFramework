using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework
{
    public static class GravshipVehicleUtility
    {
        public static bool placingGravshipVehicle;

        private static Func<IntVec3, Map, AcceptanceReport> IsValidCell = (Func<IntVec3, Map, AcceptanceReport>)AccessTools.Method(typeof(Designator_MoveGravship), "IsValidCell").CreateDelegate(typeof(Func<IntVec3, Map, AcceptanceReport>));

        private static Action<Def, Type, HashSet<ushort>> GiveShortHash = (Action<Def, Type, HashSet<ushort>>)AccessTools.Method(typeof(ShortHashGiver), "GiveShortHash").CreateDelegate(typeof(Action<Def, Type, HashSet<ushort>>));

        private static Dictionary<Type, HashSet<ushort>> takenHashesPerDeftype = AccessTools.StaticFieldRefAccess<Dictionary<Type, HashSet<ushort>>>(typeof(ShortHashGiver), "takenHashesPerDeftype");

        public static bool GravshipProcessInProgress => GravshipUtility.generatingGravship || GravshipPlacementUtility.placingGravship || placingGravshipVehicle;

        public static void PlaceGravshipVehicleUnSpawned(Building_GravEngine engine, IntVec3 loc, Rot4 rot, VehiclePawnWithMap vehicle, bool forced = false)
        {
            if (!ModsConfig.OdysseyActive || GravshipProcessInProgress) return;

            var spawned = engine.Spawned;
            var destroyed = engine.Destroyed;
            var minified = engine.SpawnedParentOrMe as MinifiedThing;
            if (!spawned)
            {
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
                return "VMF_NoConnectedEngine".Translate();
            }
            if (vehicle.FullRotation.IsDiagonal && !forced)
            {
                return "VMF_VehicleDiagonal".Translate();
            }

            placingGravshipVehicle = true;
            var curretGravship = Current.Game.Gravship;
            try
            {
                foreach (var c in engine.AllConnectedSubstructure)
                {
                    var report = IsValidCell(c.ToBaseMapCoord(vehicle), vehicle.Map);
                    if (!report.Accepted)
                    {
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
                            .DoIf(terrainGrid.CanRemoveFoundationAt, c => terrainGrid.RemoveFoundation(c));
                    }
                }
                vehicle.DisembarkAll();

                var map = vehicle.Map;
                var rot = vehicle.Rotation;

                var roomStats = vehicle.VehicleMap.regionGrid.AllRooms
                    .Where(r => !r.ExposedToSpace && r.AnyPassable)
                    .Select(r => (r.Cells.FirstOrDefault().ToBaseMapCoord(vehicle), r.Temperature, r.Vacuum)).ToList();
                var gravship = GravshipUtility.GenerateGravship(engine);
                gravship.Rotation = rot;
                var root = gravship.originalPosition.ToBaseMapCoord(vehicle);

                //先にPlaceしないとvehicleがDestroyした瞬間にマップが閉じてしまう可能性がある
                GravshipPlacementUtility.PlaceGravshipInMap(gravship, root, map, out _);
                //通常はGravshipに潰されてDestroyしているはず
                if (!vehicle.Destroyed)
                {
                    vehicle.Destroy();
                }
                foreach (var r in roomStats)
                {
                    var room = r.Item1.GetRoom(map);
                    if (room != null && !room.ExposedToSpace && room.AnyPassable)
                    {
                        room.Temperature = r.Temperature;
                        room.Vacuum = r.Vacuum;
                    }
                }
                if (forced)
                {
                    FleckMaker.ThrowDustPuff(gravship.originalPosition, map, Mathf.Max(vehicle.def.size.x, vehicle.def.size.z) + 2f);
                }
            }
            finally
            {
                Current.Game.Gravship = curretGravship;
                placingGravshipVehicle = false;
            }
            return true;
        }

        public static AcceptanceReport GenerateGravshipVehicle(Building_GravEngine engine)
        {
            if (!ModsConfig.OdysseyActive || GravshipProcessInProgress) return false;
            if (engine is null || !engine.Spawned)
            {
                return "VMF_NoConnectedEngine".Translate();
            }
            var map = engine.Map;
            var console = engine.GravshipComponents.FirstOrDefault(c => c is CompPilotConsole);
            if (console is null)
            {
                return "VMF_NoPilotConsole".Translate();
            }
            var rot = console.parent.Rotation;
            var rotCounter = rot.IsHorizontal ? rot.Opposite : rot;

            var stability = CheckGravshipVehicleStability(engine, rot, out var wheelsRect);
            if (!stability.Accepted)
            {
                return stability.Reason;
            }

            var cells = engine.ValidSubstructure;
            var bounds = CellRect.FromCellList(cells);
            var cellRect = bounds.Encapsulate(wheelsRect);
            var outOfBoundsCells = cellRect.Except(cells);
            var pathGrid = ComponentCache.GetCachedMapComponent<VehiclePathingSystem>(map)[VMF_DefOf.VMF_GravshipVehicleBase].VehiclePathGrid;
            var unwalkableCells = outOfBoundsCells.Where(c => !pathGrid.WalkableFast(c)).ToList();
            if (unwalkableCells.Any())
            {
                unwalkableCells.Do(c => map.debugDrawer.FlashCell(c, 0.5f));
                return "VMF_RectContainsImpassable".Translate();
            }
            var min = cellRect.GetCorner(rot.Opposite);

            VehicleMapProps_Gravship props = new()
            {
                engine = engine,
                size = rot.IsHorizontal ? cellRect.Size.Rotated() : cellRect.Size,
                offset = new(0f, 0f, 0.25f),
                outOfBoundsCells = [.. cellRect.Except(cells).Select(c => (c - min).RotatedBy(rotCounter).ToIntVec2)]
            };

            var curretGravship = Current.Game.Gravship;
            try
            {
                VMF_Log.Debug($"Create or get VehicleDef: {props.DefName}");
                var vehicleDef = DefDatabase<VehicleDef>.GetNamedSilentFail(props.DefName);
                vehicleDef ??= GenerateGravshipVehicleDef(props);
                vehicleDef.size = props.size;
                vehicleDef.modExtensions = [props];

                var vehiclePawn = (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(vehicleDef, Faction.OfPlayer);
                if (vehiclePawn?.VehicleMap is null) return false;

                var roomStats = map.regionGrid.AllRooms
                .Where(r => r.Cells.Any(cells.Contains))
                .Where(r => !r.ExposedToSpace && r.AnyPassable)
                .Select(r => (r.Cells.FirstOrDefault(), r.Temperature, r.Vacuum)).ToList();
                var gravship = GravshipUtility.GenerateGravship(engine);

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
                    GravshipPlacementUtility.PlaceGravshipInMap(gravship, gravship.originalPosition, map, out _);
                }

                gravship.Rotation = rotCounter;
                var minOffset = gravship.originalPosition - min;
                VMF_Log.Debug($"Place gravship to {minOffset.RotatedBy(rotCounter) + IntVec3.NorthEast}");
                GravshipPlacementUtility.PlaceGravshipInMap(gravship, minOffset.RotatedBy(rotCounter) + IntVec3.NorthEast, vehiclePawn.VehicleMap, out _);

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
                    if (room != null && !room.ExposedToSpace && room.AnyPassable)
                    {
                        room.Temperature = r.Temperature;
                        room.Vacuum = r.Vacuum;
                    }
                }
            }
            finally
            {
                Current.Game.Gravship = curretGravship;
            }

            return true;
        }

        public static VehicleDef GenerateGravshipVehicleDef(VehicleMapProps_Gravship props)
        {
            if (!ModsConfig.OdysseyActive) return null;

            VMF_Log.Debug($"Generate VehicleDef: {props.DefName}");
            var vehicleDef = GenerateInner(props);
            VehicleMod.GenerateImpliedDefs(vehicleDef, false);
            DefGenerator.AddImpliedDef(vehicleDef);
            DefDatabase<ThingDef>.Add(vehicleDef);
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                VehicleTex.CachedTextureIconPaths[vehicleDef] = WorldObjectDefOf.Gravship.expandingIconTexture;
                VehicleTex.CachedTextureIcons[vehicleDef] = WorldObjectDefOf.Gravship.ExpandingIconTexture;
                AccessTools.StaticFieldRefAccess<Dictionary<(VehicleDef, Rot4), Texture2D>>(typeof(VehicleTex), "CachedVehicleTextures")[(vehicleDef, Rot4.North)]
                = VehicleTex.VehicleTexture(VMF_DefOf.VMF_GravshipVehicleBase, Rot4.North, out _);
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
            var wheelCells = wheels.SelectMany(w => w.OccupiedRect());
            wheelsRect = CellRect.FromCellList(wheelCells);
            var bounds = CellRect.FromCellList(cells);

            var wheelsRect2 = wheelsRect;
            wheelsRect2.ClipInsideRect(bounds);
            if (wheels.Count < 3 || (float)wheelsRect2.Area / bounds.Area < 0.5f)
            {
                wheelsRect2.Do(c => engine.Map.debugDrawer.FlashCell(c, 0.25f, null, 5));
                return "VMF_WheelsUnstable".Translate();
            }
            return true;
        }

        private static VehicleDef GenerateInner(VehicleMapProps_Gravship props)
        {
            var def = new VehicleDef();
            var baseDef = VMF_DefOf.VMF_GravshipVehicleBase;
            foreach (var field in typeof(VehicleDef).GetFields())
            {
                if (!field.IsLiteral) field.SetValue(def, field.GetValue(baseDef));
            }

            def.defName = props.DefName;
            def.label = "Gravship".Translate();
            def.size = props.size;
            def.graphicData = new GraphicDataRGB();
            def.graphicData.CopyFrom(baseDef.graphicData);
            def.graphicData.drawSize = props.size.ToVector2();
            def.modContentPack = VehicleMapFramework.mod.Content;
            def.modExtensions = [props];
            def.shortHash = 0;
            GiveShortHash(def, typeof(ThingDef), takenHashesPerDeftype[typeof(ThingDef)]);
            return def;
        }
    }
}
