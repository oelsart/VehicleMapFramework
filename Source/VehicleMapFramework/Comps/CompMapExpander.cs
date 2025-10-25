using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

[HotSwap]
public class CompMapExpander : ThingComp
{
    private bool validCellsDirty;
    
    private bool? cachedIsBridge;
    
    private bool? cachedIsOnlyBridge;

    private static readonly List<IntVec3> tmpCells = new(8);

    public static bool debugDraw;

    private bool[] ValidCells
    {
        get
        {
            if (validCellsDirty)
            {
                var adjacentCells = GenAdj.AdjacentCellsAround;
                for (var i = 0; i < 8; i++)
                {
                    field[i] = false;
                    var intVec = parent.Position + adjacentCells[i];
                    if (ValidCell(intVec))
                    {
                        field[i] = true;
                    }
                }
            }
            return field;
        }
    } = new bool[8];

    public bool IsOnlyBridge
    {
        get
        {
            if (!IsBridge) return false;
            
            cachedIsOnlyBridge ??= IsOnlyBridgeStatus();
            return cachedIsOnlyBridge.Value;
            
            bool IsOnlyBridgeStatus()
            {
                if (!parent.Spawned) return false;
        
                var validCells = ValidCells;
                tmpCells.Clear();
                for (var i = 0; i < 8; i++)
                {
                    if (validCells[i])
                    {
                        tmpCells.Add(parent.Position + GenAdj.AdjacentCellsAround[i]);
                    }
                }

                var result = true;
                var first = tmpCells.PopFront();
                parent.Map.floodFiller.FloodFill(first, c => ValidCell(c) && c != parent.Position, c =>
                {
                    if (tmpCells.Contains(c))
                    {
                        tmpCells.Remove(c);
                        if (tmpCells.Empty())
                        {
                            result = false;
                            return true;
                        }
                    }
                    return false;
                });
                return result;
            }
        }
    }

    public bool IsBridge
    {
        get
        {
            cachedIsBridge ??= IsBridgeStatus();
            return cachedIsBridge.Value;
            
            bool IsBridgeStatus()
            {
                if (!parent.Spawned) return false;
        
                var validCells = ValidCells;
                var validState = validCells[^1];
                var firstBlockFound = false;
                for (var i = 0; i < 8; i++)
                {
                    if (validCells[i])
                    {
                        if (!validState)
                        {
                            if (firstBlockFound)
                            {
                                return true;
                            }
                            firstBlockFound = true;
                            validState = true;
                        }
                    }
                    else if (validState)
                    {
                        validState = false;
                    }
                }
                return false;
            }
        }
    }
    
    private bool ValidCell(IntVec3 c) => c.InBounds(parent.Map) && c.GetEdifice(parent.Map) is not VehicleStructure;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        if (parent.IsOnVehicleMapOf(out var vehicle))
        {
            vehicle.MapExpanderComps.Add(this);
            foreach (var intVec in parent.OccupiedRect())
            {
                if (intVec.GetEdifice(parent.Map) is not VehicleStructure structure)
                    continue;
                Thing.allowDestroyNonDestroyable = true;
                structure.Destroy();
                Thing.allowDestroyNonDestroyable = false;
            }
            DirtySelfAndAdjacentComps(parent.Map);
            ResizeVehicle(vehicle);
        }
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        var occupiedRect = parent.OccupiedRect();
        foreach (var thingList in occupiedRect.Select(intVec => map.thingGrid.ThingsListAtFast(intVec)))
        {
            for (var i = thingList.Count - 1; i >= 0; i--)
            {
                var thing = thingList[i];
                if (thing is Pawn) continue;
                
                if (thing.def.Minifiable)
                {
                    thing.Uninstall();
                }
                else
                {
                    thing.Destroy(DestroyMode.Deconstruct);
                }
            }
        }
        
