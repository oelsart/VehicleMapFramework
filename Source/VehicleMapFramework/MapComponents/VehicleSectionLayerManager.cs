using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleMapFramework
{
  public class VehicleSectionLayerManager(Map map) : MapComponent(map)
  {
    private Dictionary<Section, Dictionary<Type, SectionLayer[]>> layersByRot;

    private Rot4 lastGeneratedRots = Rot4.North;

    internal static readonly HashSet<Type> OrientedSectionLayerTypes =
      [.. typeof(SectionLayer_Things).AllSubclassesNonAbstract().Append(typeof(SectionLayer_SunShadowsOnVehicle))];

    public static readonly List<CompatBase> CompatClassesForDrawLayers = [];

    [UsedImplicitly] // Reflection access by Naname Walls
    public static Rot4 RotForPrintCounter => RotForPrint.IsHorizontal ? RotForPrint.Opposite : RotForPrint;

    public static Rot4 RotForPrint { get; set; }

    public static bool CacheMode { get; set; }

    public override void FinalizeInit()
    {
      LongEventHandler.ExecuteWhenFinished(() =>
      {
        if (!map.IsVehicleMap) return;

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
                layersByRot[section][type] = [layer, layer, layer, layer];
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

      var rot2 = rot.RotForVehicleDraw();
      var layer = layers[rot2.AsInt];
      if (layer.Dirty)
      {
        try
        {
          CacheMode = true;
          RotForPrint = rot2;
          if ((layer.relevantChangeTypes & MapMeshFlagDefOf.Buildings) > 0UL)
          {
            DirtyAdaptiveStorageGraphics(section, rot2);
          }
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
      }
    }

    private void UpdateSection(Section section)
    {
      foreach (var sectionLayers in layersByRot[section])
      {
        var northLayer = sectionLayers.Value[0];
        northLayer.Dirty = (section.dirtyFlags & northLayer.relevantChangeTypes) > 0UL;
        if (!northLayer.Dirty) continue;
        
        for (var i = 1; i < 4; i++)
        {
          sectionLayers.Value[i].Dirty = true;
        }
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
    /// Finalizes the vertices of the given section layer by compressing the y values.
    /// </summary>
    /// <param name="layer"></param>
    public static void FinalizeVerts(SectionLayer layer)
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

    public static void DrawLayer(VehicleSectionLayerManager component, Section section, Type layerType, Vector3 drawPos,
      Rot8 rot, float angle)
    {
      if (layerType is null) return;

      var layer = component.GetLayer(section, layerType, rot);
      if (layer is null) return;

      DrawLayer(layer, drawPos, angle);
    }

    public static void DrawLayer(SectionLayer layer, Vector3 drawPos, float angle)
    {
      if (!layer.Visible)
        return;

      var rot = Quaternion.AngleAxis(angle, Vector3.up);
      for (var i = 0; i < layer.subMeshes.Count; i++)
      {
        var subMesh = layer.subMeshes[i];
        if (subMesh.finalized && !subMesh.disabled)
        {
          Graphics.DrawMesh(subMesh.mesh, drawPos, rot, subMesh.material, subMesh.renderLayer);
        }
      }
    }
  }
}