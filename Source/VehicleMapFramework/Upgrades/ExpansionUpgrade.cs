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
        VehicleResizeUtility.PreResize(vehicle);
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
        VehicleResizeUtility.PreResize(vehicle);
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
                            thing.Destroy();
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
        if (vehicle.Spawned)
        {
            vehicle.ResetGraphic();
            var overlayRenderer = vehicle.DrawTracker.overlayRenderer;
            foreach (var overlay in overlayRenderer.Overlays)
            {
                overlayRenderer.AllOverlaysListForReading.Remove(overlay);
                vehicle.DrawTracker.RemoveRenderer(overlay);
                overlay.Destroy();
            }
            overlayRenderer.Init();

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

            var pos = vehicle.Position;
            VehicleResizeUtility.Reposition(ref pos, vehicle,
              from.graphicData.DrawOffsetForRot(Rot4.North) - to.graphicData.DrawOffsetForRot(Rot4.North));
            FrameDelay.DelayOne(state =>
            {
              VehicleResizeUtility.Respawn(state.vehicle, state.pos);
            }, (vehicle, pos));
        }
    }
}
