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
            var min = cellRect.GetCorner(rot.Opposite) + gravship.originalPosition;

            VehicleMapProps_Gravship props = new()
            {
                engine = engine,
                size = rot.IsHorizontal ? cellRect.Size.Rotated() : cellRect.Size,
                offset = new(0f, 0f, 0.25f),
                outOfBoundsCells = [.. cellRect.Cells.Where(c => !cells.Contains(c)).Select(c => (c - min).RotatedBy(rot).ToIntVec2)]
            };
            VMF_Log.Debug($"Create or get VehicleDef: {props.DefName}");
            var vehicleDef = DefDatabase<VehicleDef>.GetNamedSilentFail(props.DefName);
            vehicleDef ??= GenerateGravshipVehicleDef(props);

            var vehiclePawn = (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(vehicleDef, Faction.OfPlayer);
            if (vehiclePawn?.VehicleMap is null) return false;

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
            VMF_Log.Debug($"Place gravship to {(gravship.originalPosition - min).RotatedBy(rotCounter) + IntVec3.NorthEast}");
            GravshipPlacementUtility.PlaceGravshipInMap(gravship, (gravship.originalPosition - min).RotatedBy(rotCounter) + IntVec3.NorthEast, vehiclePawn.VehicleMap, out _);

            foreach (var wheel in wheels)
            {
                Thing thing = wheel.Key;
                if (!thing.Destroyed)
                {
                    GenSpawn.Spawn(thing, (wheel.Value.position - min).RotatedBy(rotCounter) + IntVec3.NorthEast, vehiclePawn.VehicleMap, Rot4.North);
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
