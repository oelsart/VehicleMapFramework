using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using SmashTools;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework
{
    public class VehicleSectionLayerManager(Map map) : MapComponent(map)
    {
        private Dictionary<Section, Dictionary<Type, SectionLayer[]>> layersByRot;

        private Rot4 lastGeneratedRots = Rot4.North;
        
        internal static readonly List<Type> OrientedSectionLayerTypes =
            [.. typeof(SectionLayer_Things).AllSubclassesNonAbstract().Append(typeof(SectionLayer_SunShadowsOnVehicle))];
        
        [UsedImplicitly] // Reflection access by Naname Walls
        public static Rot4 RotForPrintCounter => RotForPrint.IsHorizontal ? RotForPrint.Opposite : RotForPrint;

        public static Rot4 RotForPrint { get; set; }
        
        public static bool CacheMode { get; set; }
        
        public override void FinalizeInit()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (!map.IsVehicleMap) return;
                VMF_Harmony.DynamicPatchAllNow(Level.All);
                
                layersByRot = [];

                for (var i = 0; i < map.Size.x; i += 17)
                {
                    for (var j = 0; j < map.Size.z; j += 17)
                    {
                        var section = map.mapDrawer.SectionAt(new IntVec3(i, 0, j));
                        layersByRot[section] = [];

                        foreach (var type in typeof(SectionLayer).AllSubclassesNonAbstract())
                        {
                            var layer = section.GetLayer(type);
                            if (layer == null) continue;

                            if (OrientedSectionLayerTypes.Contains(type))
                            {
                                layersByRot[section][type] =
                                [
                                    layer,
                                    (SectionLayer)Activator.CreateInstance(type, section),
                                    (SectionLayer)Activator.CreateInstance(type, section),
                                    (SectionLayer)Activator.CreateInstance(type, section),
                                ];
                                for (var k = 0; k < 4; k++)
                                {
                                    var layer2 = layersByRot[section][type][k];
                                    layer2.Dirty = true;
                                }
                            }
                            else
                            {
                                layersByRot[section][type] = [layer];
                            }
                        }
                        
                    }
                }
            });
        }

        public SectionLayer GetLayer(Section section, Type type, Rot8 rot)
        {
            if (!layersByRot[section].TryGetValue(type, out var layers))
                return null;
            if (!OrientedSectionLayerTypes.Contains(type))
                return layers[0];
            
            var rot2 = rot.RotForVehicleDraw();
            var layer = layers[rot2.AsInt];
            if (layer.Dirty)
            {
                try
                {
                    CacheMode = true;
                    RotForPrint = rot2;
                    DirtyAdaptiveStorageGraphics(section, rot2);
                    layer.Regenerate();
                    layer.RefreshSubMeshBounds();
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not regenerate layer {layer.ToStringSafe()}: {ex}");
                }
                finally
                {
                    CacheMode = false;
                    RotForPrint = Rot4.North;
                    layer.Dirty = false;
                }
            }

            return layer;
        }

        public void UpdateAllSection()
        {
            foreach (var section in VehiclePawnWithMap.sections(map.mapDrawer))
            {
                UpdateSection(section);
            
                // LayerSubMeshを直接FinalizeしているためY圧縮をかける
                if ((section.dirtyFlags & MapMeshFlagDefOf.Buildings) > 0UL)
                {
                    var edgeShadowsLayer = GetLayer(section, typeof(SectionLayer_EdgeShadows), default);
                    FrameDelay.DelayOne(static layer => FinalizeShadowVerts(layer), edgeShadowsLayer);
                }
            }
        }

        private void UpdateSection(Section section)
        {
            if (section.dirtyFlags == 0L)
            {
                return;
            }
            foreach (var sectionLayers in layersByRot[section])
            {
                if (!OrientedSectionLayerTypes.Contains(sectionLayers.Key)) continue;
                
                var northLayer = sectionLayers.Value[0];
                northLayer.Dirty = northLayer.Dirty || (section.dirtyFlags & northLayer.relevantChangeTypes) > 0UL;
                if (!northLayer.Dirty) continue;
                
                // 北向きレイヤーはベースゲームのメソッドにより必ずRegenerateされるため先にやっておく
                DirtyAdaptiveStorageGraphics(section, Rot4.North);
                for (var i = 1; i < 4; i++)
                {
                    sectionLayers.Value[i].Dirty = true;
                }
            }
        }

        private static void TryRegenerate(SectionLayer layer, Rot4 rot)
        {
            try
            {
                CacheMode = true;
                RotForPrint = rot;
                layer.Regenerate();
                layer.RefreshSubMeshBounds();
            }
            catch (Exception ex)
            {
                Log.Error($"Could not regenerate layer {layer.ToStringSafe()}: {ex}");
            }
            finally
            {
                CacheMode = false;
                RotForPrint = Rot4.North;
                layer.Dirty = false;
            }
        }

        private void DirtyAdaptiveStorageGraphics(Section section, Rot4 rot)
        {
            if (!AdaptiveStorage.Active || rot == lastGeneratedRots) return;
            
            lastGeneratedRots = rot;
            foreach (var intVec in section.CellRect)
            {
                var list = map.thingGrid.ThingsListAt(intVec);
                var count = list.Count;
                for (var i = 0; i < count; i++)
                {
                    var thing = list[i];
                    if (AdaptiveStorage.IsAdaptiveStorageClass(thing.def.thingClass) &&
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
    }
}
