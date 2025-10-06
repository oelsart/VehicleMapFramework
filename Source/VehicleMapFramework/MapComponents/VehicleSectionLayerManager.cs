using System;
using System.Collections.Generic;
using SmashTools;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework
{
    public class VehicleSectionLayerManager : MapComponent
    {
        private Dictionary<Section, Dictionary<Type, SectionLayer[]>> layersByRot;

        internal static readonly List<Type> OrientedSectionLayerTypes = [.. typeof(SectionLayer_Things).AllSubclassesNonAbstract()];

        public VehicleSectionLayerManager(Map map) : base(map)
        {
            VehicleMapParentsComponent.CachedMapParentVehicle[map] = map.Parent as MapParent_Vehicle;
            if (MultiFloors.Active && VehicleMapParentsComponent.CachedMapParentVehicle[map] is null)
            {
                VehicleMapParentsComponent.CachedMapParentVehicle[map] = MultiFloors.GroundMap(map)?.Parent as MapParent_Vehicle;
            }
        }

        public override void FinalizeInit()
        {
            VehicleMapParentsComponent.CachedMapParentVehicle[map] = map.Parent as MapParent_Vehicle;
            if (MultiFloors.Active && VehicleMapParentsComponent.CachedMapParentVehicle[map] is null)
            {
                VehicleMapParentsComponent.CachedMapParentVehicle[map] = MultiFloors.GroundMap(map)?.Parent as MapParent_Vehicle;
            }

            if (!map.IsVehicleMapOf(out _)) return;

            VMF_Harmony.DynamicPatchAllNow(Level.All);
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                layersByRot = [];

                var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(map);
                component?.cacheMode = true;
                for (var i = 0; i < map.Size.x; i += 17)
                {
                    for (var j = 0; j < map.Size.z; j += 17)
                    {
                        var section = map.mapDrawer.SectionAt(new IntVec3(i, 0, j));
                        layersByRot[section] = [];

                        foreach (var type in OrientedSectionLayerTypes)
                        {
                            var layer = section.GetLayer(type);
                            if (layer != null)
                            {
                                layersByRot[section][type] =
                                [
                                    layer,
                                    (SectionLayer)Activator.CreateInstance(type, section),
                                    (SectionLayer)Activator.CreateInstance(type, section),
                                    (SectionLayer)Activator.CreateInstance(type, section),
                                ];
                                VehicleMapUtility.RotForPrint = Rot4.North;
                                try
                                {
                                    for (var k = 0; k < 4; k++)
                                    {
                                        layersByRot[section][type][k].Regenerate();
                                        layersByRot[section][type][k].RefreshSubMeshBounds();
                                        VehicleMapUtility.RotForPrint = VehicleMapUtility.RotForPrint.Rotated(RotationDirection.Clockwise);
                                    }
                                }
                                finally
                                {
                                    VehicleMapUtility.RotForPrint = Rot4.North;
                                }
                            }
                        }
                    }
                }
                component?.cacheMode = false;
            });
        }

        public SectionLayer GetLayer(Section section, Type type, Rot8 rot)
        {
            return layersByRot[section].TryGetValue(type, out var layers) ?
                layers[rot.RotForVehicleDraw().AsInt] :
                null;
        }

        public void UpdateAllSection()
        {
            if (!map.IsVehicleMapOf(out _))
                return;

            for (var i = 0; i < map.Size.x; i += 17)
            {
                for (var j = 0; j < map.Size.z; j += 17)
                {
                    var section = map.mapDrawer.SectionAt(new IntVec3(i, 0, j));
                    UpdateSection(section);
                }
            }
        }

        private void UpdateSection(Section section)
        {
            if (section.dirtyFlags == 0L)
            {
                return;
            }
            foreach (var sectionLayers in layersByRot[section].Values)
            {
                sectionLayers[0].Dirty = sectionLayers[0].Dirty || (section.dirtyFlags & sectionLayers[0].relevantChangeTypes) != 0;
                if (!sectionLayers[0].Dirty)
                {
                    continue;
                }
                try
                {
                    VehicleMapUtility.RotForPrint = Rot4.North;
                    for (var i = 0; i < 4; i++)
                    {
                        try
                        {
                            sectionLayers[i].Regenerate();
                        }
                        finally
                        {
                            VehicleMapUtility.RotForPrint = VehicleMapUtility.RotForPrint.Rotated(RotationDirection.Clockwise);
                        }
                    }
                    VehicleMapUtility.RotForPrint = Rot4.North;
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not regenerate layer {sectionLayers[0].ToStringSafe()}: {ex}");
                }
                sectionLayers[0].Dirty = false;
            }
        }
    }
}
