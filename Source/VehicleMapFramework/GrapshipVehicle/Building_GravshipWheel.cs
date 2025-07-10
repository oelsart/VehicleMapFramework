using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework
{
    public class Building_GravshipWheel : Building
    {
        public override Vector3 DrawPos => base.DrawPos;

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;

            if (!this.IsOnVehicleMapOf(out _))
            {
                yield return new Command_Action()
                {
                    defaultLabel = "VMF_JackUp".Translate(),
                    action = GenerateGravshipVehicle
                };
            }
        }

        public override void Print(SectionLayer layer)
        {
            base.Print(layer);
        }

        public void GenerateGravshipVehicle()
        {
            //if (!ModsConfig.OdysseyActive) return;

            bool ExistSubstructure(IntVec3 x)
            {
                if (x.InBounds(Map))
                {
                    TerrainDef terrainDef = Map.terrainGrid.FoundationAt(x);
                    return terrainDef != null;// && terrainDef.IsSubstructure;
                }
                return false;
            }

            var firstCell = Position + Rotation.RighthandCell;
            if (!ExistSubstructure(firstCell))
            {
                firstCell = Position - Rotation.RighthandCell;
                if (!ExistSubstructure(firstCell)) return;
            }

            HashSet<IntVec3> cells = [];
            Map.floodFiller.FloodFill(firstCell, x =>
            {
                return x.TryGetFirstThing<Building_GravshipWheel>(Map, out _) || ExistSubstructure(x);
            }, x =>
            {
                cells.Add(x);
            });

            var cellRect = CellRect.FromCellList(cells);
            var min = cellRect.Min;
            VehicleMapProps_Gravship props = new()
            {
                size = cellRect.Size,
                outOfBoundsCells = [.. cellRect.Cells.Where(c => !cells.Contains(c)).Select(c => (c - min).ToIntVec2)]
            };
            var vehicleDef = GravshipVehicleUtility.GenerateGravshipVehicleDef(props);
            var vehiclePawn = (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(vehicleDef, Faction.OfPlayer);
            if (vehiclePawn.VehicleMap is null) return;
            //var gravship = GravshipUtility.GenerateGravship(engine);

            var map = Map;
            foreach (var c in cells)
            {
                var pos = c - min + IntVec3.NorthEast;
                var things = c.GetThingList(map);
                for (var i = 0; i < things.Count; i++)
                {
                    var thing = things[i];
                    thing.DeSpawn(DestroyMode.WillReplace);
                    GenSpawn.Spawn(thing, pos, vehiclePawn.VehicleMap);
                }
                var terrainDef = map.terrainGrid.FoundationAt(c);
                if (terrainDef != null)
                {
                    map.terrainGrid.RemoveFoundation(c);
                    vehiclePawn.VehicleMap.terrainGrid.SetFoundation(pos, terrainDef);
                }
            }
            GenSpawn.Spawn(vehiclePawn, cellRect.CenterCell, map);
            //GravshipPlacementUtility.PlaceGravshipInMap(gravship, vehiclePawnWithMap.VehicleMap.Center, vehiclePawnWithMap.VehicleMap, out _);
        }
    }
}
