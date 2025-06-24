using Gilzoide.ManagedJobs;
using RimWorld;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public static class DynamicDrawManagerOnVehicle
    {
        public static void DrawDynamicThings(Map map)
        {
            if (!DebugViewSettings.drawThingsDynamic || map.Disposed)
            {
                return;
            }
            bool flag = SilhouetteUtility.CanHighlightAny();
            var drawThings = map.dynamicDrawManager.DrawThings;
            NativeArray<ThingCullDetails> details = new NativeArray<ThingCullDetails>(drawThings.Count, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            ComputeCulledThings(details, map, drawThings);
            if (!DebugViewSettings.singleThreadedDrawing)
            {
                using (new ProfilerBlock("Ensure Graphics Initialized"))
                {
                    for (int i = 0; i < details.Length; i++)
                    {
                        if (details[i].shouldDraw)
                        {
                            drawThings[i].DynamicDrawPhase(DrawPhase.EnsureInitialized);
                        }
                    }
                }
                PreDrawVisibleThings(details, drawThings);
            }
            try
            {
                using (new ProfilerBlock("Draw Visible"))
                {
                    for (int j = 0; j < details.Length; j++)
                    {
                        if (details[j].shouldDraw || details[j].shouldDrawShadow)
                        {
                            try
                            {
                                if (details[j].shouldDraw)
                                {
                                    drawThings[j].DynamicDrawPhase(DrawPhase.Draw);
                                }
                                else if (drawThings[j] is Pawn pawn)
                                {
                                    pawn.DrawShadowAt(pawn.DrawPos);
                                }
                            }
                            catch (Exception arg)
                            {
                                Log.Error(string.Format("Exception drawing {0}: {1}", drawThings[j], arg));
                            }
                        }
                    }
                }
                if (flag)
                {
                    DrawSilhouettes(details, drawThings);
                }
            }
            catch (Exception arg2)
            {
                Log.Error(string.Format("Exception drawing dynamic things: {0}", arg2));
            }
            finally
            {
                details.Dispose();
            }
        }

        private static void PreDrawVisibleThings(NativeArray<ThingCullDetails> details, IReadOnlyList<Thing> drawThings)
        {
            using (new ProfilerBlock("Pre draw job"))
            {
                new ManagedJobParallelFor(new PreDrawThings
                {
                    details = details.ToArray(),
                    things = drawThings
                }).Schedule(details.Length, UnityData.GetIdealBatchCount(details.Length), default(JobHandle)).Complete();
            }
        }

        private static void ComputeCulledThings(NativeArray<ThingCullDetails> details, Map map, IReadOnlyList<Thing> drawThings)
        {
            CellRect cellRect = Find.CameraDriver.CurrentViewRect;
            cellRect = cellRect.ExpandedBy(1);
            if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                cellRect.ClipInsideMap(vehicle.Map);
            }
            using (new ProfilerBlock("Prepare cull job"))
            {
                for (int i = 0; i < drawThings.Count; i++)
                {
                    Thing thing = drawThings[i];
                    ThingCullDetails value = new ThingCullDetails
                    {
                        cell = thing.Position,
                        coarseBounds = thing.MovedOccupiedDrawRect(),
                        hideAtSnowOrSandDepth = thing.def.hideAtSnowOrSandDepth,
                        seeThroughFog = thing.def.seeThroughFog,
                        hasSunShadows = thing.def.HasSunShadows
                    };
                    details[i] = value;
                }
            }
            using (new ProfilerBlock("Cull job"))
            {
                new CullJob
                {
                    mapSizeX = map.Size.x,
                    viewRect = cellRect,
                    //fogGrid = map.fogGrid.FogGrid_Unsafe,
                    //depthGrid = map.snowGrid.DepthGrid_Unsafe,
                    details = details,
                    checkShadows = MatBases.SunShadow.shader.isSupported,
                    shadowViewRect = GetSunShadowsViewRect(map, cellRect)
                }.Schedule(details.Length, UnityData.GetIdealBatchCount(details.Length), default(JobHandle)).Complete();
            }
        }

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
            GenCelestial.LightInfo lightSourceInfo = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow);
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
            if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
            {
                rect.ClipInsideMap(vehicle.Map);
            }
            cachedRect[map] = (RealTime.frameCount, rect);
            return cachedRect[map].rect;
        }

        private static Dictionary<Map, (int frame, CellRect rect)> cachedRect = new Dictionary<Map, (int, CellRect)>();

        private static void DrawSilhouettes(NativeArray<ThingCullDetails> details, IReadOnlyList<Thing> drawThings)
        {
            using (new ProfilerBlock("Prepare matrices job"))
            {
                for (int i = 0; i < details.Length; i++)
                {
                    if (details[i].shouldDraw)
                    {
                        Thing thing = drawThings[i];
                        if (SilhouetteUtility.ShouldDrawSilhouette(thing) && thing is Pawn pawn)
                        {
                            ThingCullDetails value = details[i];
                            value.pos = pawn.Drawer.renderer.SilhouettePos;
                            value.drawSize = pawn.Drawer.renderer.SilhouetteGraphic.drawSize;
                            value.drawSilhouette = true;
                            details[i] = value;
                        }
                    }
                }
            }
            using (new ProfilerBlock("Compute matrices"))
            {
                new ComputeSilhouetteMatricesJob
                {
                    inverseFovScale = Find.CameraDriver.InverseFovScale,
                    altitude = AltitudeLayer.Silhouettes.AltitudeFor(),
                    details = details
                }.Schedule(details.Length, UnityData.GetIdealBatchCount(details.Length), default).Complete();
            }
            using (new ProfilerBlock("Draw silhouettes"))
            {
                for (int j = 0; j < details.Length; j++)
                {
                    if (details[j].drawSilhouette && drawThings[j] is Pawn thing2)
                    {
                        SilhouetteUtility.DrawSilhouetteJob(thing2, details[j].trs);
                    }
                }
            }
        }

        [BurstCompile]
        private struct CullJob : IJobParallelFor
        {
            [BurstCompile]
            public void Execute(int index)
            {
                ThingCullDetails thingCullDetails = this.details[index];
                int index2 = CellIndicesUtility.CellToIndex(thingCullDetails.cell, this.mapSizeX);
                //if (!thingCullDetails.seeThroughFog && this.fogGrid[index2])
                //{
                //    return;
                //}
                //if (thingCullDetails.hideAtSnowDepth < 1f && this.depthGrid[index2] > thingCullDetails.hideAtSnowDepth)
                //{
                //    return;
                //}
                if (!this.viewRect.Overlaps(thingCullDetails.coarseBounds))
                {
                    if (this.checkShadows && thingCullDetails.hasSunShadows)
                    {
                        thingCullDetails.shouldDrawShadow = this.shadowViewRect.Contains(thingCullDetails.cell);
                    }
                    return;
                }
                thingCullDetails.shouldDraw = true;
                this.details[index] = thingCullDetails;
            }

            public CellRect viewRect;

            public CellRect shadowViewRect;

            public int mapSizeX;

            public bool checkShadows;

            //[ReadOnly]
            //public NativeArray<bool> fogGrid;

            //[ReadOnly]
            //public NativeArray<float> depthGrid;

            public NativeArray<ThingCullDetails> details;
        }

        private struct ThingCullDetails
        {
            public IntVec3 cell;

            public CellRect coarseBounds;

            public bool seeThroughFog;

            public float hideAtSnowOrSandDepth;

            public Vector3 pos;

            public Vector2 drawSize;

            public bool drawSilhouette;

            public bool hasSunShadows;

            public Matrix4x4 trs;

            public bool shouldDraw;

            public bool shouldDrawShadow;
        }

        [BurstCompile]
        private struct ComputeSilhouetteMatricesJob : IJobParallelFor
        {
            [BurstCompile]
            public void Execute(int index)
            {
                ThingCullDetails thingCullDetails = this.details[index];
                if (!thingCullDetails.drawSilhouette)
                {
                    return;
                }
                Vector3 vector = new Vector3(thingCullDetails.drawSize.x, 0f, thingCullDetails.drawSize.y);
                Vector3 s = this.inverseFovScale;
                if (vector.x < 2.5f)
                {
                    s.x *= vector.x + SilhouetteUtility.AdjustScale(vector.x);
                }
                else
                {
                    s.x *= vector.x;
                }
                if (vector.z < 2.5f)
                {
                    s.z *= vector.z + SilhouetteUtility.AdjustScale(vector.z);
                }
                else
                {
                    s.z *= vector.z;
                }
                Vector3 pos = thingCullDetails.pos;
                pos.y = this.altitude;
                thingCullDetails.trs = Matrix4x4.TRS(pos, Quaternion.AngleAxis(0f, Vector3.up), s);
                this.details[index] = thingCullDetails;
            }

            public Vector3 inverseFovScale;

            public float altitude;

            public NativeArray<ThingCullDetails> details;
        }

        private class PreDrawThings : IJobParallelFor
        {
            public void Execute(int index)
            {
                Thing thing = this.things[index];
                if (this.details[index].shouldDraw)
                {
                    thing.DynamicDrawPhase(DrawPhase.ParallelPreDraw);
                }
            }

            public ThingCullDetails[] details;

            public IReadOnlyList<Thing> things;
        }
    }
}
