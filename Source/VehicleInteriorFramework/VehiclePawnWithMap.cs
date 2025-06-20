using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Vehicles;
using Verse;
using static VehicleInteriors.ModCompat;

namespace VehicleInteriors
{
    [StaticConstructorOnStartup]
    public class VehiclePawnWithMap : VehiclePawn
    {
        public Map VehicleMap => this.interiorMap;

        public bool AllowHaulIn {
            get
            {
                return this.allowHaulIn;
            }
            set
            {
                this.allowHaulIn = value;
            }
        }

        public bool AllowHaulOut
        {
            get
            {
                return this.allowHaulOut;
            }
            set
            {
                this.allowHaulOut = value;
            }
        }

        public bool AllowEnter
        {
            get
            {
                return this.allowEnter;
            }
        }

        public bool AllowExit
        {
            get
            {
                return this.allowExit;
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

        public HashSet<IntVec3> CachedStandableMapEdgeCells
        {
            get
            {
                if (this.standableCellsCachedTick != Find.TickManager.TicksGame || Find.TickManager.Paused)
                {
                    this.standableCellsCachedTick = Find.TickManager.TicksGame;
                    this.standableMapEdgeCellsCache.Clear();
                    this.standableMapEdgeCellsCache.AddRange(CachedMapEdgeCells.Where(c => c.Standable(this.interiorMap)));
                }
                return this.standableMapEdgeCellsCache;
            }
        }

        public List<CompVehicleEnterSpot> EnterComps => this.enterCompsInt;

        public IEnumerable<CompVehicleEnterSpot> StandableEnterComps => EnterComps.Where(c => c.parent.Position.Standable(interiorMap));

        public override Vector3 DrawPos
        {
            get
            {
                if (this.Spawned)
                {
                    return base.DrawPos;
                }
                return this.cachedDrawPos;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;

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
                icon = iconIncreasePriority,
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
                icon = iconDecreasePriority,
            };

            yield return new Command_Toggle()
            {
                isActive = () => this.allowHaulIn,
                toggleAction = () => this.allowHaulIn = !this.allowHaulIn,
                defaultLabel = "VMF_AllowsHaulIn".Translate(),
                defaultDesc = "VMF_AllowsHaulInDesc".Translate(),
                icon = iconAllowHaulIn,
            };

            yield return new Command_Toggle()
            {
                isActive = () => this.allowHaulOut,
                toggleAction = () => this.allowHaulOut = !this.allowHaulOut,
                defaultLabel = "VMF_AllowsHaulOut".Translate(),
                defaultDesc = "VMF_AllowsHaulOutDesc".Translate(),
                icon = iconAllowHaulOut,
            };

            yield return new Command_Toggle()
            {
                isActive = () => this.allowEnter,
                toggleAction = () => this.allowEnter = !this.allowEnter,
                defaultLabel = "VMF_AllowEnter".Translate(),
                defaultDesc = "VMF_AllowEnterDesc".Translate(),
                icon = iconAllowEnter,
            };

            yield return new Command_Toggle()
            {
                isActive = () => this.allowExit,
                toggleAction = () => this.allowExit = !this.allowExit,
                defaultLabel = "VMF_AllowsGetOff".Translate(),
                defaultDesc = "VMF_AllowsGetOffDesc".Translate(),
                icon = iconAllowExit,
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
            this.mapFollower = new Vehicle_MapFollower(this);

            this.interiorMap.skyManager = this.Map.skyManager;
            this.interiorMap.weatherDecider = this.Map.weatherDecider;
            this.interiorMap.weatherManager = this.Map.weatherManager;
        }

        public override void Tick()
        {
            if (this.Spawned)
            {
                this.cachedDrawPos = this.DrawPos;

                this.mapFollower.MapFollowerTick();
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
                            HealthHelper.AttemptToDrown(pawn))
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
            if (Find.Maps.Contains(this.interiorMap))
            {
                Current.Game.DeinitAndRemoveMap(this.interiorMap, false);
            }
            Find.World.GetComponent<VehicleMapParentsComponent>().vehicleMaps.Remove(this.interiorMap.Parent as MapParent_Vehicle);
            base.Destroy(mode);
            this.interiorMap = null;
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            VehiclePawnWithMapCache.DeRegisterVehicle(this);
            this.mapFollower.DeRegisterVehicle();
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
                this.interiorMap.weatherDecider = new WeatherDecider(this.interiorMap);
            }
            foreach (var thing in this.interiorMap.listerThings.AllThings.Intersect(Find.Selector.SelectedObjects))
            {
                Find.Selector.Deselect(thing);
            }
            foreach (var zone in this.interiorMap.zoneManager.AllZones.Intersect(Find.Selector.SelectedObjects))
            {
                Find.Selector.Deselect(zone);
            }
            var crossMapHaulDestinationManager = this.Map.GetCachedMapComponent<CrossMapHaulDestinationManager>();
            foreach (var haulSource in this.interiorMap.haulDestinationManager.AllHaulSourcesListForReading)
            {
                crossMapHaulDestinationManager.RemoveHaulSource(haulSource);
            }
            foreach (var haulDestination in this.interiorMap.haulDestinationManager.AllHaulDestinations)
            {
                crossMapHaulDestinationManager.RemoveHaulDestination(haulDestination);
            }
            base.DeSpawn(mode);
        }

        public override void DrawAt(in TransformData transform, bool compDraw = true)
        {
            var drawLoc = transform.position;
            if (base.CompVehicleLauncher?.inFlight ?? false)
            {
                drawLoc.y = AltitudeLayer.PawnState.AltitudeFor();
            }
            this.cachedDrawPos = drawLoc;
            base.DrawAt(transform, compDraw);

            if (base.vehiclePather?.Moving ?? false)
            {
                this.CellDesignationsDirty();
            }
            this.DrawVehicleMap(transform.rotation);
            var focused = Command_FocusVehicleMap.FocusedVehicle;
            Command_FocusVehicleMap.FocusedVehicle = this;
            this.interiorMap.roofGrid.RoofGridUpdate();
            this.interiorMap.mapTemperature.TemperatureUpdate();
            Command_FocusVehicleMap.FocusedVehicle = focused;
        }

        [Obsolete]
        public override void DrawAt(Vector3 drawLoc,  Rot8 rot, float extraRotation, bool flip = false, bool compDraw = true)
        {
            if (base.CompVehicleLauncher?.inFlight ?? false)
            {
                drawLoc.y = AltitudeLayer.PawnState.AltitudeFor();
            }
            this.cachedDrawPos = drawLoc;
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
            map.mapDrawer.MapMeshDrawerUpdate_First();
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
            var rot = this.FullRotation;
            ((SectionLayer_TerrainOnVehicle)section.GetLayer(typeof(SectionLayer_TerrainOnVehicle))).DrawLayer(rot, drawPos, extraRotation);
            ((SectionLayer_ThingsGeneralOnVehicle)section.GetLayer(typeof(SectionLayer_ThingsGeneralOnVehicle))).DrawLayer(rot, drawPos, extraRotation);
            this.DrawLayer(section, typeof(SectionLayer_BuildingsDamage), drawPos, extraRotation);
            if (Find.WindowStack.TryGetWindow<MainTabWindow_Architect>(out var window) && (window.selectedDesPanel?.def.showPowerGrid ?? false) ||
                Find.DesignatorManager.SelectedDesignator is Designator_Build designator && designator.PlacingDef is ThingDef tDef && tDef.HasComp<CompPower>())
            {
                ((SectionLayer_ThingsPowerGridOnVehicle)section.GetLayer(typeof(SectionLayer_ThingsPowerGridOnVehicle))).DrawLayer(rot, drawPos.WithY(0f), extraRotation);
            }
            this.DrawLayer(section, t_SectionLayer_Zones, drawPos, extraRotation);
            ((SectionLayer_LightingOnVehicle)section.GetLayer(typeof(SectionLayer_LightingOnVehicle))).DrawLayer(this, drawPos, extraRotation);
            if (Find.CurrentMap == this.interiorMap)
            {
                this.DrawLayer(section, typeof(SectionLayer_IndoorMask), drawPos, extraRotation);
                //this.DrawLayer(section, typeof(SectionLayer_LightingOverlay), drawPos, extraRotation);
            }
            this.DrawModLayers(section, drawPos, extraRotation);
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

        protected virtual void DrawModLayers(Section section, Vector3 drawPos, float extraRotation)
        {
            if (VFECore.Active)
            {
                ((SectionLayer_ThingsOnVehicle)section.GetLayer(VFECore.SectionLayer_ResourceOnVehicle))?.DrawLayer(this.FullRotation, drawPos, extraRotation);
            }
            if (DefenseGrid.Active)
            {
                var selDesignator = Find.DesignatorManager.SelectedDesignator;
                if (selDesignator is Designator_Build designator_Build && designator_Build.PlacingDef is ThingDef thingDef && thingDef.HasComp(DefenseGrid.CompDefenseConduit))
                {
                    this.DrawLayer(section, DefenseGrid.SectionLayer_DefenseGridOverlay, drawPos.Yto0(), extraRotation);
                }
                else if (DefenseGrid.Designator_DeconstructConduit.IsAssignableFrom(selDesignator?.GetType()))
                {
                    this.DrawLayer(section, DefenseGrid.SectionLayer_DefenseGridOverlay, drawPos.Yto0(), extraRotation);
                }
            }
            if (DubsBadHygiene.Active && !DubsBadHygiene.LiteMode)
            {
                var selDesignator = Find.DesignatorManager.SelectedDesignator;
                var sewagePipeOverlay = section.GetLayer(DubsBadHygiene.SectionLayer_SewagePipeOverlay);
                var airDuctOverlay = section.GetLayer(DubsBadHygiene.SectionLayer_AirDuctOverlay);
                CompProperties compProperties;
                if (selDesignator is Designator_Build designator_Build && designator_Build.PlacingDef is ThingDef thingDef &&
                    (compProperties = thingDef.comps.Find(c => DubsBadHygiene.CompProperties_Pipe?.IsAssignableFrom(c.GetType()) ?? false)) != null)
                {
                    var mode = DubsBadHygiene.CompProperties_Pipe_mode(compProperties);
                    if (sewagePipeOverlay != null & DubsBadHygiene.SectionLayer_PipeOverlay_mode(sewagePipeOverlay) == mode)
                    {
                        this.DrawLayer(section, DubsBadHygiene.SectionLayer_SewagePipeOverlay, drawPos.Yto0(), extraRotation);
                    }
                    if (airDuctOverlay != null && DubsBadHygiene.SectionLayer_PipeOverlay_mode(airDuctOverlay) == mode)
                    {
                        this.DrawLayer(section, DubsBadHygiene.SectionLayer_AirDuctOverlay, drawPos.Yto0(), extraRotation);
                    }
                    if (Time.frameCount % 120 == 0)
                    {
                        section.GetLayer(DubsBadHygiene.SectionLayer_SewagePipeOverlay)?.Regenerate();
                        section.GetLayer(DubsBadHygiene.SectionLayer_AirDuctOverlay)?.Regenerate();
                    }
                }
                this.DrawLayer(section, DubsBadHygiene.SectionLayer_Irrigation, drawPos, extraRotation);
                this.DrawLayer(section, DubsBadHygiene.SectionLayer_FertilizerGrid, drawPos, extraRotation);
                ((SectionLayer_ThingsSewagePipeOnVehicle)section.GetLayer(typeof(SectionLayer_ThingsSewagePipeOnVehicle)))?.DrawLayer(this.FullRotation, drawPos, extraRotation);
            }
            if (Rimefeller.Active)
            {
                var selDesignator = Find.DesignatorManager.SelectedDesignator;
                var sewagePipeOverlay = section.GetLayer(Rimefeller.SectionLayer_SewagePipe);
                CompProperties compProperties;
                if (selDesignator is Designator_Build designator_Build && designator_Build.PlacingDef is ThingDef thingDef &&
                    (compProperties = thingDef.comps.Find(c => Rimefeller.CompProperties_Pipe?.IsAssignableFrom(c.GetType()) ?? false)) != null)
                {
                    var mode = Rimefeller.CompProperties_Pipe_mode(compProperties);
                    if (sewagePipeOverlay != null & Rimefeller.SectionLayer_PipeOverlay_mode(sewagePipeOverlay) == mode)
                    {
                        this.DrawLayer(section, Rimefeller.SectionLayer_SewagePipe, drawPos.Yto0(), extraRotation);
                    }
                    if (Time.frameCount % 120 == 0)
                    {
                        section.GetLayer(Rimefeller.SectionLayer_SewagePipe)?.Regenerate();
                    }
                }
                this.DrawLayer(section, Rimefeller.XSectionLayer_Napalm, drawPos, extraRotation);
                this.DrawLayer(section, Rimefeller.XSectionLayer_OilSpill, drawPos, extraRotation);
                ((SectionLayer_ThingsPipeOnVehicle)section.GetLayer(typeof(SectionLayer_ThingsPipeOnVehicle)))?.DrawLayer(this.FullRotation, drawPos, extraRotation);
            }
        }

        private List<Matrix4x4> matrices = new List<Matrix4x4>();

        private void DrawLayer(Section section, Type layerType, Vector3 drawPos, float extraRotation)
        {
            if (layerType == null) return;

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

        public virtual bool AllowEnterFor(Pawn pawn)
        {
            return AllowEnter || (pawn?.HostileTo(Faction.OfPlayer) ?? true) || pawn.Drafted;
        }

        public virtual bool AllowExitFor(Pawn pawn)
        {
            return AllowExit || (pawn?.HostileTo(Faction.OfPlayer) ?? true) || pawn.Drafted;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref this.interiorMap, "interiorMap");
            Scribe_Values.Look(ref this.allowHaulIn, "allowsHaulIn");
            Scribe_Values.Look(ref this.allowHaulOut, "allowsHaulOut");
            Scribe_Values.Look(ref this.allowEnter, "allowEnter");
            Scribe_Values.Look(ref this.allowExit, "autoGetOff");
        }

        protected override void PostLoad()
        {
            base.PostLoad();
            if (!this.Spawned && !this.Destroyed)
            {
                this.CompVehicleTurrets?.InitTurrets();
                if (UnityData.IsInMainThread)
                {
                    this.overlayRenderer.Init();
                }
                else
                {
                    LongEventHandler.ExecuteWhenFinished(() =>
                    {
                        this.overlayRenderer.Init();
                    });
                }
                base.ResetRenderStatus();
            }
        }

        private Map interiorMap;

        public Vehicle_MapFollower mapFollower;

        public Vector3 cachedDrawPos;
        
        private readonly List<CompVehicleEnterSpot> enterCompsInt = new List<CompVehicleEnterSpot>();

        private bool allowHaulIn = true;

        private bool allowHaulOut = true;

        private bool allowEnter = true;

        private bool allowExit = true;

        private HashSet<IntVec3> structureCellsCache;

        private HashSet<IntVec3> outOfBoundsCellsCache;

        private HashSet<IntVec3> mapEdgeCellsCache;

        private HashSet<IntVec3> standableMapEdgeCellsCache = new HashSet<IntVec3>();

        public bool structureCellsDirty;

        private int standableCellsCachedTick;

        private static readonly Material ClipMat = SolidColorMaterials.NewSolidColorMaterial(new Color(0.3f, 0.1f, 0.1f, 0.5f), ShaderDatabase.MetaOverlay);

        private static readonly Texture2D iconAllowHaulIn = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/AllowHaulIn");

        private static readonly Texture2D iconAllowHaulOut = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/AllowHaulOut");

        private static readonly Texture2D iconIncreasePriority = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/IncreasePriority");

        private static readonly Texture2D iconDecreasePriority = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/DecreasePriority");

        private static readonly Texture2D iconAllowEnter = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/AllowEnter");

        private static readonly Texture2D iconAllowExit = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/AllowExit");

        private static readonly Type t_SectionLayer_Zones = AccessTools.TypeByName("Verse.SectionLayer_Zones");

        private static readonly FastInvokeHandler DirtyCellDesignationsCache = MethodInvoker.GetHandler(AccessTools.Method(typeof(DesignationManager), "DirtyCellDesignationsCache"));
    }
}