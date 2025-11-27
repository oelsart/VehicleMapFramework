using System;
using System.Collections.Generic;
using RimWorld;
using SmashTools;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework
{
    public class VehicleSectionLayerManager : MapComponent
    {
        private Dictionary<Section, Dictionary<Type, SectionLayer[]>> layersByRot;
        
        internal static readonly List<Type> OrientedSectionLayerTypes =
            [.. typeof(SectionLayer_Things).AllSubclassesNonAbstract().Concat(typeof(SectionLayer_SunShadowsOnVehicle))];

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
                            if (layer == null) continue;
                            
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
                                    var layer2 = layersByRot[section][type][k];
                                    layer2.Regenerate();
                                    layer2.RefreshSubMeshBounds();
                                    VehicleMapUtility.RotForPrint = VehicleMapUtility.RotForPrint.Rotated(RotationDirection.Clockwise);
                                    DirtyAdaptiveStorageGraphics(layer2, section);
                                }
                            }
                            finally
                            {
                                VehicleMapUtility.RotForPrint = Rot4.North;
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
                    
                    // LayerSubMeshを直接FinalizeしているためY圧縮をかける
                    var edgeShadowsLayer = section.GetLayer(typeof(SectionLayer_EdgeShadows));
                    if (edgeShadowsLayer != null)
                    {
                        FinalizeShadowVerts(edgeShadowsLayer);
                    }
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
                    try
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            sectionLayers[i].Regenerate();
                            VehicleMapUtility.RotForPrint =
                                VehicleMapUtility.RotForPrint.Rotated(RotationDirection.Clockwise);
                            DirtyAdaptiveStorageGraphics(sectionLayers[i], section);
                        }
                    }
                    finally
                    {
                        VehicleMapUtility.RotForPrint = Rot4.North;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not regenerate layer {sectionLayers[0].ToStringSafe()}: {ex}");
                }
                sectionLayers[0].Dirty = false;
            }
        }

        /// <summary>
        /// Fixes the shadows layer by compressing the y values.
        /// </summary>
        /// <param name="layer"></param>
        public static void FinalizeShadowVerts(SectionLayer layer)
        {
            var subMesh = layer.subMeshes.FirstOrDefault(subMesh => subMesh.finalized);
            if (subMesh is null) return;
            for (var i = 0; i < subMesh.verts.Count; i++)
            {
                var vert = subMesh.verts[i];
                vert.y /= VehicleMapUtility.YCompress;
                subMesh.verts[i] = vert;
            }
            subMesh.mesh.SetVertices(subMesh.verts);
        }

        private void DirtyAdaptiveStorageGraphics(SectionLayer layer, Section section)
        {
            if (AdaptiveStorage.Active && layer is SectionLayer_ThingsGeneral)
            {
                foreach (var intVec in section.CellRect)
                {
                    var list = map.thingGrid.ThingsListAt(intVec);
                    var count = list.Count;
                    for (var i = 0; i < count; i++)
                    {
                        var thing = list[i];
                        if (thing.def.thingClass.SameOrSubclassOf(AdaptiveStorage.ThingClass) &&
                            (thing.def.seeThroughFog || !map.fogGrid.IsFogged(thing.Position)) &&
                            thing.def.drawerType != DrawerType.None &&
                            thing.def.drawerType != DrawerType.RealtimeOnly &&
                            (thing.def.hideAtSnowOrSandDepth >= 1f ||
                             Math.Max(map.snowGrid.GetDepth(thing.Position), thing.Position.GetSandDepth(map)) <=
                             thing.def.hideAtSnowOrSandDepth) &&
                            (thing.def.plant == null || thing.def.plant.showInFrozenWater ||
                             thing.Position.GetTerrain(map) != TerrainDefOf.ThinIce) && thing.Position.x == intVec.x &&
                            thing.Position.z == intVec.z &&
                            AdaptiveStorage.Renderer(thing) is { } renderer)
                        {
                            AdaptiveStorage.SetAllPrintDatasDirty(renderer);
                        }
                    }
                }
            }
        }
    }
}
