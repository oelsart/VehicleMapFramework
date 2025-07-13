using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleMapFramework
{
    public static class GravshipVehicleUtility
    {
        private static Func<IntVec3, Map, AcceptanceReport> IsValidCell = (Func<IntVec3, Map, AcceptanceReport>)AccessTools.Method(typeof(Designator_MoveGravship), "IsValidCell").CreateDelegate(typeof(Func<IntVec3, Map, AcceptanceReport>));

        public static AcceptanceReport PlaceGravshipVehicle(Building_GravEngine engine, VehiclePawnWithMap vehicle, bool forced = false)
        {
            if (!ModsConfig.OdysseyActive) return false;

            if (engine is null)
            {
                return "VMF_NoConnectedEngine".Translate();
            }
            if (vehicle.FullRotation.IsDiagonal && !forced)
            {
                return "VMF_VehicleDiagonal".Translate();
            }

            var wheels = engine.GravshipComponents.OfType<CompGravshipWheel>()
                .Where(w => w.parent.Rotation == Rot4.North)
                .Where(w => w.AdjacentCells.Any(engine.ValidSubstructure.Contains) && !w.parent.OccupiedRect().Any(engine.ValidSubstructure.Contains))
                .ToDictionary(c => c.parent, c => new PositionData(c.parent.Position.ToBaseMapCoord(vehicle), Rot4.North));
            var substructureCells = engine.ValidSubstructure;
            foreach (var c in wheels.SelectMany(w => w.Key.OccupiedRect().Cells).Union(substructureCells))
            {
                var report = IsValidCell(c.ToBaseMapCoord(vehicle), vehicle.Map);
                if (!report.Accepted)
                {
                    if (!forced)
                    {
                        return report;
                    }
                    vehicle.VehicleMap.terrainGrid.RemoveFoundation(c);
                    var wheel = wheels.FirstOrDefault(w => w.Key.OccupiedRect().Contains(c));
                    wheels.Remove(wheel.Key);
                }
            }
            foreach (Thing thing in wheels.Keys)
            {
                thing.PreSwapMap();
                if (thing.Spawned)
                {
                    thing.DeSpawn(DestroyMode.WillReplace);
                }
            }

            var gravship = GravshipUtility.GenerateGravship(engine);
            var rot = vehicle.Rotation;
            gravship.Rotation = rot;
            var map = vehicle.Map;
            var root = gravship.originalPosition.ToBaseMapCoord(vehicle);

            vehicle.Destroy();
            GravshipPlacementUtility.PlaceGravshipInMap(gravship, root, map, out _);
            foreach (var wheel in wheels)
            {
                var thing = wheel.Key;
                if (!thing.Destroyed)
                {
                    var pos = wheel.Value.position;
                    var size = thing.def.Size;
                    //GenAdj.AdjustForRotation(ref pos, ref size, rot);
                    GenSpawn.Spawn(thing, pos, map, rot);
                }
            }
            return true;
        }

        public static AcceptanceReport GenerateGravshipVehicle(Building_GravEngine engine, Map map)
        {
            if (!ModsConfig.OdysseyActive) return false;

            if (engine is null)
            {
                return "VMF_NoConnectedEngine".Translate();
            }
            var console = engine.GravshipComponents.FirstOrDefault(c => c is CompPilotConsole);
            if (console is null)
            {
                return "VMF_NoPilotConsole".Translate();
            }
            var rot = console.parent.Rotation;
            var rotCounter = rot.IsHorizontal ? rot.Opposite : rot;

            var wheels = engine.GravshipComponents.OfType<CompGravshipWheel>()
                .Where(w => w.parent.Rotation == rot)
                .Where(w => w.AdjacentCells.Any(engine.ValidSubstructure.Contains) && !w.parent.OccupiedRect().Any(engine.ValidSubstructure.Contains))
                .ToDictionary(c => c.parent, c => new PositionData(c.parent.Position, Rot4.North));
            var wheelCells = wheels.Keys.SelectMany(w => w.OccupiedRect()).ToList();
            var wheelsRect = CellRect.FromCellList(wheelCells);
            var gravship = GravshipUtility.GenerateGravship(engine);
            var bounds = gravship.Bounds;
            wheelsRect = wheelsRect.MovedBy(-gravship.originalPosition);

            if (wheels.Count < 3 || (float)wheelsRect.ClipInsideRect(bounds).Area / bounds.Area < 0.5f)
            {
                GravshipPlacementUtility.PlaceGravshipInMap(gravship, gravship.originalPosition, map, out _);
                return "VMF_WheelsUnstable".Translate();
            }
            var cellRect = bounds.Encapsulate(wheelsRect);
            var cells = gravship.Foundations.Keys;
            var outOfBoundsCells = cellRect.Except(cells).Select(c => c + gravship.originalPosition).Except(wheelCells).Select(map.cellIndices.CellToIndex);
            var pathGrid = ComponentCache.GetCachedMapComponent<VehiclePathingSystem>(map)[VMF_DefOf.VMF_GravshipVehicleBase].VehiclePathGrid;
            if (!outOfBoundsCells.All(pathGrid.WalkableFast))
            {
                GravshipPlacementUtility.PlaceGravshipInMap(gravship, gravship.originalPosition, map, out _);
                return "VMF_RectContainsImpassable".Translate();
            }

            foreach (Thing thing in wheels.Keys)
            {
                thing.PreSwapMap();
                if (thing.Spawned)
                {
                    thing.DeSpawn(DestroyMode.WillReplace);
                }
            }
            var min = cellRect.GetCorner(rot.Opposite);

            VehicleMapProps_Gravship props = new()
            {
                engine = engine,
                size = rot.IsHorizontal ? cellRect.Size.Rotated() : cellRect.Size,
                offset = new(0f, 0f, 0.25f),
                outOfBoundsCells = [.. cellRect.Except(cells).Select(c => (c - min).RotatedBy(rotCounter).ToIntVec2)]
            };
            VMF_Log.Debug($"Create or get VehicleDef: {props.DefName}");
            var vehicleDef = DefDatabase<VehicleDef>.GetNamedSilentFail(props.DefName);
            vehicleDef ??= GenerateGravshipVehicleDef(props);
            vehicleDef.size = props.size;

            var vehiclePawn = (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(vehicleDef, Faction.OfPlayer);
            if (vehiclePawn?.VehicleMap is null) return false;

            map.GetCachedMapComponent<VehiclePathingSystem>().RequestGridsFor(vehiclePawn);

            var root = cellRect.MovedBy(gravship.originalPosition).CenterCell;
            Thing spawnedVehicle = null;
            try
            {
                spawnedVehicle = GenSpawn.Spawn(vehiclePawn, root, map, rot);
            }
            catch (Exception ex)
            {
                VMF_Log.Error($"Error while spawning gravship vehicle.\n{ex.Message}");
            }
            if (spawnedVehicle is null)
            {
                GravshipPlacementUtility.PlaceGravshipInMap(gravship, gravship.originalPosition, map, out _);
                return false;
            }

            gravship.Rotation = rotCounter;
            var minOffset = min + gravship.originalPosition;
            VMF_Log.Debug($"Place gravship to {(gravship.originalPosition - minOffset).RotatedBy(rotCounter) + IntVec3.NorthEast}");
            GravshipPlacementUtility.PlaceGravshipInMap(gravship, (gravship.originalPosition - minOffset).RotatedBy(rotCounter) + IntVec3.NorthEast, vehiclePawn.VehicleMap, out _);

            foreach (var wheel in wheels)
            {
                Thing thing = wheel.Key;
                if (!thing.Destroyed)
                {
                    GenSpawn.Spawn(thing, (wheel.Value.position - minOffset).RotatedBy(rotCounter) + IntVec3.NorthEast, vehiclePawn.VehicleMap, Rot4.North);
                }
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
            return vehicleDef;
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
            def.shortHash = (ushort)(GenText.StableStringHash(def.defName) % 65535);
            def.graphicData = new GraphicDataRGB();
            def.graphicData.CopyFrom(baseDef.graphicData);
            def.graphicData.drawSize = props.size.ToVector2();
            def.modContentPack = VehicleMapFramework.mod.Content;
            def.modExtensions = [props];
            return def;
        }
    }
}
