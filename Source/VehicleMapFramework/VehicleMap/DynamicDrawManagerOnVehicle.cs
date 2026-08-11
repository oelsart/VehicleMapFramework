using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public static class DynamicDrawManagerOnVehicle
{
  public static void DrawDynamicThings(Map map)
  {
    if (!DebugViewSettings.drawThingsDynamic || map.Disposed)
    {
      return;
    }

    var flag = SilhouetteUtility.CanHighlightAny();
    var drawThings = map.dynamicDrawManager.DrawThings;
    //NativeArray<ThingCullDetails> details = new(drawThings.Count, Allocator.TempJob);
    //ComputeCulledThings(details, map, drawThings);
    if (!DebugViewSettings.singleThreadedDrawing)
    {
      using (new ProfilerBlock("Ensure Graphics Initialized"))
      {
        for (var i = 0; i < drawThings.Count; i++)
        {
          drawThings[i].DynamicDrawPhase(DrawPhase.EnsureInitialized);
        }
      }
      //PreDrawVisibleThings(details, drawThings);
    }

    try
    {
      using (new ProfilerBlock("Draw Visible"))
      {
        MapComponent comp = null;
        var banded = AsAboveSoBelow.Active &&
                     (comp = AsAboveSoBelow.CompOf(map)) is not null &&
                     AsAboveSoBelow.Banded(comp);
        var currentBand = banded ? AsAboveSoBelow.CurrentBand(map) : 0;
        for (var j = 0; j < drawThings.Count; j++)
        {
          try
          {
            if (banded)
            {
              var position = drawThings[j].Position;
              var band = AsAboveSoBelow.BandOf(comp, position);
              if (band > currentBand)
                continue;
              if (band < currentBand &&
                  !(bool)AsAboveSoBelow.TryResolveVisibleBelow(null, Params<(object, object, IntVec3, IntVec3, int)>.Get(
                    (map, comp, AsAboveSoBelow.Translate(comp, position, currentBand), IntVec3.Zero, 0))))
                continue;
            }
            drawThings[j].DynamicDrawPhase(DrawPhase.Draw);
          }
          catch (Exception arg)
          {
            Log.Error($"Exception drawing {drawThings[j]}: {arg}");
          }
        }
      }

      if (flag)
      {
        DrawSilhouettes(drawThings);
      }
    }
    catch (Exception arg2)
    {
      Log.Error($"Exception drawing dynamic things: {arg2}");
    }
    // finally
    // {
    //     details.Dispose();
    // }
  }

  // private static void PreDrawVisibleThings(NativeArray<ThingCullDetails> details, IReadOnlyList<Thing> drawThings)
  // {
  //     using (new ProfilerBlock("Pre draw job"))
  //     {
  //         new ManagedJobParallelFor(new PreDrawThings
  //         {
  //             details = [.. details],
  //             things = drawThings
  //         }).Schedule(details.Length, UnityData.GetIdealBatchCount(details.Length)).Complete();
  //     }
  // }

  // private static void ComputeCulledThings(NativeArray<ThingCullDetails> details, Map map, IReadOnlyList<Thing> drawThings)
  // {
  //     var cellRect = Find.CameraDriver.CurrentViewRect;
  //     cellRect = cellRect.ExpandedBy(1);
  //     if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned && vehicle.VehicleMap != Find.CurrentMap)
  //     {
  //         cellRect.ClipInsideMap(vehicle.Map);
  //     }
  //     using (new ProfilerBlock("Prepare cull job"))
  //     {
  //         for (var i = 0; i < details.Length; i++)
  //         {
  //             var thing = drawThings[i];
  //             ThingCullDetails value = new()
  //             {
  //                 cell = thing.Position,
  //                 coarseBounds = thing.MovedOccupiedDrawRect(),
  //                 // hideAtSnowOrSandDepth = thing.def.hideAtSnowOrSandDepth,
  //                 // seeThroughFog = thing.def.seeThroughFog,
  //                 hasSunShadows = thing.def.HasSunShadows
  //             };
  //             details[i] = value;
  //         }
  //     }
  //     using (new ProfilerBlock("Cull job"))
  //     {
  //         new CullJob
  //         {
  //             mapSizeX = map.Size.x,
  //             viewRect = cellRect,
  //             //fogGrid = map.fogGrid.FogGrid_Unsafe,
  //             //depthGrid = map.snowGrid.DepthGrid_Unsafe,
  //             details = details,
  //             checkShadows = MatBases.SunShadow.shader.isSupported,
  //             shadowViewRect = GetSunShadowsViewRect(map, cellRect)
  //         }.Schedule(details.Length, UnityData.GetIdealBatchCount(details.Length)).Complete();
  //     }
  // }

  public static CellRect GetSunShadowsViewRect(Map map, CellRect rect)
  {
    if (!cachedRect.ContainsKey(map))
    {
      cachedRect[map] = (RealTime.frameCount, CellRect.Empty);
    }
    else if (cachedRect[map].frame == RealTime.frameCount)
    {
      return cachedRect[map].rect;
    }

    var lightSourceInfo = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow);
    if (lightSourceInfo.vector.x < 0f)
    {
      rect.maxX -= Mathf.FloorToInt(lightSourceInfo.vector.x);
    }
    else
    {
      rect.minX -= Mathf.CeilToInt(lightSourceInfo.vector.x);
    }

    if (lightSourceInfo.vector.y < 0f)
    {
      rect.maxZ -= Mathf.FloorToInt(lightSourceInfo.vector.y);
    }
    else
    {
      rect.minZ -= Mathf.CeilToInt(lightSourceInfo.vector.y);
    }

    cachedRect[map] = (RealTime.frameCount, rect);
    return cachedRect[map].rect;
  }

  private static readonly Dictionary<Map, (int frame, CellRect rect)> cachedRect = [];

  private static void DrawSilhouettes(IReadOnlyList<Thing> drawThings)
  {
    // using (new ProfilerBlock("Prepare matrices job"))
    // {
    //     for (var i = 0; i < drawThings.Count; i++)
    //     {
    //         var thing = drawThings[i];
    //         if (SilhouetteUtility.ShouldDrawSilhouette(thing) && thing is Pawn pawn)
    //         {
    //             var value = details[i];
    //             value.pos = pawn.Drawer.renderer.SilhouettePos;
    //             value.drawSize = pawn.Drawer.renderer.SilhouetteGraphic.drawSize;
    //             value.drawSilhouette = true;
    //             details[i] = value;
    //         }
    //     }
    // }
    // using (new ProfilerBlock("Compute matrices"))
    // {
    //     new ComputeSilhouetteMatricesJob
    //     {
    //         inverseFovScale = Find.CameraDriver.InverseFovScale,
    //         altitude = AltitudeLayer.Silhouettes.AltitudeFor(),
    //         details = details
    //     }.Schedule(details.Length, UnityData.GetIdealBatchCount(details.Length)).Complete();
    // }
    using (new ProfilerBlock("Draw silhouettes"))
    {
      for (var j = 0; j < drawThings.Count; j++)
      {
        if (drawThings[j] is Pawn thing2)
        {
          SilhouetteUtility.DrawGraphicSilhouette(thing2, thing2.Drawer.renderer.SilhouettePos);
        }
      }
    }
  }
  //
  // private struct CullJob : IJobParallelFor
  // {
  //     public void Execute(int index)
  //     {
  //         var thingCullDetails = details[index];
  //         _ = CellIndicesUtility.CellToIndex(thingCullDetails.cell, mapSizeX);
  //         //if (!thingCullDetails.seeThroughFog && this.fogGrid[index2])
  //         //{
  //         //    return;
  //         //}
  //         //if (thingCullDetails.hideAtSnowDepth < 1f && this.depthGrid[index2] > thingCullDetails.hideAtSnowDepth)
  //         //{
  //         //    return;
  //         //}
  //         if (!viewRect.Overlaps(thingCullDetails.coarseBounds))
  //         {
  //             if (checkShadows && thingCullDetails.hasSunShadows)
  //             {
  //                 thingCullDetails.shouldDrawShadow = shadowViewRect.Contains(thingCullDetails.cell);
  //             }
  //             return;
  //         }
  //         thingCullDetails.shouldDraw = true;
  //         details[index] = thingCullDetails;
  //     }
  //
  //     public CellRect viewRect;
  //
  //     public CellRect shadowViewRect;
  //
  //     public int mapSizeX;
  //
  //     public bool checkShadows;
  //
  //     //[ReadOnly]
  //     //public NativeArray<bool> fogGrid;
  //
  //     //[ReadOnly]
  //     //public NativeArray<float> depthGrid;
  //
  //     public NativeArray<ThingCullDetails> details;
  // }
  //
  // private struct ThingCullDetails
  // {
  //     public IntVec3 cell;
  //
  //     public CellRect coarseBounds;
  //
  //     // public bool seeThroughFog;
  //     // public float hideAtSnowOrSandDepth;
  //
  //     public Vector3 pos;
  //
  //     public Vector2 drawSize;
  //
  //     public bool drawSilhouette;
  //
  //     public bool hasSunShadows;
  //
  //     public Matrix4x4 trs;
  //
  //     public bool shouldDraw;
  //
  //     public bool shouldDrawShadow;
  // }
  //
  // private struct ComputeSilhouetteMatricesJob : IJobParallelFor
  // {
  //     public void Execute(int index)
  //     {
  //         var thingCullDetails = details[index];
  //         if (!thingCullDetails.drawSilhouette)
  //         {
  //             return;
  //         }
  //         Vector3 vector = new(thingCullDetails.drawSize.x, 0f, thingCullDetails.drawSize.y);
  //         var s = inverseFovScale;
  //         if (vector.x < 2.5f)
  //         {
  //             s.x *= vector.x + SilhouetteUtility.AdjustScale(vector.x);
  //         }
  //         else
  //         {
  //             s.x *= vector.x;
  //         }
  //         if (vector.z < 2.5f)
  //         {
  //             s.z *= vector.z + SilhouetteUtility.AdjustScale(vector.z);
  //         }
  //         else
  //         {
  //             s.z *= vector.z;
  //         }
  //         var pos = thingCullDetails.pos;
  //         pos.y = altitude;
  //         thingCullDetails.trs = Matrix4x4.TRS(pos, Quaternion.identity, s);
  //         details[index] = thingCullDetails;
  //     }
  //
  //     public Vector3 inverseFovScale;
  //
  //     public float altitude;
  //
  //     public NativeArray<ThingCullDetails> details;
  // }
  //
  // private class PreDrawThings : IJobParallelFor
  // {
  //     public void Execute(int index)
  //     {
  //         var thing = things[index];
  //         if (details[index].shouldDraw)
  //         {
  //             thing.DynamicDrawPhase(DrawPhase.ParallelPreDraw);
  //         }
  //     }
  //
  //     public ThingCullDetails[] details;
  //
  //     public IReadOnlyList<Thing> things;
  // }
}