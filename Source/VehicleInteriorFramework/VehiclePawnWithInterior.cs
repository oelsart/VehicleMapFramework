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
            base.SpawnSetup(map, respawningAfterLoad);

            VehiclePawnWithInterior.allVehicles.Add(this);
            var mapParent = (MapParent_Vehicle)WorldObjectMaker.MakeWorldObject(VIF_DefOf.VIF_VehicleMap);
            mapParent.vehicle = this;
            this.interiorMap = MapGenerator.GenerateMap(new IntVec3(3, 1, 2), mapParent, mapParent.MapGeneratorDef, mapParent.ExtraGenStepDefs, null, true);
        }

        public override void Tick()
        {
            base.Tick();
        }

        public override void DrawAt(Vector3 drawLoc, Rot8 rot, float extraRotation, bool flip = false, bool compDraw = true)
        {
            base.DrawAt(drawLoc, rot, extraRotation, flip, compDraw);

            var map = this.interiorMap;
            PlantFallColors.SetFallShaderGlobals(map);
            map.waterInfo.SetTextures();
            //map.avoidGrid.DebugDrawOnMap();
            //BreachingGridDebug.DebugDrawAllOnMap(map);
            map.mapDrawer.MapMeshDrawerUpdate_First();
            //map.powerNetGrid.DrawDebugPowerNetGrid();
            //DoorsDebugDrawer.DrawDebug();
            //map.mapDrawer.DrawMapMesh();
            this.DrawVehicleMapMesh(map);
            //map.dynamicDrawManager.DrawDynamicThings();
            map.gameConditionManager.GameConditionManagerDraw(map);
            //MapEdgeClipDrawer.DrawClippers(__instance);
            this.DrawClippers(map);
            map.designationManager.DrawDesignations();
            map.overlayDrawer.DrawAllOverlays();
            map.temporaryThingDrawer.Draw();
            map.flecks.FleckManagerDraw();
        }

        private void DrawVehicleMapMesh(Map map)
        {
            var mapDrawer = map.mapDrawer;
            var viewRect = Find.CameraDriver.CurrentViewRect.ExpandedBy(1).ClipInsideMap(this.Map);
            for (int i = 0; i < map.Size.x; i += 17)
            {
                for (int j = 0; j < map.Size.z; j += 17)
                {
                    var section = mapDrawer.SectionAt(new IntVec3(i, 0, j));
                    this.DrawSection(section);
                }
            }
        }

        private void DrawSection(Section section)
        {
            if (anyLayerDirty(section))
            {
                RegenerateDirtyLayers(section);
            }
            this.DrawLayer(section, terrainLayerType);
            this.DrawLayer(section, typeof(SectionLayer_ThingsGeneral));
            this.DrawLayer(section, typeof(SectionLayer_BuildingsDamage));
            this.DrawLayer(section, typeof(SectionLayer_ThingsPowerGrid));
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

        private void DrawLayer(Section section, Type layerType)
        {
            var terrainLayer = section.GetLayer(layerType);
            if (terrainLayer.Dirty) terrainLayer.Regenerate();
            foreach (var subMesh in terrainLayer.subMeshes)
            {
                if (layerType == terrainLayerType && subMesh.material.shader != VIF_Shaders.terrainHardWithZ) subMesh.material.shader = VIF_Shaders.terrainHardWithZ;
                Graphics.DrawMesh(subMesh.mesh, Vector3.zero.OrigToVehicleMap(this).WithY(this.DrawPos.y).WithYOffset(Altitudes.AltInc * 20f), base.FullRotation.AsQuat(), subMesh.material, 0); ;
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

        public Map interiorMap;

        public static HashSet<VehiclePawnWithInterior> allVehicles = new HashSet<VehiclePawnWithInterior>();

        private static readonly Type terrainLayerType = AccessTools.TypeByName("Verse.SectionLayer_Terrain");

        private static AccessTools.FieldRef<Section, bool> anyLayerDirty = AccessTools.FieldRefAccess<Section, bool>("anyLayerDirty");

        private static readonly FastInvokeHandler RegenerateDirtyLayers = MethodInvoker.GetHandler(AccessTools.Method(typeof(Section), "RegenerateDirtyLayers"));

        private static readonly Material ClipMat = SolidColorMaterials.NewSolidColorMaterial(new Color(0.5f, 0.1f, 0.1f, 0.5f), ShaderDatabase.MetaOverlay);

        private static readonly float ClipAltitude = AltitudeLayer.WorldClipper.AltitudeFor();
    }
}