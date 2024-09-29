using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    [StaticConstructorOnStartup]
    public class VehiclePawnWithInterior : VehiclePawn
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            return base.GetGizmos().AddItem(new Command_FocusVehicleMap());
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            if (this.interiorMap == null)
            {
                VehicleMap vehicleMap;
                if ((vehicleMap = this.def.GetModExtension<VehicleMap>()) != null)
                {
                    var mapParent = (MapParent_Vehicle)WorldObjectMaker.MakeWorldObject(VIF_DefOf.VIF_VehicleMap);
                    mapParent.vehicle = this;
                    mapParent.Tile = 0;
                    this.interiorMap = MapGenerator.GenerateMap(new IntVec3(vehicleMap.size.x, 1, vehicleMap.size.z), mapParent, mapParent.MapGeneratorDef, mapParent.ExtraGenStepDefs, null, true);
                    Find.WorldObjects.Add(mapParent);
                }
            }
            this.UseBaseMapComponent(map);
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            Current.Game.DeinitAndRemoveMap(this.interiorMap, false);
        }

        public override void DrawAt(Vector3 drawLoc, Rot8 rot, float extraRotation, bool flip = false, bool compDraw = true)
        {
            base.DrawAt(drawLoc, rot, extraRotation, flip, compDraw);

            this.CacheDrawPos();

            var map = this.interiorMap;
            PlantFallColors.SetFallShaderGlobals(map);
            //map.waterInfo.SetTextures();
            //map.avoidGrid.DebugDrawOnMap();
            //BreachingGridDebug.DebugDrawAllOnMap(map);
            map.mapDrawer.MapMeshDrawerUpdate_First();
            //map.powerNetGrid.DrawDebugPowerNetGrid();
            //DoorsDebugDrawer.DrawDebug();
            //map.mapDrawer.DrawMapMesh();
            var drawPos = Vector3.zero.OrigToVehicleMap(this);
            this.DrawVehicleMapMesh(map, drawPos);
            map.dynamicDrawManager.DrawDynamicThings();
            map.gameConditionManager.GameConditionManagerDraw(map);
            //MapEdgeClipDrawer.DrawClippers(__instance);
            this.DrawClippers(map);
            map.designationManager.DrawDesignations();
            map.overlayDrawer.DrawAllOverlays();
            map.temporaryThingDrawer.Draw();
            map.flecks.FleckManagerDraw();
        }

        private void DrawVehicleMapMesh(Map map, Vector3 drawPos)
        {
            var mapDrawer = map.mapDrawer;
            for (int i = 0; i < map.Size.x; i += 17)
            {
                for (int j = 0; j < map.Size.z; j += 17)
                {
                    var section = mapDrawer.SectionAt(new IntVec3(i, 0, j));
                    this.DrawSection(section, drawPos);
                }
            }
        }

        private void DrawSection(Section section, Vector3 drawPos)
        {
            this.DrawLayer(section, terrainLayerType, drawPos);
            ((SectionLayer_ThingsOnVehicle)section.GetLayer(typeof(SectionLayer_ThingsOnVehicle))).DrawLayer(this, drawPos);
            this.DrawLayer(section, typeof(SectionLayer_BuildingsDamage), drawPos);
            this.DrawLayer(section, typeof(SectionLayer_ThingsPowerGrid), drawPos);
            if (DebugViewSettings.drawSectionEdges)
            {
                Vector3 a = section.botLeft.ToVector3();
                GenDraw.DrawLineBetween(a, a + new Vector3(0f, 0f, 17f));
                GenDraw.DrawLineBetween(a, a + new Vector3(17f, 0f, 0f));
                if (section.CellRect.Contains(UI.MouseCell()))
                {
                    var bounds = section.Bounds;
                    Vector3 a2 = bounds.Min.ToVector3();
                    Vector3 a3 = bounds.Max.ToVector3() + new Vector3(1f, 0f, 1f);
                    GenDraw.DrawLineBetween(a2, a2 + new Vector3((float)bounds.Width, 0f, 0f), SimpleColor.Magenta, 0.2f);
                    GenDraw.DrawLineBetween(a2, a2 + new Vector3(0f, 0f, (float)bounds.Height), SimpleColor.Magenta, 0.2f);
                    GenDraw.DrawLineBetween(a3, a3 - new Vector3((float)bounds.Width, 0f, 0f), SimpleColor.Magenta, 0.2f);
                    GenDraw.DrawLineBetween(a3, a3 - new Vector3(0f, 0f, (float)bounds.Height), SimpleColor.Magenta, 0.2f);
                }
            }
        }

        private void DrawLayer(Section section, Type layerType, Vector3 drawPos)
        {
            var layer = section.GetLayer(layerType);
            if (layer.Dirty) layer.Regenerate();
            foreach (var subMesh in layer.subMeshes)
            {
                if (layerType == terrainLayerType && subMesh.material.shader != VIF_Shaders.terrainHardWithZ) subMesh.material.shader = VIF_Shaders.terrainHardWithZ;
                Graphics.DrawMesh(subMesh.mesh, drawPos, base.FullRotation.AsQuat(), subMesh.material, 0); ;
            }
        }

        private void DrawClippers(Map map)
        {
            if (VehicleMapUtility.FocusedVehicle == this)
            {
                Material material = VehiclePawnWithInterior.ClipMat;
                var drawPos = this.DrawPos;
                var quat = this.FullRotation.AsQuat();
                IntVec3 size = map.Size;
                Vector3 s = new Vector3(500f, 1f, (float)size.z);
                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(new Vector3(-250f, VehiclePawnWithInterior.ClipAltitude, size.z / 2f).OrigToVehicleMap(this), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                matrix = default(Matrix4x4);
                matrix.SetTRS(new Vector3(size.x + 250f, VehiclePawnWithInterior.ClipAltitude, size.z / 2f).OrigToVehicleMap(this), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                s = new Vector3(1000f, 1f, 500f);
                matrix = default(Matrix4x4);
                matrix.SetTRS(new Vector3(size.x / 2f, VehiclePawnWithInterior.ClipAltitude, size.z + 250f).OrigToVehicleMap(this), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                matrix = default(Matrix4x4);
                matrix.SetTRS(new Vector3(size.x / 2f, VehiclePawnWithInterior.ClipAltitude, -250f).OrigToVehicleMap(this), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref this.interiorMap, "interiorMap");
        }

        private void UseBaseMapComponent(Map map)
        {
            this.interiorMap.attackTargetsCache = map.attackTargetsCache;
            //this.interiorMap.listerThings = map.listerThings;
            //this.interiorMap.listerBuildings = map.listerBuildings;
            //this.interiorMap.mapPawns = map.mapPawns;
        }

        private void CacheDrawPos()
        {
            OnVehiclePositionCache.cacheMode = true;
            foreach(var thing in this.interiorMap.dynamicDrawManager.DrawThings)
            {
                OnVehiclePositionCache.cachedDrawPos[thing] = thing.DrawPos.OrigToVehicleMap(this);
                OnVehiclePositionCache.cachedPosOnBaseMap[thing] = OnVehiclePositionCache.cachedDrawPos[thing].ToIntVec3();
            }
            OnVehiclePositionCache.cacheMode = false;
        }

        public Map interiorMap;

        private static readonly Type terrainLayerType = AccessTools.TypeByName("Verse.SectionLayer_Terrain");

        private static readonly Material ClipMat = SolidColorMaterials.NewSolidColorMaterial(new Color(0.3f, 0.1f, 0.1f, 0.65f), ShaderDatabase.MetaOverlay);

        private static readonly float ClipAltitude = AltitudeLayer.WorldClipper.AltitudeFor();
    }
}