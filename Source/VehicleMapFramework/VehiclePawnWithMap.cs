using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Vehicles.World;
using Verse;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class VehiclePawnWithMap : VehiclePawn
{
    public Map VehicleMap
    {
        get
        {
            if (interiorMap == null)
            {
                GenerateVehicleMap();
            }
            return interiorMap;
        }
    }

    public bool AllowHaulIn
    {
        get
        {
            return allowHaulIn;
        }
        set
        {
            allowHaulIn = value;
        }
    }

    public bool AllowHaulOut
    {
        get
        {
            return allowHaulOut;
        }
        set
        {
            allowHaulOut = value;
        }
    }

    public bool AllowEnter
    {
        get
        {
            return allowEnter;
        }
    }

    public bool AllowExit
    {
        get
        {
            return allowExit;
        }
    }

    public HashSet<IntVec3> CachedStructureCells
    {
        get
        {
            if (structureCellsCache == null || structureCellsDirty)
            {
                structureCellsDirty = false;
                structureCellsCache = [.. interiorMap.listerThings.ThingsOfDef(VMF_DefOf.VMF_VehicleStructureFilled)
                        .Concat(interiorMap.listerThings.ThingsOfDef(VMF_DefOf.VMF_VehicleStructureEmpty)).Select(b => b.Position)];
            }
            return structureCellsCache;
        }
    }

    public HashSet<IntVec3> CachedOutOfBoundsCells
    {
        get
        {
            if (outOfBoundsCellsCache == null || outOfBoundsCellsDirty)
            {
                var props = VehicleDef.GetModExtension<VehicleMapProps>();
                if (props != null)
                {
                    outOfBoundsCellsCache = [.. props.OutOfBoundsCells.Select(c => c.ToIntVec3)];
                }
                else
                {
                    outOfBoundsCellsCache = [];
                }
                ;
            }
            return outOfBoundsCellsCache;
        }
    }

    public HashSet<IntVec3> CachedMapEdgeCells
    {
        get
        {
            if (mapEdgeCellsCache == null)
            {
                mapEdgeCellsCache = [];
                foreach (var c in CellRect.WholeMap(interiorMap).EdgeCells)
                {
                    var facingInside = c.DirectionToInsideMap(this).FacingCell;
                    var c2 = c;
                    while (CachedOutOfBoundsCells.Contains(c2))
                    {
                        c2 += facingInside;
                    }
                    if (c2.InBounds(interiorMap))
                    {
                        mapEdgeCellsCache.Add(c2);
                    }
                }
            }
            return mapEdgeCellsCache;
        }
    }

    public HashSet<IntVec3> CachedStandableMapEdgeCells
    {
        get
        {
            if (standableCellsCachedTick != Find.TickManager.TicksGame || Find.TickManager.Paused)
            {
                standableCellsCachedTick = Find.TickManager.TicksGame;
                standableMapEdgeCellsCache.Clear();
                standableMapEdgeCellsCache.AddRange(CachedMapEdgeCells.Where(c => c.Standable(interiorMap)));
            }
            return standableMapEdgeCellsCache;
        }
    }

    public List<CompVehicleEnterSpot> EnterComps => enterCompsInt;

    public IEnumerable<CompVehicleEnterSpot> AvailableEnterComps => EnterComps.Where(c => c.parent.Position.Standable(interiorMap) && c.Available);

    public override Vector3 DrawPos
    {
        get
        {
            if (Spawned)
            {
                return base.DrawPos;
            }
            return cachedDrawPos;
        }
    }

    public override int UpdateRateTicks
    {
        get
        {
            if (Spawned)
            {
                return 250;
            }
            return base.UpdateRateTicks;
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
                var allGroups = interiorMap.haulDestinationManager.AllGroups;
                var priorityList = allGroups.Select(g => g.Settings.Priority).ToList();
                for (var i = 0; i < allGroups.Count(); i++)
                {
                    allGroups.ElementAt(i).Settings.Priority = (StoragePriority)Math.Min((sbyte)(priorityList[i] + 1), (sbyte)StoragePriority.Critical);
                    MoteMaker.ThrowText(allGroups.ElementAt(i).CellsList[0].ToVector3Shifted().ToBaseMapCoord(this), Map, allGroups.ElementAt(i).Settings.Priority.ToString(), Color.white, -1f);
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
                var allGroups = interiorMap.haulDestinationManager.AllGroups;
                var priorityList = allGroups.Select(g => g.Settings.Priority).ToList();
                for (var i = 0; i < allGroups.Count(); i++)
                {
                    allGroups.ElementAt(i).Settings.Priority = (StoragePriority)Math.Max((sbyte)(priorityList[i] - 1), (sbyte)StoragePriority.Low);
                    MoteMaker.ThrowText(allGroups.ElementAt(i).CellsList[0].ToVector3Shifted().ToBaseMapCoord(this), Map, allGroups.ElementAt(i).Settings.Priority.ToString(), Color.white, -1f);
                }
            },
            defaultLabel = "VMF_DecreasePriority".Translate(),
            defaultDesc = "VMF_DecreasePriorityDesc".Translate(),
            icon = iconDecreasePriority,
        };

        yield return new Command_Toggle()
        {
            isActive = () => allowHaulIn,
            toggleAction = () => allowHaulIn = !allowHaulIn,
            defaultLabel = "VMF_AllowsHaulIn".Translate(),
            defaultDesc = "VMF_AllowsHaulInDesc".Translate(),
            icon = iconAllowHaulIn,
        };

        yield return new Command_Toggle()
        {
            isActive = () => allowHaulOut,
            toggleAction = () => allowHaulOut = !allowHaulOut,
            defaultLabel = "VMF_AllowsHaulOut".Translate(),
            defaultDesc = "VMF_AllowsHaulOutDesc".Translate(),
            icon = iconAllowHaulOut,
        };

        yield return new Command_Toggle()
        {
            isActive = () => allowEnter,
            toggleAction = () => allowEnter = !allowEnter,
            defaultLabel = "VMF_AllowEnter".Translate(),
            defaultDesc = "VMF_AllowEnterDesc".Translate(),
            icon = iconAllowEnter,
        };

        yield return new Command_Toggle()
        {
            isActive = () => allowExit,
            toggleAction = () => allowExit = !allowExit,
            defaultLabel = "VMF_AllowsGetOff".Translate(),
            defaultDesc = "VMF_AllowsGetOffDesc".Translate(),
            icon = iconAllowExit,
        };

        if (DebugSettings.ShowDevGizmos)
        {
            yield return new Command_FocusVehicleMap();
        }
    }

    private void GenerateVehicleMap()
    {
        try
        {
            VehicleMapProps props;
            if ((props = def.GetModExtension<VehicleMapProps>() ?? def.GetModExtension<VehicleInteriors.VehicleMapProps>()) != null)
            {
                var mapParent = (MapParent_Vehicle)WorldObjectMaker.MakeWorldObject(VMF_DefOf.VMF_VehicleMap);
                mapParent.mapGenerator = VMF_DefOf.VMF_VehicleMapGenerator;
                mapParent.vehicle = this;
                mapParent.Tile = 0;
                mapParent.SetFaction(Faction);
                var mapSize = new IntVec3(props.size.x, 1, props.size.z);
                mapSize.x += 2;
                mapSize.z += 2;
                interiorMap = MapGenerator.GenerateMap(mapSize, mapParent, mapParent.MapGeneratorDef, mapParent.ExtraGenStepDefs, isPocketMap: true);
                Find.World.pocketMaps.Add(mapParent);

                foreach (var c in props.EmptyStructureCells)
                {
                    GenSpawn.Spawn(VMF_DefOf.VMF_VehicleStructureEmpty, c.ToIntVec3, interiorMap).SetFaction(Faction.OfPlayer);
                }
                foreach (var c in props.FilledStructureCells)
                {
                    GenSpawn.Spawn(VMF_DefOf.VMF_VehicleStructureFilled, c.ToIntVec3, interiorMap).SetFaction(Faction.OfPlayer);
                }
                foreach (var c in CachedOutOfBoundsCells)
                {
                    GenSpawn.Spawn(VMF_DefOf.VMF_VehicleStructureEmpty, c, interiorMap).SetFaction(Faction.OfPlayer);
                }
            }
        }
        catch (Exception ex)
        {
            VMF_Log.Error($"Error while generating vehicle map.\n{ex}");
        }
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        if (interiorMap == null)
        {
            GenerateVehicleMap();
        }
        interiorMap.PocketMapParent.sourceMap = map;
        SetPocketTileInfo();

        if (def.HasModExtension<VehicleMapProps_Gravship>())
        {
            if (GravshipUtility.GetPlayerGravEngine(interiorMap) is Building_GravEngine engine && (engine.launchInfo?.doNegativeOutcome ?? false))
            {
                var list = handlers.OfType<VehicleRoleHandlerBuildable>().SelectMany<VehicleRoleHandlerBuildable, Pawn>(h => h.thingOwner).ToList();
                for (var i = 0; i < list.Count; i++)
                {
                    DisembarkPawn(list[i]);
                }
                var gravship = GravshipUtility.GenerateGravship(engine);
                GravshipPlacementUtility.PlaceGravshipInMap(gravship, gravship.originalPosition, interiorMap, out _);
                DefDatabase<LandingOutcomeDef>.AllDefsListForReading.RandomElementByWeight(d => d.weight).Worker.ApplyOutcome(gravship);
                engine.launchInfo = null;
            }
        }

        base.SpawnSetup(map, respawningAfterLoad);
        VehiclePawnWithMapCache.RegisterVehicle(this);
        mapFollower = new VehicleMapFollower(this);

        interiorMap.skyManager = Map.skyManager;
        interiorMap.weatherDecider = Map.weatherDecider;
        interiorMap.weatherManager = Map.weatherManager;

        if (Find.CurrentMap == interiorMap)
        {
            Current.Game.CurrentMap = map;
        }
        Transform.rotation = 0f;
    }

    protected override void Tick()
    {
        if (Spawned)
        {
            cachedDrawPos = DrawPos;
            mapFollower.MapFollowerTick();
        }
        base.Tick();
    }

    protected override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        SetPocketTileInfo();
    }

    private void SetPocketTileInfo()
    {
        try
        {
            if (Spawned)
            {
                interiorMap.Parent.Tile = Map.Tile;
                interiorMap.pocketTileInfo = Map.TileInfo;
                return;
            }

            static WorldObject GetWorldObject(IThingHolder holder)
            {
                while (holder != null)
                {
                    if (holder is WorldObject worldObject)
                    {
                        return worldObject;
                    }
                    holder = holder.ParentHolder;
                }
                return null;
            }
            var worldObject2 = GetWorldObject(this);
            if (worldObject2 is AerialVehicleInFlight aerial)
            {
                Task.Run(() =>
                {
                    interiorMap.Parent.Tile = WorldHelper.GetNearestTile(aerial.DrawPos);
                    interiorMap.pocketTileInfo = Find.WorldGrid[interiorMap.Parent.Tile];
                });
                return;
            }
            if (worldObject2 == null || worldObject2 is MapParent_Vehicle)
            {
                return;
            }
            interiorMap.Parent.Tile = worldObject2.Tile;
            interiorMap.pocketTileInfo = Find.WorldGrid[interiorMap.Parent.Tile];
        }
        finally
        {
            interiorMap.pocketTileInfo ??= new Tile
            {
                PrimaryBiome = VMF_DefOf.VMF_VehicleMapGenerator.pocketMapProperties.biome
            };
        }
    }

    //PocketMapとしての管理に変更になったんでマップが破壊されたら車両マップも破壊されるはず
    //public override void Notify_MyMapRemoved()
    //{
    //    Destroy();
    //}

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        VMF_Log.Debug($"{this} is destroyed.");

        if (Spawned)
        {
            DisembarkAll();
        }
        StringBuilder stringBuilder = new();
        bool flag = false;
        foreach (var thing in interiorMap.listerThings.AllThings.Where(t => t.def.drawerType != DrawerType.None).ToArray())
        {
            if (mode != DestroyMode.Vanish)
            {
                var positionOnBaseMap = thing.PositionOnBaseMap();
                if (thing.def.category == ThingCategory.Building)
                {
                    thing.Destroy();
                    thing.Position = positionOnBaseMap;
                    GenLeaving.DoLeavingsFor(thing, Map, DestroyMode.Deconstruct);
                }
                else if (thing.Isnt<Explosion>())
                {
                    thing.DeSpawn();
                    var terrain = positionOnBaseMap.GetTerrain(Map);
                    if (thing is Pawn pawn && (terrain == TerrainDefOf.WaterDeep || terrain == TerrainDefOf.WaterOceanDeep) &&
                        HealthHelper.AttemptToDrown(pawn))
                    {
                        flag = true;
                        stringBuilder.AppendLine(pawn.LabelCap);
                    }
                    if (!GenPlace.TryPlaceThing(thing, positionOnBaseMap, Map, ThingPlaceMode.Near))
                    {
                        CellFinder.TryFindRandomCellNear(positionOnBaseMap, Map, 50, c => GenPlace.TryPlaceThing(thing, c, Map, ThingPlaceMode.Near), out _);
                    }
                }
            }
        }

        if (flag)
        {
            string text = "VF_BoatSunkWithPawnsDesc".Translate(LabelShort, stringBuilder.ToString());
            Find.LetterStack.ReceiveLetter("VF_BoatSunk".Translate(), text, LetterDefOf.NegativeEvent, new TargetInfo(Position, Map));
        }
        base.Destroy(mode);

        if (Find.Maps.Contains(interiorMap))
        {
            Current.Game.DeinitAndRemoveMap(interiorMap, false);
        }

        //基本的にはDeinitの時にすべてキャッシュは破棄されるはずだが……
        //しかし細かなマップ除去や追加操作が行われるとバグりやすい気がするので、もう全部クリアしちゃう
        foreach (var component in typeof(MapComponent).AllSubclassesNonAbstract())
        {
            GenGeneric.InvokeStaticMethodOnGenericType(typeof(MapComponentCache<>), component, "ClearAll");
        }
        //foreach (var component in typeof(DetachedMapComponent).AllSubclassesNonAbstract())
        //{
        //    GenGeneric.InvokeStaticMethodOnGenericType(typeof(DetachedMapComponentCache<>), component, "ClearAll");
        //}

        interiorMap = null;
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        interiorMap.PocketMapParent.sourceMap = null;
        VehiclePawnWithMapCache.DeRegisterVehicle(this);
        mapFollower.DeRegisterVehicle();
        if (mode != DestroyMode.KillFinalize)
        {
            interiorMap.skyManager = new SkyManager(interiorMap);
            interiorMap.skyManager.ForceSetCurSkyGlow(Map.skyManager.CurSkyGlow);
            interiorMap.weatherManager = new WeatherManager(interiorMap);
            interiorMap.weatherManager.curWeather = Map.weatherManager.curWeather;
            interiorMap.weatherManager.lastWeather = Map.weatherManager.lastWeather;
            interiorMap.weatherManager.prevSkyTargetLerp = Map.weatherManager.prevSkyTargetLerp;
            interiorMap.weatherManager.currSkyTargetLerp = Map.weatherManager.currSkyTargetLerp;
            interiorMap.weatherManager.curWeatherAge = Map.weatherManager.curWeatherAge;
            interiorMap.weatherDecider = new WeatherDecider(interiorMap);
        }
        foreach (var thing in interiorMap.listerThings.AllThings.Intersect(Find.Selector.SelectedObjects))
        {
            Find.Selector.Deselect(thing);
        }
        foreach (var zone in interiorMap.zoneManager.AllZones.Intersect(Find.Selector.SelectedObjects))
        {
            Find.Selector.Deselect(zone);
        }
        var crossMapHaulDestinationManager = Map.GetCachedMapComponent<CrossMapHaulDestinationManager>();
        foreach (var haulSource in interiorMap.haulDestinationManager.AllHaulSourcesListForReading)
        {
            crossMapHaulDestinationManager.RemoveHaulSource(haulSource);
        }
        foreach (var haulDestination in interiorMap.haulDestinationManager.AllHaulDestinations)
        {
            crossMapHaulDestinationManager.RemoveHaulDestination(haulDestination);
        }
        CrossMapReachabilityCache.ClearCache();
        var map = Map;
        base.DeSpawn(mode);

        Delay.AfterNTicks(5, () => map.regionGrid.AllRegions.Do(r => r.ListerThings.Remove(this)));
    }

    public override void DrawAt(in Vector3 drawLoc, Rot8 rot, float rotation)
    {
        cachedDrawPos = drawLoc.WithYOffset(-Altitudes.AltInc * 100f);
        if (Transform.rotation != rotation)
        {
            Transform.rotation = rotation;
            CellDesignationsDirty();
        }
        DrawTracker.DynamicDrawPhaseAt(DrawPhase.Draw, in drawLoc, rot, rotation);
        DrawVehicleMap(Transform.rotation);
        var focused = Command_FocusVehicleMap.FocusedVehicle;
        Command_FocusVehicleMap.FocusedVehicle = this;
        interiorMap.roofGrid.RoofGridUpdate();
        interiorMap.mapTemperature.TemperatureUpdate();
        Command_FocusVehicleMap.FocusedVehicle = focused;
    }

    public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
    {
        cachedDrawPos = drawLoc;
        base.DynamicDrawPhaseAt(phase, drawLoc, flip);

        if (phase == DrawPhase.Draw)
        {
            if (vehiclePather?.Moving ?? false)
            {
                CellDesignationsDirty();
            }
            DrawVehicleMap(Transform.rotation);
            var focused = Command_FocusVehicleMap.FocusedVehicle;
            Command_FocusVehicleMap.FocusedVehicle = this;
            interiorMap.roofGrid.RoofGridUpdate();
            interiorMap.mapTemperature.TemperatureUpdate();
            Command_FocusVehicleMap.FocusedVehicle = focused;
        }
    }

    private void CellDesignationsDirty()
    {
        foreach (var def in DefDatabase<DesignationDef>.AllDefs.Where(d => d.targetType == TargetType.Cell))
        {
            DirtyCellDesignationsCache(interiorMap.designationManager, def);
        }
    }

    public virtual void DrawVehicleMap(float extraRotation)
    {
        var map = interiorMap;
        //PlantFallColors.SetFallShaderGlobals(map);
        //map.waterInfo.SetTextures();
        //map.avoidGrid.DebugDrawOnMap();
        //BreachingGridDebug.DebugDrawAllOnMap(map);
        map.mapDrawer.MapMeshDrawerUpdate_First();
        //map.powerNetGrid.DrawDebugPowerNetGrid();
        //DoorsDebugDrawer.DrawDebug();
        //map.mapDrawer.DrawMapMesh();
        var drawPos = Vector3.zero.ToBaseMapCoord(this, extraRotation);
        DrawVehicleMapMesh(map, drawPos, extraRotation);
        DynamicDrawManagerOnVehicle.DrawDynamicThings(map);
        DrawClippers(map);
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
                DrawSection(section, drawPos, extraRotation);
            }
        }
    }

    protected virtual void DrawSection(Section section, Vector3 drawPos, float extraRotation)
    {
        var rot = FullRotation;
        ((SectionLayer_TerrainOnVehicle)section.GetLayer(typeof(SectionLayer_TerrainOnVehicle))).DrawLayer(rot, drawPos, extraRotation);
        ((SectionLayer_ThingsGeneralOnVehicle)section.GetLayer(typeof(SectionLayer_ThingsGeneralOnVehicle))).DrawLayer(rot, drawPos, extraRotation);
        DrawLayer(section, typeof(SectionLayer_BuildingsDamage), drawPos, extraRotation);
        if ((Find.WindowStack.TryGetWindow<MainTabWindow_Architect>(out var window) && (window.selectedDesPanel?.def.showPowerGrid ?? false)) ||
            (Find.DesignatorManager.SelectedDesignator is Designator_Build designator && designator.PlacingDef is ThingDef tDef && tDef.HasComp<CompPower>()))
        {
            ((SectionLayer_ThingsPowerGridOnVehicle)section.GetLayer(typeof(SectionLayer_ThingsPowerGridOnVehicle))).DrawLayer(rot, drawPos.WithY(0f), extraRotation);
        }
        DrawLayer(section, t_SectionLayer_Zones, drawPos, extraRotation);
        if (Find.CurrentMap == interiorMap && !VehicleMapFramework.settings.drawPlanet)
        {
            DrawLayer(section, typeof(SectionLayer_LightingOverlay), drawPos, extraRotation);
        }
        else
        {
            ((SectionLayer_LightingOnVehicle)section.GetLayer(typeof(SectionLayer_LightingOnVehicle))).DrawLayer(this, drawPos, extraRotation);
        }
        DrawModLayers(section, drawPos, extraRotation);
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
            ((SectionLayer_ThingsOnVehicle)section.GetLayer(VFECore.SectionLayer_ResourceOnVehicle))?.DrawLayer(FullRotation, drawPos, extraRotation);
        }
        if (DefenseGrid.Active)
        {
            var selDesignator = Find.DesignatorManager.SelectedDesignator;
            if (selDesignator is Designator_Build designator_Build && designator_Build.PlacingDef is ThingDef thingDef && thingDef.HasComp(DefenseGrid.CompDefenseConduit))
            {
                DrawLayer(section, DefenseGrid.SectionLayer_DefenseGridOverlay, drawPos.Yto0(), extraRotation);
            }
            else if (DefenseGrid.Designator_DeconstructConduit.IsAssignableFrom(selDesignator?.GetType()))
            {
                DrawLayer(section, DefenseGrid.SectionLayer_DefenseGridOverlay, drawPos.Yto0(), extraRotation);
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
                    DrawLayer(section, DubsBadHygiene.SectionLayer_SewagePipeOverlay, drawPos.Yto0(), extraRotation);
                }
                if (airDuctOverlay != null && DubsBadHygiene.SectionLayer_PipeOverlay_mode(airDuctOverlay) == mode)
                {
                    DrawLayer(section, DubsBadHygiene.SectionLayer_AirDuctOverlay, drawPos.Yto0(), extraRotation);
                }
                if (Time.frameCount % 120 == 0)
                {
                    section.GetLayer(DubsBadHygiene.SectionLayer_SewagePipeOverlay)?.Regenerate();
                    section.GetLayer(DubsBadHygiene.SectionLayer_AirDuctOverlay)?.Regenerate();
                }
            }
            DrawLayer(section, DubsBadHygiene.SectionLayer_Irrigation, drawPos, extraRotation);
            DrawLayer(section, DubsBadHygiene.SectionLayer_FertilizerGrid, drawPos, extraRotation);
            ((SectionLayer_ThingsSewagePipeOnVehicle)section.GetLayer(typeof(SectionLayer_ThingsSewagePipeOnVehicle)))?.DrawLayer(FullRotation, drawPos, extraRotation);
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
                    DrawLayer(section, Rimefeller.SectionLayer_SewagePipe, drawPos.Yto0(), extraRotation);
                }
                if (Time.frameCount % 120 == 0)
                {
                    section.GetLayer(Rimefeller.SectionLayer_SewagePipe)?.Regenerate();
                }
            }
            DrawLayer(section, Rimefeller.XSectionLayer_Napalm, drawPos, extraRotation);
            DrawLayer(section, Rimefeller.XSectionLayer_OilSpill, drawPos, extraRotation);
            ((SectionLayer_ThingsPipeOnVehicle)section.GetLayer(typeof(SectionLayer_ThingsPipeOnVehicle)))?.DrawLayer(FullRotation, drawPos, extraRotation);
        }
        if (ModsConfig.OdysseyActive)
        {
            DrawLayer(section, typeof(SectionLayer_GravshipMask), drawPos, extraRotation);
            ((SectionLayer_SubstructurePropsOnVehicle)section.GetLayer(typeof(SectionLayer_SubstructurePropsOnVehicle)))?.DrawLayer(FullRotation, drawPos, extraRotation);
            ((SectionLayer_GravshipHullOnVehicle)section.GetLayer(typeof(SectionLayer_GravshipHullOnVehicle)))?.DrawLayer(FullRotation, drawPos, extraRotation);
        }
    }

    private void DrawLayer(Section section, Type layerType, Vector3 drawPos, float extraRotation)
    {
        if (layerType == null) return;

        var layer = section.GetLayer(layerType);
        if (!layer.Visible)
        {
            return;
        }
        var angle = Ext_Math.RotateAngle(FullRotation.AsAngle, extraRotation);
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
            Material material = ClipMat;
            var quat = FullRotation.AsQuat();
            IntVec3 size = map.Size;
            Vector3 s = new(500f, 1f, size.z);
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
            foreach (var c in CachedStructureCells)
            {
                matrix.SetTRS(c.ToVector3Shifted().ToBaseMapCoord(), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
            }
        }

        if (Find.CurrentMap == map && WorldRendererUtility.DrawingMap && VehicleMapFramework.settings.drawPlanet)
        {
            Material material = MapEdgeClipDrawer.ClipMat;
            Vector2 size = Patch_Map_MapUpdate.MeshSize;
            var longSide = Mathf.Max(DrawSize.x / 2f, DrawSize.y / 2f);
            Vector3 origin = new((-size.x / 2f) + longSide, 0f, (-size.y / 2f) + longSide);
            Vector3 s = new(500f, 1f, size.y);
            Matrix4x4 matrix = default;
            matrix.SetTRS(new Vector3(-250f, 0f, size.y / 2f) + origin, Quaternion.identity, s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
            matrix = default;
            matrix.SetTRS(new Vector3(size.x + 250f, 0f, size.y / 2f) + origin, Quaternion.identity, s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
            s = new Vector3(1000f, 1f, 500f);
            matrix = default;
            matrix.SetTRS(new Vector3(size.x / 2f, 0f, size.y + 250f) + origin, Quaternion.identity, s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
            matrix = default;
            matrix.SetTRS(new Vector3(size.x / 2f, 0f, -250f) + origin, Quaternion.identity, s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }
    }

    public override string GetInspectString()
    {
        if (VehicleMapFramework.settings.weightFactor == 0f) return null;

        var str = base.GetInspectString();
        var stat = GetStatValue(VMF_DefOf.MaximumPayload);

        str += $"\n{VMF_DefOf.MaximumPayload.LabelCap}:" +
            $" {(VehicleMapUtility.VehicleMapMass(this) * VehicleMapFramework.settings.weightFactor).ToStringEnsureThreshold(2, 0)} /" +
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
        Scribe_References.Look(ref interiorMap, "interiorMap");
        Scribe_Values.Look(ref allowHaulIn, "allowsHaulIn");
        Scribe_Values.Look(ref allowHaulOut, "allowsHaulOut");
        Scribe_Values.Look(ref allowEnter, "allowEnter");
        Scribe_Values.Look(ref allowExit, "autoGetOff");
    }

    protected override void PostLoad()
    {
        base.PostLoad();
        CompVehicleTurrets?.RevalidateTurrets();
        ResetRenderStatus();
    }

    private Map interiorMap;

    public VehicleMapFollower mapFollower;

    public Vector3 cachedDrawPos;

    private readonly List<CompVehicleEnterSpot> enterCompsInt = [];

    private bool allowHaulIn = true;

    private bool allowHaulOut = true;

    private bool allowEnter = true;

    private bool allowExit = true;

    private HashSet<IntVec3> structureCellsCache;

    private HashSet<IntVec3> outOfBoundsCellsCache;

    private HashSet<IntVec3> mapEdgeCellsCache;

    private HashSet<IntVec3> standableMapEdgeCellsCache = [];

    public bool structureCellsDirty;

    public bool outOfBoundsCellsDirty;

    private int standableCellsCachedTick;

    private static readonly Material ClipMat = SolidColorMaterials.NewSolidColorMaterial(new Color(0.3f, 0.1f, 0.1f, 0.5f), ShaderDatabase.MetaOverlay);

    private static readonly Texture2D iconAllowHaulIn = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/AllowHaulIn");

    private static readonly Texture2D iconAllowHaulOut = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/AllowHaulOut");

    private static readonly Texture2D iconIncreasePriority = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/IncreasePriority");

    private static readonly Texture2D iconDecreasePriority = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/DecreasePriority");

    private static readonly Texture2D iconAllowEnter = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/AllowEnter");

    private static readonly Texture2D iconAllowExit = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/AllowExit");

    private static readonly Type t_SectionLayer_Zones = AccessTools.TypeByName("Verse.SectionLayer_Zones");

    private static readonly FastInvokeHandler DirtyCellDesignationsCache = MethodInvoker.GetHandler(AccessTools.Method(typeof(DesignationManager), "DirtyCellDesignationsCache"));
}