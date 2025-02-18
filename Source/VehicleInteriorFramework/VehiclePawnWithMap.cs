using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    [StaticConstructorOnStartup]
    public class VehiclePawnWithMap : VehiclePawn
    {
        public Map VehicleMap => this.interiorMap;

        public bool AllowsHaulIn {
            get
            {
                return this.allowsHaulIn;
            }
            set
            {
                this.allowsHaulIn = value;
            }
        }

        public bool AllowsHaulOut
        {
            get
            {
                return this.allowsHaulOut;
            }
            set
            {
                this.allowsHaulOut = value;
            }
        }

        public bool AllowsGetOff
        {
            get
            {
                return this.allowsGetOff;
            }
            set
            {
                this.allowsGetOff = value;
            }
        }

        public HashSet<IntVec3> CachedStructureCells
        {
            get
            {
                if (this.structureCellsCache == null || this.structureCellsDirty)
                {
                    this.structureCellsCache = this.interiorMap.listerThings.ThingsOfDef(VMF_DefOf.VMF_VehicleStructureFilled)
                            .Concat(this.interiorMap.listerThings.ThingsOfDef(VMF_DefOf.VMF_VehicleStructureEmpty)).Select(b => b.Position).ToHashSet();
                }
                return this.structureCellsCache;
            }
        }

        public HashSet<IntVec3> CachedOutOfBoundsCells
        {
            get
            {
                if (this.outOfBoundsCellsCache == null)
                {
                    var props = this.VehicleDef.GetModExtension<VehicleMapProps>();
                    if (props != null)
                    {
                        this.outOfBoundsCellsCache = props.OutOfBoundsCells.Select(c => c.ToIntVec3).ToHashSet();
                    }
                    else
                    {
                        this.outOfBoundsCellsCache = new HashSet<IntVec3>();
                    };
                }
                return this.outOfBoundsCellsCache;
            }
        }

        public HashSet<IntVec3> CachedMapEdgeCells
        {
            get
            {
                if (this.mapEdgeCellsCache == null)
                {
                    this.mapEdgeCellsCache = new HashSet<IntVec3>();
                    foreach (var c in CellRect.WholeMap(this.interiorMap).EdgeCells)
                    {
                        var facingInside = c.FullDirectionToInsideMap(this.interiorMap).FacingCell;
                        var c2 = c;
                        while (this.CachedOutOfBoundsCells.Contains(c2))
                        {
                            c2 += facingInside;
                        }
                        if (c2.InBounds(this.interiorMap))
                        {
                            this.mapEdgeCellsCache.Add(c2);
                        }
                    }
                }
                return this.mapEdgeCellsCache;
            }
        }

        public override List<IntVec3> InteractionCells => this.interactionCellsInt;

        //VehiclePawnWithMapに関してはcachModeに関わらず先にcachedDrawPosから取らないとずれるぜ
        public override Vector3 DrawPos
        {
            get
            {
                if (!VehiclePawnWithMapCache.cachedDrawPos.TryGetValue(this, out var result))
                {
                    result = base.DrawPos;
                }
                return result;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;

            yield return new Command_Toggle()
            {
                isActive = () => this.allowsHaulIn,
                toggleAction = () => this.allowsHaulIn = !this.allowsHaulIn,
                defaultLabel = "VMF_AllowsHaulIn".Translate(),
                defaultDesc = "VMF_AllowsHaulInDesc".Translate(),
                icon = VehiclePawnWithMap.iconAllowsHaulIn,
            };

            yield return new Command_Toggle()
            {
                isActive = () => this.allowsHaulOut,
                toggleAction = () => this.allowsHaulOut = !this.allowsHaulOut,
                defaultLabel = "VMF_AllowsHaulOut".Translate(),
                defaultDesc = "VMF_AllowsHaulOutDesc".Translate(),
                icon = VehiclePawnWithMap.iconAllowsHaulOut,
            };

            yield return new Command_Action()
            {
                action = () =>
                {
                    //リンクされたストレージの優先度が変わりすぎてしまうのを防ぎかつ全てのストレージにMoteを出したいので、一度優先度をキャッシュしておく
                    var allGroups = this.interiorMap.haulDestinationManager.AllGroups;
                    var priorityList = allGroups.Select(g => g.Settings.Priority).ToList();
                    for (var i = 0; i < allGroups.Count(); i++)
                    {
                        allGroups.ElementAt(i).Settings.Priority = (StoragePriority)Math.Min((sbyte)(priorityList[i] + 1), (sbyte)StoragePriority.Critical);
                        MoteMaker.ThrowText(allGroups.ElementAt(i).CellsList[0].ToVector3Shifted().ToBaseMapCoord(this), this.Map, allGroups.ElementAt(i).Settings.Priority.ToString(), Color.white, -1f);
                    }
                },
                defaultLabel = "VMF_IncreasePriority".Translate(),
                defaultDesc = "VMF_IncreasePriorityDesc".Translate(),
                icon = VehiclePawnWithMap.iconIncreasePriority,
            };

            yield return new Command_Action()
            {
                action = () =>
                {
                    var allGroups = this.interiorMap.haulDestinationManager.AllGroups;
                    var priorityList = allGroups.Select(g => g.Settings.Priority).ToList();
                    for (var i = 0; i < allGroups.Count(); i++)
                    {
                        allGroups.ElementAt(i).Settings.Priority = (StoragePriority)Math.Max((sbyte)(priorityList[i] - 1), (sbyte)StoragePriority.Low);
                        MoteMaker.ThrowText(allGroups.ElementAt(i).CellsList[0].ToVector3Shifted().ToBaseMapCoord(this), this.Map, allGroups.ElementAt(i).Settings.Priority.ToString(), Color.white, -1f);
                    }
                },
                defaultLabel = "VMF_DecreasePriority".Translate(),
                defaultDesc = "VMF_DecreasePriorityDesc".Translate(),
                icon = VehiclePawnWithMap.iconDecreasePriority,
            };

            yield return new Command_Toggle()
            {
                isActive = () => this.allowsGetOff,
                toggleAction = () => this.allowsGetOff = !this.allowsGetOff,
                defaultLabel = "VMF_AllowsGetOff".Translate(),
                defaultDesc = "VMF_AllowsGetOffDesc".Translate(),
                icon = VehiclePawnWithMap.iconAllowsGetOff,
            };

            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_FocusVehicleMap();
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            if (this.interiorMap == null)
            {
                VehicleMapProps props;
                if ((props = this.def.GetModExtension<VehicleMapProps>()) != null)
                {
                    var mapParent = (MapParent_Vehicle)WorldObjectMaker.MakeWorldObject(VMF_DefOf.VMF_VehicleMap);
                    mapParent.vehicle = this;
                    mapParent.Tile = 0;
                    mapParent.SetFaction(base.Faction);
                    var mapSize = new IntVec3(props.size.x, 1, props.size.z);
                    mapSize.x += 2;
                    mapSize.z += 2;
                    this.interiorMap = MapGenerator.GenerateMap(mapSize, mapParent, mapParent.MapGeneratorDef, mapParent.ExtraGenStepDefs, null, true);
                    Find.World.GetComponent<VehicleMapParentsComponent>().vehicleMaps.Add(mapParent);

                    foreach (var c in props.EmptyStructureCells)
                    {
                        var c2 = c;
                        c2.x += 1;
                        c2.z += 1;
                        GenSpawn.Spawn(VMF_DefOf.VMF_VehicleStructureEmpty, c2.ToIntVec3, this.interiorMap).SetFaction(Faction.OfPlayer);
                    }
                    foreach (var c in props.FilledStructureCells)
                    {
                        var c2 = c;
                        c2.x += 1;
                        c2.z += 1;
                        GenSpawn.Spawn(VMF_DefOf.VMF_VehicleStructureFilled, c2.ToIntVec3, this.interiorMap).SetFaction(Faction.OfPlayer);
                    }
                    foreach (var c in this.CachedOutOfBoundsCells)
                    {
                        GenSpawn.Spawn(VMF_DefOf.VMF_VehicleStructureEmpty, c, this.interiorMap).SetFaction(Faction.OfPlayer);
                    }
                }
            }
            base.SpawnSetup(map, respawningAfterLoad);
            VehiclePawnWithMapCache.RegisterVehicle(this);

            this.interiorMap.skyManager = this.Map.skyManager;
            this.interiorMap.weatherDecider = this.Map.weatherDecider;
            this.interiorMap.weatherManager = this.Map.weatherManager;
        }

        public override void Tick()
        {
            if (this.Spawned || VehicleInteriors.settings.drawPlanet && Find.CurrentMap == this.interiorMap &&
                this.IsHashIntervalTick(50) && (Find.WindowStack.TryGetWindow<MainTabWindow_Architect>(out var window) && (window.selectedDesPanel?.def.showPowerGrid ?? false) ||
                Find.DesignatorManager.SelectedDesignator is Designator_Build designator && designator.PlacingDef is ThingDef tDef && tDef.HasComp<CompPower>()))
            {
                //PowerGridのメッシュがタイミング的に即時にRegenerateされないので、定期チェックしている。より良い方法を検討したい
                var map = this.interiorMap;
                for (int i = 0; i < map.Size.x; i += 17)
                {
                    for (int j = 0; j < map.Size.z; j += 17)
                    {
                        map.mapDrawer?.MapMeshDirty(new IntVec3(i, 0, j), MapMeshFlagDefOf.PowerGrid);
                    }
                }
            }

            base.Tick();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (base.Spawned)
            {
                this.DisembarkAll();
            }
            StringBuilder stringBuilder = new StringBuilder();
            bool flag = false;
            foreach (var thing in this.interiorMap.listerThings.AllThings.Where(t => t.def.drawerType != DrawerType.None).ToArray())
            {
                VehiclePawnWithMapCache.cachedDrawPos.Remove(thing);
                VehiclePawnWithMapCache.cachedPosOnBaseMap.Remove(thing);
                if (mode != DestroyMode.Vanish)
                {
                    var positionOnBaseMap = thing.PositionOnBaseMap();
                    if (thing.def.category == ThingCategory.Building)
                    {
                        thing.Destroy();
                        thing.Position = positionOnBaseMap;
                        GenLeaving.DoLeavingsFor(thing, this.Map, DestroyMode.Deconstruct);
                    }
                    else if (thing.Isnt<Explosion>())
                    {
                        thing.DeSpawn();
                        var terrain = positionOnBaseMap.GetTerrain(base.Map);
                        if (thing is Pawn pawn && (terrain == TerrainDefOf.WaterDeep || terrain == TerrainDefOf.WaterOceanDeep) &&
                            HediffHelper.AttemptToDrown(pawn))
                        {
                            flag = true;
                            stringBuilder.AppendLine(pawn.LabelCap);
                        }
                        if (!GenPlace.TryPlaceThing(thing, positionOnBaseMap, base.Map, ThingPlaceMode.Near))
                        {
                            CellFinder.TryFindRandomCellNear(positionOnBaseMap, base.Map, 50, c => GenPlace.TryPlaceThing(thing, c, base.Map, ThingPlaceMode.Near), out _);
                        }
                    }
                }
            }

            if (flag)
            {
                string text = "VF_BoatSunkWithPawnsDesc".Translate(LabelShort, stringBuilder.ToString());
                Find.LetterStack.ReceiveLetter("VF_BoatSunk".Translate(), text, LetterDefOf.NegativeEvent, new TargetInfo(base.Position, base.Map));
            }
            Current.Game.DeinitAndRemoveMap(this.interiorMap, false);
            Find.World.GetComponent<VehicleMapParentsComponent>().vehicleMaps.Remove(this.interiorMap.Parent as MapParent_Vehicle);
            base.Destroy(mode);
            this.interiorMap = null;
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            VehiclePawnWithMapCache.DeRegisterVehicle(this);
            if (mode != DestroyMode.KillFinalize)
            {
                this.interiorMap.skyManager = new SkyManager(this.interiorMap);
                this.interiorMap.skyManager.ForceSetCurSkyGlow(this.Map.skyManager.CurSkyGlow);
                this.interiorMap.weatherManager = new WeatherManager(this.interiorMap);
                this.interiorMap.weatherManager.curWeather = this.Map.weatherManager.curWeather;
                this.interiorMap.weatherManager.lastWeather = this.Map.weatherManager.lastWeather;
                this.interiorMap.weatherManager.prevSkyTargetLerp = this.Map.weatherManager.prevSkyTargetLerp;
                this.interiorMap.weatherManager.currSkyTargetLerp = this.Map.weatherManager.currSkyTargetLerp;
                this.interiorMap.weatherManager.curWeatherAge = this.Map.weatherManager.curWeatherAge;
                this.interiorMap.weatherManager.growthSeasonMemory = this.Map.weatherManager.growthSeasonMemory;
                this.interiorMap.weatherDecider = new WeatherDecider(this.interiorMap);
            }
            foreach (var thing in this.interiorMap.listerThings.AllThings.Intersect(Find.Selector.SelectedObjects))
            {
                Find.Selector.Deselect(thing);
            }
            base.DeSpawn(mode);
        }

        public override void DrawAt(Vector3 drawLoc, Rot8 rot, float extraRotation, bool flip = false, bool compDraw = true)
        {
            if (base.CompVehicleLauncher?.inFlight ?? false)
            {
                drawLoc.y = AltitudeLayer.PawnState.AltitudeFor();
            }
            VehiclePawnWithMapCache.cachedDrawPos[this] = drawLoc;
            base.DrawAt(drawLoc, rot, extraRotation, flip, compDraw);
            
            if (base.vehiclePather?.Moving ?? false)
            {
                this.CellDesignationsDirty();
            }
            this.DrawVehicleMap(extraRotation);
            var focused = Command_FocusVehicleMap.FocusedVehicle;
            Command_FocusVehicleMap.FocusedVehicle = this;
            this.interiorMap.roofGrid.RoofGridUpdate();
            this.interiorMap.mapTemperature.TemperatureUpdate();
            Command_FocusVehicleMap.FocusedVehicle = focused;
        }

        private void CellDesignationsDirty()
        {
            foreach (var def in DefDatabase<DesignationDef>.AllDefs.Where(d => d.targetType == TargetType.Cell))
            {
                DirtyCellDesignationsCache(this.interiorMap.designationManager, def);
            }
        }

        public virtual void DrawVehicleMap(float extraRotation)
        {
            var map = this.interiorMap;
            //PlantFallColors.SetFallShaderGlobals(map);
            //map.waterInfo.SetTextures();
            //map.avoidGrid.DebugDrawOnMap();
            //BreachingGridDebug.DebugDrawAllOnMap(map);
            VehiclePawnWithMapCache.cacheMode = true;
            map.mapDrawer.MapMeshDrawerUpdate_First();
            VehiclePawnWithMapCache.cacheMode = false;
            //map.powerNetGrid.DrawDebugPowerNetGrid();
            //DoorsDebugDrawer.DrawDebug();
            //map.mapDrawer.DrawMapMesh();
            var drawPos = Vector3.zero.ToBaseMapCoord(this, extraRotation);
            this.DrawVehicleMapMesh(map, drawPos, extraRotation);
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                DynamicDrawManagerOnVehicle.DrawDynamicThings(map);
            });
            this.DrawClippers(map);
            map.designationManager.DrawDesignations();
            map.overlayDrawer.DrawAllOverlays();
            map.temporaryThingDrawer.Draw();
            map.flecks.FleckManagerDraw();
            //map.gameConditionManager.GameConditionManagerDraw(map);
            //MapEdgeClipDrawer.DrawClippers(__instance);
        }

        private void DrawVehicleMapMesh(Map map, Vector3 drawPos, float extraRotation)
        {
            var mapDrawer = map.mapDrawer;
            for (int i = 0; i < map.Size.x; i += 17)
            {
                for (int j = 0; j < map.Size.z; j += 17)
                {
                    var section = mapDrawer.SectionAt(new IntVec3(i, 0, j));
                    this.DrawSection(section, drawPos, extraRotation);
                }
            }
        }

        protected virtual void DrawSection(Section section, Vector3 drawPos, float extraRotation)
        {
            this.DrawLayer(section, typeof(SectionLayer_TerrainOnVehicle), drawPos, extraRotation);
            ((SectionLayer_ThingsGeneralOnVehicle)section.GetLayer(typeof(SectionLayer_ThingsGeneralOnVehicle))).DrawLayer(this.FullRotation, drawPos, extraRotation);
            this.DrawLayer(section, typeof(SectionLayer_BuildingsDamage), drawPos, extraRotation);
            if (Find.WindowStack.TryGetWindow<MainTabWindow_Architect>(out var window) && (window.selectedDesPanel?.def.showPowerGrid ?? false) ||
                Find.DesignatorManager.SelectedDesignator is Designator_Build designator && designator.PlacingDef is ThingDef tDef && tDef.HasComp<CompPower>())
            {
                ((SectionLayer_ThingsPowerGridOnVehicle)section.GetLayer(typeof(SectionLayer_ThingsPowerGridOnVehicle))).DrawLayer(this.FullRotation, drawPos.WithY(0f), extraRotation);
            }
            this.DrawLayer(section, t_SectionLayer_Zones, drawPos, extraRotation);
            ((SectionLayer_LightingOnVehicle)section.GetLayer(typeof(SectionLayer_LightingOnVehicle))).DrawLayer(this, drawPos, extraRotation);
            if (Find.CurrentMap == this.interiorMap)
            {
                this.DrawLayer(section, typeof(SectionLayer_IndoorMask), drawPos, extraRotation);
                //this.DrawLayer(section, typeof(SectionLayer_LightingOverlay), drawPos, extraRotation);
            }
            //if (DebugViewSettings.drawSectionEdges)
            //{
            //    Vector3 a = section.botLeft.ToVector3();
            //    GenDraw.DrawLineBetween(a, a + new Vector3(0f, 0f, 17f));
            //    GenDraw.DrawLineBetween(a, a + new Vector3(17f, 0f, 0f));
            //    if (section.CellRect.Contains(UI.MouseCell()))
            //    {
            //        var bounds = section.Bounds;
            //        Vector3 a2 = bounds.Min.ToVector3();
            //        Vector3 a3 = bounds.Max.ToVector3() + new Vector3(1f, 0f, 1f);
            //        GenDraw.DrawLineBetween(a2, a2 + new Vector3((float)bounds.Width, 0f, 0f), SimpleColor.Magenta, 0.2f);
            //        GenDraw.DrawLineBetween(a2, a2 + new Vector3(0f, 0f, (float)bounds.Height), SimpleColor.Magenta, 0.2f);
            //        GenDraw.DrawLineBetween(a3, a3 - new Vector3((float)bounds.Width, 0f, 0f), SimpleColor.Magenta, 0.2f);
            //        GenDraw.DrawLineBetween(a3, a3 - new Vector3(0f, 0f, (float)bounds.Height), SimpleColor.Magenta, 0.2f);
            //    }
            //}
        }

        private void DrawLayer(Section section, Type layerType, Vector3 drawPos, float extraRotation)
        {
            var layer = section.GetLayer(layerType);
            if (!layer.Visible)
            {
                return;
            }
            var angle = Ext_Math.RotateAngle(this.FullRotation.AsAngle, extraRotation);
            foreach (var subMesh in layer.subMeshes)
            {
                if (subMesh.finalized && !subMesh.disabled)
                {
                    Graphics.DrawMesh(subMesh.mesh, drawPos, Quaternion.AngleAxis(angle, Vector3.up), subMesh.material, 0);
                }
            }
        }

        private void DrawClippers(Map map)
        {
            if (Command_FocusVehicleMap.FocuseLockedVehicle == this || Command_FocusVehicleMap.FocusedVehicle == this)
            {
                Material material = VehiclePawnWithMap.ClipMat;
                var quat = this.FullRotation.AsQuat();
                IntVec3 size = map.Size;
                Vector3 s = new Vector3(500f, 1f, size.z);
                Matrix4x4 matrix = default;
                matrix.SetTRS(new Vector3(-250f, 0f, size.z / 2f).ToBaseMapCoord(this), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                matrix = default;
                matrix.SetTRS(new Vector3(size.x + 250f, 0f, size.z / 2f).ToBaseMapCoord(this), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                s = new Vector3(1000f, 1f, 500f);
                matrix = default;
                matrix.SetTRS(new Vector3(size.x / 2f, 0f, size.z + 250f).ToBaseMapCoord(this), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                matrix = default;
                matrix.SetTRS(new Vector3(size.x / 2f, 0f, -250f).ToBaseMapCoord(this), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);

                s = Vector3.one;
                foreach (var c in this.CachedStructureCells)
                {
                    matrix.SetTRS(c.ToVector3Shifted().ToBaseMapCoord(), quat, s);
                    Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                }
            }
        }

        public override string GetInspectString()
        {
            if (VehicleInteriors.settings.weightFactor == 0f) return null;

            var str = base.GetInspectString();
            var stat = this.GetStatValue(VMF_DefOf.MaximumPayload);

            str += $"\n{VMF_DefOf.MaximumPayload.LabelCap}:" +
                $" {(VehicleMapUtility.VehicleMapMass(this) * VehicleInteriors.settings.weightFactor).ToStringEnsureThreshold(2, 0)} /" +
                $" {stat.ToStringEnsureThreshold(2, 0)} {"kg".Translate()}";
            return str;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref this.interiorMap, "interiorMap");
            Scribe_Values.Look(ref this.allowsHaulIn, "allowsHaulIn");
            Scribe_Values.Look(ref this.allowsHaulOut, "allowsHaulOut");
            Scribe_Values.Look(ref this.allowsGetOff, "autoGetOff");
        }

        protected override void PostLoad()
        {
            base.PostLoad();
            if (!this.Spawned && !this.Destroyed)
            {
                this.CompVehicleTurrets?.InitTurrets();
                if (UnityData.IsInMainThread)
                {
                    this.graphicOverlay.Init();
                }
                else
                {
                    LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        this.graphicOverlay.Init();
                    });
                }
                base.ResetRenderStatus();
            }
        }

        private Map interiorMap;
        
        private readonly List<IntVec3> interactionCellsInt = new List<IntVec3>();

        private bool allowsHaulIn = true;

        private bool allowsHaulOut = true;

        private bool allowsGetOff = true;

        private HashSet<IntVec3> structureCellsCache;

        private HashSet<IntVec3> outOfBoundsCellsCache;

        private HashSet<IntVec3> mapEdgeCellsCache;

        public bool structureCellsDirty;

        private static readonly Material ClipMat = SolidColorMaterials.NewSolidColorMaterial(new Color(0.3f, 0.1f, 0.1f, 0.5f), ShaderDatabase.MetaOverlay);

        private static readonly Texture2D iconAllowsHaulIn = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/AllowsHaulIn");

        private static readonly Texture2D iconAllowsHaulOut = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/AllowsHaulOut");

        private static readonly Texture2D iconIncreasePriority = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/IncreasePriority");

        private static readonly Texture2D iconDecreasePriority = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/DecreasePriority");

        private static readonly Texture2D iconAllowsGetOff = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/AllowsGetOff");

        private static readonly Type t_SectionLayer_Zones = AccessTools.TypeByName("Verse.SectionLayer_Zones");

        private static readonly FastInvokeHandler DirtyCellDesignationsCache = MethodInvoker.GetHandler(AccessTools.Method(typeof(DesignationManager), "DirtyCellDesignationsCache"));
    }
}