        if (map.IsVehicleMapOf(out var vehicle))
        {
            vehicle.MapExpanderComps.Remove(this);
            foreach (var intVec in occupiedRect)
            {
                GenSpawn.Spawn(VMF_DefOf.VMF_VehicleStructureEmpty, intVec, map, WipeMode.VanishOrMoveAside);
            }

            if (IsBridge)
            {
                vehicle.MapExpanderComps.ForEach(c => c.cachedIsOnlyBridge = null);
            }
            DirtySelfAndAdjacentComps(map);
            ResizeVehicle(vehicle);
        }
    }

    private void DirtySelfAndAdjacentComps(Map map)
    {
        validCellsDirty = true;
        cachedIsBridge = null;
        cachedIsOnlyBridge = null;
        foreach (var intVec in GenAdj.CellsAdjacent8Way(parent).Where(c => c.InBounds(map)))
        {
            foreach (var thing in map.thingGrid.ThingsListAtFast(intVec))
            {
                if (!thing.TryGetComp<CompMapExpander>(out var comp))
                    continue;
                comp.validCellsDirty = true;
                comp.cachedIsBridge = null;
                comp.cachedIsOnlyBridge = null;
                break;
            }
        }
    }

    private static void ResizeVehicle(VehiclePawnWithMap vehicle)
    {
        if (!UnityData.IsInMainThread)
        {
            LongEventHandler.ExecuteWhenFinished(() => ResizeVehicle(vehicle));
            return;
        }
        var curSize = vehicle.def.size;
        var mapRect = CellRect.WholeMap(vehicle.VehicleMap);
        var newRect = CellRect.FromCellList(mapRect.Except(vehicle.CachedStructureCells));
        var newSize = newRect.Size;
        if (curSize != newSize)
        {
            vehicle.def.size = newSize;
            var offset = mapRect.CenterVector3 - newRect.CenterVector3;
            var data = vehicle.VehicleGraphic.DataRgb;
            var prevOffset = data.drawOffset;
            data.drawOffset = offset;
            data.drawOffsetNorth = offset;
            data.drawOffsetEast = offset.RotatedBy(Rot4.East);
            data.drawOffsetSouth = offset.RotatedBy(Rot4.South);
            data.drawOffsetWest = offset.RotatedBy(Rot4.West);
            if (vehicle.Spawned)
            {
                var diff = prevOffset - offset;
                vehicle.Position += new IntVec3(
                    (int)MathF.Truncate(diff.x),
                    0,
                    (int)MathF.Truncate(diff.z)).RotatedBy(vehicle.Rotation);
                var opp = Convert.ToInt32(vehicle.Rotation.AsInt > 1);
                if ((diff.x < 0f) == (newSize.x % 2 == opp))
                {
                    vehicle.Position += (IntVec3.East * (int)(diff.x % 1f * 2f)).RotatedBy(vehicle.Rotation);
                }
                if ((diff.z < 0f) == (newSize.z % 2 == opp))
                {
                    vehicle.Position += (IntVec3.North * (int)(diff.z % 1f * 2f)).RotatedBy(vehicle.Rotation);
                }
                
                vehicle.DrawTracker.tweener.ResetTweenedPosToRoot();
                vehicle.Map.GetCachedMapComponent<VehiclePathingSystem>().RequestGridsFor(vehicle);
                var def = vehicle.VehicleDef;
                def.components?.ForEach(component =>
                {
                    component.hitbox.Hitbox.Clear();
                    component.hitbox.Initialize(def);
                });
                if (!vehicle.vehiclePather.Moving)
                {
                    vehicle.vehiclePather.nextCell = vehicle.Position;
                }
            }
        }
    }

    public static void DebugDraw(List<CompMapExpander> comps)
    {
        if (!debugDraw || !VehicleMapUtility.FocusedOnVehicleMap(out var vehicle))
            return;
        var quat = vehicle.FullAngleQuat();
        foreach (var comp in comps)
        {
            if (!comp.IsBridge)
                continue;
            var mat = DebugMatsSpectrum.Mat(comp.IsOnlyBridge ? 10 : 30, true);
            var vector = comp.parent.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays).ToBaseMapCoord();
            Graphics.DrawMesh(MeshPool.plane10, vector, quat, mat, 0);
        }
    }
}
