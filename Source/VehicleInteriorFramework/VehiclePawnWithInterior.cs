using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    [StaticConstructorOnStartup]
    public class VehiclePawnWithInterior : VehiclePawn
    {
        public bool AllowsAutoHaul {
            get
            {
                return this.allowsAutoHaul;
            }
            set
            {
                this.allowsAutoHaul = value;
            }
        }

        public override List<IntVec3> InteractionCells => this.interactionCellsInt;

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
            base.SpawnSetup(map, respawningAfterLoad);
            this.cachedDrawPos = this.DrawPos;
            this.CopyFromBaseMapComponents();
        }

        private void CopyFromBaseMapComponents()
        {
            this.interiorMap.attackTargetsCache = this.Map.attackTargetsCache;
            this.interiorMap.listerHaulables = this.Map.listerHaulables;
            foreach(var thing in this.interiorMap.listerThings.AllThings)
            {
                this.interiorMap.attackTargetsCache.Notify_ThingSpawned(thing);
                if(thing.def.category == ThingCategory.Item)
                {
                    this.interiorMap.listerHaulables.Notify_Spawned(thing);
                }
            }

            //foreach (var dest in this.interiorMap.haulDestinationManager.AllHaulDestinations.ToArray())
            //{
            //    this.Map.haulDestinationManager.AddHaulDestination(dest);
            //}
            //foreach (var source in this.interiorMap.haulDestinationManager.AllHaulSourcesListForReading.ToArray())
            //{
            //    this.Map.haulDestinationManager.AddHaulSource(source);
            //}
            //this.interiorMap.haulDestinationManager = this.Map.haulDestinationManager;
        }

        public override void Tick()
        {
            if (this.Spawned)
            {
                this.cachedDrawPos = this.DrawPos;
                if (VehiclePawnWithInterior.lastCachedTick != Find.TickManager.TicksGame)
                {
                    VehiclePawnWithInterior.lastCachedTick = Find.TickManager.TicksGame;
                    OnVehiclePositionCache.cachedDrawPos.Clear();
                    OnVehiclePositionCache.cachedPosOnBaseMap.Clear();
                }
            }
            base.Tick();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            base.Destroy(mode);
            foreach (var thing in this.interiorMap.listerThings.AllThings.Where(t => t.def.drawerType != DrawerType.None))
            {
                OnVehiclePositionCache.cachedDrawPos.Remove(thing);
                OnVehiclePositionCache.cachedPosOnBaseMap.Remove(thing);
                if (mode != DestroyMode.Vanish)
                {
                    thing.DeSpawn(DestroyMode.Vanish);
                    if (thing.def.category == ThingCategory.Building)
                    {
                        thing.Position = this.Position;
                        GenLeaving.DoLeavingsFor(thing, this.Map, DestroyMode.Deconstruct);
                    }
                    else
                    {
                        GenPlace.TryPlaceThing(thing, this.Position, this.Map, ThingPlaceMode.Near);
                    }
                }
            }
            Current.Game.DeinitAndRemoveMap(this.interiorMap, false);
            if (Find.WorldObjects.Contains(this.interiorMap.Parent)) Find.WorldObjects.Remove(this.interiorMap.Parent);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            this.interiorMap.attackTargetsCache = new AttackTargetsCache(this.interiorMap);
            this.interiorMap.listerHaulables = new ListerHaulables(this.interiorMap);
            //this.interiorMap.haulDestinationManager = new HaulDestinationManager(this.interiorMap);

            foreach(var thing in this.interiorMap.listerThings.AllThings)
            {
                this.interiorMap.attackTargetsCache.Notify_ThingSpawned(thing);
                this.Map.attackTargetsCache.Notify_ThingDespawned(thing);
                if (thing.def.category == ThingCategory.Item)
                {
                    this.interiorMap.listerHaulables.Notify_Spawned(thing);
                    this.Map.listerHaulables.Notify_DeSpawned(thing);
                }
            }
            //foreach(var dest in this.Map.haulDestinationManager.AllHaulDestinations.Where(d => d.Map == this.interiorMap).ToArray())
            //{
            //    this.Map.haulDestinationManager.RemoveHaulDestination(dest);
            //    this.interiorMap.haulDestinationManager.AddHaulDestination(dest);
            //}
            //foreach(var source in this.Map.haulDestinationManager.AllHaulSourcesListForReading.Where(s => s.Map == this.interiorMap).ToArray())
            //{
            //    this.Map.haulDestinationManager.RemoveHaulSource(source);
            //    this.interiorMap.haulDestinationManager.AddHaulSource(source);
            //}
            base.DeSpawn(mode);
        }

        public override void DrawAt(Vector3 drawLoc, Rot8 rot, float extraRotation, bool flip = false, bool compDraw = true)
        {
            base.DrawAt(drawLoc, rot, extraRotation, flip, compDraw);

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
            if (anyLayerDirty(section))
            {
                RegenerateDirtyLayers(section);
            }
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
                var drawPos = this.cachedDrawPos;
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
            Scribe_Values.Look(ref this.allowsAutoHaul, "allowsAutoHaul");
        }

        public Map interiorMap;

        public Vector3 cachedDrawPos;

        private static int lastCachedTick = -1;
        
        private readonly List<IntVec3> interactionCellsInt = new List<IntVec3>();

        private static readonly Type terrainLayerType = AccessTools.TypeByName("Verse.SectionLayer_Terrain");

        private static readonly Material ClipMat = SolidColorMaterials.NewSolidColorMaterial(new Color(0.3f, 0.1f, 0.1f, 0.65f), ShaderDatabase.MetaOverlay);

        private static readonly float ClipAltitude = AltitudeLayer.WorldClipper.AltitudeFor();

        private static readonly AccessTools.FieldRef<Section, bool> anyLayerDirty = AccessTools.FieldRefAccess<Section, bool>("anyLayerDirty");

        private static readonly FastInvokeHandler RegenerateDirtyLayers = MethodInvoker.GetHandler(AccessTools.Method(typeof(Section), "RegenerateDirtyLayers"));

        private bool allowsAutoHaul = true;
    }
}