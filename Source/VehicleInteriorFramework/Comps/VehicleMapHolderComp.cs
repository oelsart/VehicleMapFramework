using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class VehicleMapHolderComp : WorldObjectComp
    {
        public override IEnumerable<FloatMenuOption> GetTransportPodsFloatMenuOptions(IEnumerable<IThingHolder> pods, CompLaunchable representative)
        {
            IEnumerable<VehiclePawnWithMap> vehicles = null;
            if (this.parent is VehicleCaravan vehicleCaravan)
            {
                vehicles = vehicleCaravan.Vehicles.OfType<VehiclePawnWithMap>();
            }
            else if (this.parent is Caravan caravan)
            {
                vehicles = caravan.pawns?.OfType<VehiclePawnWithMap>();
            }
            else if (this.parent is AerialVehicleInFlight aerial)
            {
                vehicles = aerial.Vehicles.OfType<VehiclePawnWithMap>();
            }
            else if (this.parent is MapParent mapParent)
            {
                if (mapParent.HasMap)
                {
                    vehicles = VehiclePawnWithMapCache.allVehicles[mapParent.Map];
                }
            }

            if (vehicles.NullOrEmpty()) yield break;

            foreach (var vehicle in vehicles)
            {
                var mapParent = vehicle.VehicleMap.Parent;
                var aerial = vehicle.GetAerialVehicle();
                var tile = aerial != null ? WorldHelper.GetNearestTile(aerial.DrawPos) : vehicle.GetRootTile();

                bool CanLandInSpecificCell()
                {
                    if (mapParent == null || !mapParent.HasMap)
                    {
                        return false;
                    }
                    if (mapParent.EnterCooldownBlocksEntering())
                    {
                        return FloatMenuAcceptanceReport.WithFailMessage("MessageEnterCooldownBlocksEntering".Translate(mapParent.EnterCooldownTicksLeft().ToStringTicksToPeriod()));
                    }
                    return true;
                }

                if (!CanLandInSpecificCell())
                {
                    continue;
                }
                yield return new FloatMenuOption("VIF_LandInSpecificMap".Translate(vehicle.VehicleMap.Parent.Label, this.parent.Label), delegate
                {
                    Map myMap = representative.parent.Map;
                    Map map = vehicle.VehicleMap;
                    Current.Game.CurrentMap = map;
                    CameraJumper.TryHideWorld();
                    vehicle.ForceResetCache();
                    Find.Targeter.BeginTargeting(TargetingParameters.ForDropPodsDestination(), delegate (LocalTargetInfo x)
                    {
                        representative.TryLaunch(tile, new TransportPodsArrivalAction_LandInVehicleMap(mapParent, x.Cell, representative.parent.TryGetComp<CompShuttle>() != null));
                    }, null, delegate
                    {
                        if (Find.Maps.Contains(myMap))
                        {
                            Current.Game.CurrentMap = myMap;
                        }
                    }, CompLaunchable.TargeterMouseAttachment);
                });
            }
        }
    }
}
