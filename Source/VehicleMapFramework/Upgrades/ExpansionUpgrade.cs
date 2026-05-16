using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class ExpansionUpgrade : Upgrade
{
    public Dictionary<VehicleDef, VehicleDef> defTransitions;
    public List<CellRect> expandAreas;
    
    public override bool UnlockOnLoad => false;

    public override void Unlock(VehiclePawn vehicle, bool unlockingPostLoad)
    {
        var from = vehicle.VehicleDef;
        if (!defTransitions.TryGetValue(vehicle.VehicleDef, out var to))
            return;
        vehicle.def = to;
        
        if (vehicle is not VehiclePawnWithMap vehiclePawnWithMap)
            return;
        foreach (var area in expandAreas)
        {
            foreach (var c in area.MovedBy(IntVec2.One))
                vehiclePawnWithMap.VehicleMap.terrainGrid.SetTerrain(c, VMF_DefOf.VMF_VehicleFloor);
        }
        Finalize(vehiclePawnWithMap, from, to);
    }

    public override void Refund(VehiclePawn vehicle)
    {
        var from = vehicle.VehicleDef;
        if (defTransitions.FirstOrDefault(pair => pair.Value == vehicle.VehicleDef).Key is not { } to)
            return;
        vehicle.def = to;
        
        if (vehicle is not VehiclePawnWithMap vehiclePawnWithMap)
            return;
        foreach (var area in expandAreas)
        {
            var cellRect = area.MovedBy(IntVec2.One);
            foreach (var c in cellRect)
                vehiclePawnWithMap.VehicleMap.terrainGrid.SetTerrain(c, VMF_DefOf.VMF_ImpassableFloor);
            try
            {
                Thing.allowDestroyNonDestroyable = true;
                foreach (var c in cellRect)
                {
                    var thingList = vehiclePawnWithMap.VehicleMap.thingGrid.ThingsListAtFast(c);
                    for (var i = thingList.Count - 1; i >= 0; i--)
                    {
                        var thing = thingList[i];
                        if (thing is Pawn pawn)
                            pawn.pather.TryRecoverFromUnwalkablePosition(false);
                        else
                            thing.Destroy(DestroyMode.Deconstruct);
                    }
                }
            }
            finally
            {
                Thing.allowDestroyNonDestroyable = false;
            }
        }
        Finalize(vehiclePawnWithMap, from, to);
    }

    private void Finalize(VehiclePawnWithMap vehicle, VehicleDef from, VehicleDef to)
    {
        vehicle.impassableCellsDirty = true;
        if (vehicle.Spawned)
        {
            var rot = vehicle.Rotation;
            VehicleResizeUtility.Reposition(vehicle, from.graphicData.DrawOffsetForRot(rot) - to.graphicData.DrawOffsetForRot(rot));
            vehicle.ResetGraphic();
            var overlayRenderer = vehicle.DrawTracker.overlayRenderer;
            foreach (var overlay in overlayRenderer.Overlays)
            {
                overlayRenderer.AllOverlaysListForReading.Remove(overlay);
                vehicle.DrawTracker.RemoveRenderer(overlay);
                overlay.Destroy();
            }
            overlayRenderer.Init();
            VehicleResizeUtility.RefreshVehiclePather(vehicle);

            var components = vehicle.statHandler.components.ToList();
            vehicle.statHandler.InitializeComponents();
            foreach (var component in vehicle.statHandler.components)
            {
                foreach (var component2 in components)
                {
                    if (component.props.key == component2.props.key)
                        component.SetHealth(component2.Health);
                }
            }

            FrameDelay.DelayOne(_vehicle =>
            {
                var pos = _vehicle.Position;
                var _rot = _vehicle.Rotation;
                var map = _vehicle.Map;
                var selected = Find.Selector.IsSelected(_vehicle);
                _vehicle.DeSpawnWithoutJobClearVehicle(DestroyMode.WillReplace);
                GenSpawn.Spawn(_vehicle, pos, map, _rot);
                if (selected)
                    Find.Selector.Select(_vehicle, false, false);
            }, vehicle);
        }
    }
}
