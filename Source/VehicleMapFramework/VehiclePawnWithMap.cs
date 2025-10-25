using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Vehicles.World;
using Verse;
using Verse.AI;
using static VehicleMapFramework.ModCompat;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class VehiclePawnWithMap : VehiclePawn, IAttackTarget
{
    private Map interiorMap;

    private VehicleMapFollower mapFollower;

    public Vector3 cachedDrawPos;

    private bool allowHaulIn = true;

    private bool allowHaulOut = true;

    private bool allowEnter = true;

    private bool allowExit = true;

    public bool structureCellsDirty;

    public bool mapEdgeCellsDirty;

    private int standableCellsCachedTick;

    private int cellDesignationsDirtyTick;

    private static Def pipeNetDef;

    private static readonly Material ClipMat =
        SolidColorMaterials.NewSolidColorMaterial(new Color(0.3f, 0.1f, 0.1f, 0.5f), ShaderDatabase.MetaOverlay);

    private static readonly Texture2D iconAllowHaulIn = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/AllowHaulIn");

    private static readonly Texture2D iconAllowHaulOut = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/AllowHaulOut");

    private static readonly Texture2D iconIncreasePriority = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/IncreasePriority");

    private static readonly Texture2D iconDecreasePriority = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/DecreasePriority");

    private static readonly Texture2D iconAllowEnter = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/AllowEnter");

    private static readonly Texture2D iconAllowExit = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/AllowExit");

    private static readonly Type t_SectionLayer_Zones = GenTypes.GetTypeInAnyAssembly("Verse.SectionLayer_Zones", "Verse");

    private static readonly FastInvokeHandler DirtyCellDesignationsCache =
        MethodInvoker.GetHandler(AccessTools.Method(typeof(DesignationManager), "DirtyCellDesignationsCache"));

    public Map VehicleMap
    {
        get
        {
            if (interiorMap == null)
            {
                GenerateVehicleMap(Map);
            }
            return interiorMap;
        }
    }

    public Map CurrentLevel
    {
        get => field ?? interiorMap;
        set;
    }

    public bool AllowHaulIn
    {
        get => allowHaulIn;
        set => allowHaulIn = value;
    }

    public bool AllowHaulOut
    {
        get => allowHaulOut;
        set => allowHaulOut = value;
    }

    [UsedImplicitly]
    public bool AllowEnter => allowEnter;

    [UsedImplicitly]
    public bool AllowExit => allowExit;

    public HashSet<IntVec3> CachedStructureCells
    {
        get
        {
            if (field != null && !structureCellsDirty) return field;
            structureCellsDirty = false;
            field = [.. interiorMap.listerThings.ThingsOfDef(VMF_DefOf.VMF_VehicleStructureFilled)
                .Concat(interiorMap.listerThings.ThingsOfDef(VMF_DefOf.VMF_VehicleStructureEmpty)).Select(b => b.Position)];
            return field;
        }
    }

    public HashSet<IntVec3> CachedExpandableCells
    {
        get
        {
            if (field != null) return field;
            field = [];
            var props = VehicleDef.GetModExtension<VehicleMapProps>();
            if (props != null)
            {
                field = [.. props.ExpandableCells.Select(c => c.ToIntVec3)];
            }
            else
            {
                field = [];
            }
            return field;
        }
    }

    public HashSet<IntVec3> CachedOutOfBoundsCells
    {
        get
        {
            if (field != null) return field;
            var props = VehicleDef.GetModExtension<VehicleMapProps>();
            if (props != null)
            {
                field = [.. props.OutOfBoundsCells.Select(c => c.ToIntVec3)];
            }
            else
            {
                field = [];
            }
            return field;
        }
    }

    public HashSet<IntVec3> CachedMapEdgeCells
    {
        get
        {
            if (field != null && !mapEdgeCellsDirty) return field;
            field ??= [];
            field.Clear();
            foreach (var c in CellRect.WholeMap(interiorMap).EdgeCells)
            {
                var facingInside = c.DirectionToInsideMap(this).FacingCell;
                var c2 = c;
                while (CachedOutOfBoundsCells.Contains(c2) || (CachedExpandableCells.Contains(c2) && CachedStructureCells.Contains(c2)))
                {
                    c2 += facingInside;
                }
                if (c2.InBounds(interiorMap))
                {
                    field.Add(c2);
                }
            }
            return field;
        }
    }

    public HashSet<IntVec3> CachedWalkableMapEdgeCells
    {
        get
        {
            if (standableCellsCachedTick != GenTicks.TicksGame || Find.TickManager.Paused)
            {
                standableCellsCachedTick = GenTicks.TicksGame;
                field.Clear();
                field.AddRange(CachedMapEdgeCells.Where(c => c.Walkable(interiorMap)));
            }
            return field;
        }
    } = [];

    public List<CompVehicleEnterSpot> EnterComps { get; } = [];

    public IEnumerable<CompVehicleEnterSpot> AvailableEnterComps => EnterComps.Where(c => c.parent.Position.Walkable(interiorMap) && c.Available);

    public List<CompFuelTank> FuelTankComps { get; } = [];
    
    public List<CompMapExpander> MapExpanderComps { get; } = [];

    public override Vector3 DrawPos => Spawned ? base.DrawPos : cachedDrawPos;

    public new bool ThreatDisabled(IAttackTargetSearcher disabledFor) => VehicleMap.mapPawns.FreeHumanlikesSpawnedOfFaction(Faction).Empty() && base.ThreatDisabled(disabledFor);

    public new float TargetPriorityFactor => 0.15f;

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (var gizmo in base.GetGizmos()) yield return gizmo;

        yield return new Command_Action
        {
            action = () =>
            {
                //リンクされたストレージの優先度が変わりすぎてしまうのを防ぎかつ全てのストレージにMoteを出したいので、一度優先度をキャッシュしておく
                var allGroups = interiorMap.haulDestinationManager.AllGroupsListForReading;
                var priorityList = allGroups.Select(g => g.Settings.Priority).ToList();
                for (var i = 0; i < allGroups.Count; i++)
                {
                    allGroups[i].Settings.Priority = (StoragePriority)Math.Min((sbyte)(priorityList[i] + 1), (sbyte)StoragePriority.Critical);
                    MoteMaker.ThrowText(allGroups[i].CellsList[0].ToVector3Shifted().ToBaseMapCoord(this), Map, allGroups[i].Settings.Priority.ToString(), Color.white);
                }
            },
            defaultLabel = "VMF_IncreasePriority".Translate(),
            defaultDesc = "VMF_IncreasePriorityDesc".Translate(),
            icon = iconIncreasePriority,
        };

        yield return new Command_Action
        {
            action = () =>
            {
                var allGroups = interiorMap.haulDestinationManager.AllGroupsListForReading;
                var priorityList = allGroups.Select(g => g.Settings.Priority).ToList();
                for (var i = 0; i < allGroups.Count; i++)
                {
                    allGroups[i].Settings.Priority = (StoragePriority)Math.Max((sbyte)(priorityList[i] - 1), (sbyte)StoragePriority.Low);
                    MoteMaker.ThrowText(allGroups[i].CellsList[0].ToVector3Shifted().ToBaseMapCoord(this), Map, allGroups[i].Settings.Priority.ToString(), Color.white);
                }
            },
            defaultLabel = "VMF_DecreasePriority".Translate(),
            defaultDesc = "VMF_DecreasePriorityDesc".Translate(),
            icon = iconDecreasePriority,
        };

        yield return new Command_Toggle
        {
            isActive = () => allowHaulIn,
            toggleAction = () => allowHaulIn = !allowHaulIn,
            defaultLabel = "VMF_AllowsHaulIn".Translate(),
            defaultDesc = "VMF_AllowsHaulInDesc".Translate(),
            icon = iconAllowHaulIn,
        };

        yield return new Command_Toggle
        {
            isActive = () => allowHaulOut,
            toggleAction = () => allowHaulOut = !allowHaulOut,
            defaultLabel = "VMF_AllowsHaulOut".Translate(),
            defaultDesc = "VMF_AllowsHaulOutDesc".Translate(),
            icon = iconAllowHaulOut,
        };

        yield return new Command_Toggle
        {
            isActive = () => allowEnter,
            toggleAction = () => allowEnter = !allowEnter,
            defaultLabel = "VMF_AllowEnter".Translate(),
            defaultDesc = "VMF_AllowEnterDesc".Translate(),
            icon = iconAllowEnter,
        };

        yield return new Command_Toggle
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
            yield return new Command_Toggle
            {
                defaultLabel = "Debug draw: bridge cells",
                Order = 5001,
                isActive = () => CompMapExpander.debugDraw,
                toggleAction = () => CompMapExpander.debugDraw = !CompMapExpander.debugDraw
            };
        }
    }

    private void GenerateVehicleMap(Map sourceMap)
    {
        try
        {
            VehicleMapProps props;
            if ((props = def.GetModExtension<VehicleMapProps>()) != null)
            {
                var mapParent = (MapParent_Vehicle)WorldObjectMaker.MakeWorldObject(VMF_DefOf.VMF_VehicleMap);
                mapParent.mapGenerator = VMF_DefOf.VMF_VehicleMapGenerator;
                mapParent.vehicle = this;
                mapParent.Tile = 0;
                mapParent.SetFaction(Faction);
                var mapSize = new IntVec3(props.size.x, 1, props.size.z);
                mapSize.x += 2;
                mapSize.z += 2;
                mapParent.sourceMap = sourceMap;
                interiorMap = MapGenerator.GenerateMap(mapSize, mapParent, mapParent.MapGeneratorDef, mapParent.ExtraGenStepDefs, isPocketMap: true);
                Find.World.pocketMaps.Add(mapParent);

                foreach (var c in props.FilledStructureCells)
                {
                    GenSpawn.Spawn(VMF_DefOf.VMF_VehicleStructureFilled, c.ToIntVec3, interiorMap).SetFaction(Faction);
                }
                foreach (var c in props.EmptyStructureCells)
                {
                    GenSpawn.Spawn(VMF_DefOf.VMF_VehicleStructureEmpty, c.ToIntVec3, interiorMap).SetFaction(Faction);
                }
                foreach (var c in props.ExpandableCells)
                {
                    GenSpawn.Spawn(VMF_DefOf.VMF_VehicleStructureEmpty, c.ToIntVec3, interiorMap).SetFaction(Faction);
                }
                foreach (var c in CachedOutOfBoundsCells)
                {
                    GenSpawn.Spawn(VMF_DefOf.VMF_VehicleStructureEmpty, c, interiorMap).SetFaction(Faction);
                }
            }
        }
        catch (Exception ex)
        {
            VMF_Log.Error($"Error while generating vehicle map.\n{ex}");
        }
    }

    internal void RemoveVehicleMap()
    {
        if (Find.Maps.Contains(interiorMap))
        {
            var pocketMapParent = interiorMap.PocketMapParent;
            if (pocketMapParent != null)
            {
                pocketMapParent.sourceMap = null;
                Find.World.pocketMaps.Remove(pocketMapParent);
                Find.World.renderer.wantedMode = WorldRenderMode.None;
            }
            Current.Game.DeinitAndRemoveMap(interiorMap, false);
        }
        interiorMap = null;

        if (!VehicleMapFramework.settings.dynamicUnpatchEnabled) return;
        if (VehicleMapParentsComponent.CachedMapParentVehicle.Any(p => p.Value != null)) return;
        VMF_Harmony.DynamicPatchAll(VehicleMapFramework.settings.dynamicPatchLevel);
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        if (interiorMap == null)
        {
            GenerateVehicleMap(map);
        }
        else
        {
            interiorMap.PocketMapParent?.sourceMap = map;
        }

        if (!Find.World.worldObjects.Contains(interiorMap!.Parent))
        {
            Find.World.worldObjects.Add(interiorMap.Parent);
        }
        CurrentLevel ??= interiorMap;

        var isGravship = def.HasModExtension<VehicleMapProps_Gravship>();
        if (isGravship)
        {
            var engine = GravshipUtility.GetPlayerGravEngine_NewTemp(interiorMap);
            if (engine?.launchInfo?.doNegativeOutcome ?? false)
            {
                var list = handlers.OfType<VehicleRoleHandlerBuildable>().SelectMany<VehicleRoleHandlerBuildable, Pawn>(h => h.thingOwner).ToList();
                foreach (var t in list)
                {
                    DisembarkPawn(t);
                }
                var gravship = GravshipUtility.GenerateGravship(engine);
                GravshipVehicleUtility.PlaceGravship(null, gravship, gravship.originalPosition, interiorMap);
                DefDatabase<LandingOutcomeDef>.AllDefsListForReading.RandomElementByWeight(d => d.weight).Worker.ApplyOutcome(gravship);
                engine.launchInfo = null;
            }
        }

        base.SpawnSetup(map, respawningAfterLoad);
        CacheDrawPos(DrawPos);
        VehiclePawnWithMapCache.RegisterVehicle(this);
        mapFollower = new VehicleMapFollower(this);

        interiorMap.skyManager = Map.skyManager;
        interiorMap.weatherDecider = Map.weatherDecider;
        interiorMap.weatherManager = Map.weatherManager;
        SetTile();

        if (Find.CurrentMap == interiorMap)
        {
            Current.Game.CurrentMap = map;
        }
        Transform.rotation = 0f;
        interiorMap.mapPawns.AllPawns.OfType<VehiclePawn>().Do(v =>
        {
            v.Transform.rotation = 0f;
        });
    }

    protected override void Tick()
    {
        if (Spawned)
        {
            CacheDrawPos(DrawPos);
            mapFollower?.MapFollowerTick();
        }
        else if (this.IsHashIntervalTick(15))
        {
            SetTile();
        }
        base.Tick();
    }

    private void SetTile()
    {
        if (Spawned)
        {
            interiorMap.Parent.Tile = Map.Tile;
            return;
        }

        var worldObject2 = GetWorldObject(this);
        switch (worldObject2)
        {
            case AerialVehicleInFlight aerial:
                Task.Run(() =>
                {
                    interiorMap.Parent.Tile = WorldHelper.GetNearestTile(aerial.DrawPos);
                });
                return;
            case null or MapParent_Vehicle:
                return;
        }

        interiorMap.Parent.Tile = worldObject2.Tile;
        return;

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
    }

    //PocketMapとしての管理に変更になったんでマップが破壊されたら車両マップも破壊されるはず
    //public override void Notify_MyMapRemoved()
    //{
    //    Destroy();
    //}

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        if (Spawned)
        {
            DisembarkAll();
        }
        StringBuilder stringBuilder = new();
        var flag = false;
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
        RemoveVehicleMap();
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        //sourceMapをinteriorMap自身にすると無限ループの危険がある
        interiorMap.PocketMapParent.sourceMap = null;
        VehiclePawnWithMapCache.DeRegisterVehicle(this);
        mapFollower.DeRegisterVehicle();
        if (mode != DestroyMode.KillFinalize)
        {
            interiorMap.skyManager = new SkyManager(interiorMap);
            interiorMap.skyManager.ForceSetCurSkyGlow(Map.skyManager.CurSkyGlow);
            interiorMap.weatherManager = new WeatherManager(interiorMap)
            {
                curWeather = Map.weatherManager.curWeather,
                lastWeather = Map.weatherManager.lastWeather,
                prevSkyTargetLerp = Map.weatherManager.prevSkyTargetLerp,
                currSkyTargetLerp = Map.weatherManager.currSkyTargetLerp,
                curWeatherAge = Map.weatherManager.curWeatherAge
            };
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
        CrossMapReachabilityCache.ClearCacheFor(interiorMap);
        Map.regionGrid.AllRegions.Where(r => r.ListerThings.Contains(this)).Do(r => r.ListerThings.Remove(this));
        base.DeSpawn(mode);
    }

    public override void DrawAt(in Vector3 drawLoc, Rot8 rot, float rotation)
    {
        if (!Spawned)
        {
            interiorMap?.GetDetachedMapComponent<VehiclePositionManager>().AllClaimants.DoIf(v => v.def.graphicData?.drawRotated ?? false, v =>
            {
                v.Transform.rotation = rotation.FlipAngle(v);
            });
            if (!Mathf.Approximately(Transform.rotation, rotation))
            {
                Transform.rotation = rotation;
                CellDesignationsDirty();
            }
        }
        var drawLoc2 = drawLoc.WithYOffset(-Altitudes.AltInc * 100f);
        CacheDrawPos(drawLoc2);
        DrawTracker.DynamicDrawPhaseAt(DrawPhase.Draw, in drawLoc2, rot, Transform.rotation.FlipAngle(this));

        DrawVehicleMap();
    }

    private void CacheDrawPos(Vector3 drawLoc)
    {
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            var transform = new TransformData(drawLoc + Transform.position, FullRotation, Transform.rotation.FlipAngle(this));
            var result = VehicleGraphic?.ParallelGetPreRenderResults(ref transform, false, this);
            cachedDrawPos = result?.position ?? drawLoc;
        });
    }

    public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
    {
        base.DynamicDrawPhaseAt(phase, drawLoc, flip);
        if (phase == DrawPhase.Draw)
        {
            if (vehiclePather?.Moving ?? false)
            {
                CellDesignationsDirty();
            }
            DrawVehicleMap();
        }
    }

    private void CellDesignationsDirty()
    {
        if (cellDesignationsDirtyTick == GenTicks.TicksGame) return;
        cellDesignationsDirtyTick = GenTicks.TicksGame;
        foreach (var designationDef in DefDatabase<DesignationDef>.AllDefs.Where(d => d.targetType == TargetType.Cell))
        {
            DirtyCellDesignationsCache(interiorMap.designationManager, designationDef);
        }
    }

    protected virtual void DrawVehicleMap()
    {
        var map = CurrentLevel ?? interiorMap;
        //PlantFallColors.SetFallShaderGlobals(map);
        //map.waterInfo.SetTextures();
        //map.avoidGrid.DebugDrawOnMap();
        //BreachingGridDebug.DebugDrawAllOnMap(map);
        Delay.AfterNSeconds(0f, () =>
        {
            var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(map);
            component?.cacheMode = true;
            map.GetCachedMapComponent<VehicleSectionLayerManager>().UpdateAllSection();
            map.mapDrawer.MapMeshDrawerUpdate_First();
            component?.cacheMode = false;
        });
        //map.powerNetGrid.DrawDebugPowerNetGrid();
        //DoorsDebugDrawer.DrawDebug();
        //map.mapDrawer.DrawMapMesh();
        var drawPos = Vector3.zero.ToBaseMapCoord(this);
        DrawVehicleMapMesh(map, drawPos);
        DynamicDrawManagerOnVehicle.DrawDynamicThings(map);
        DrawClippers(map);
        map.designationManager.DrawDesignations();
        map.overlayDrawer.DrawAllOverlays();
        map.temporaryThingDrawer.Draw();
        map.flecks.FleckManagerDraw();

        var focused = Command_FocusVehicleMap.FocusedVehicle;
        Command_FocusVehicleMap.FocusedVehicle = this;
        map.roofGrid.RoofGridUpdate();
        map.mapTemperature.TemperatureUpdate();
        MapComponentUtility.MapComponentOnDraw(map);
        CompMapExpander.DebugDraw(MapExpanderComps);
        Command_FocusVehicleMap.FocusedVehicle = focused;
        //map.gameConditionManager.GameConditionManagerDraw(map);
        //MapEdgeClipDrawer.DrawClippers(__instance);
    }

    private void DrawVehicleMapMesh(Map map, Vector3 drawPos)
    {
        var mapDrawer = map.mapDrawer;
        var component = map.GetCachedMapComponent<VehicleSectionLayerManager>();
        for (var i = 0; i < map.Size.x; i += 17)
        {
            for (var j = 0; j < map.Size.z; j += 17)
            {
                var section = mapDrawer.SectionAt(new IntVec3(i, 0, j));
                DrawSection(section, drawPos, component);
            }
        }
    }

    protected virtual void DrawSection(Section section, Vector3 drawPos, VehicleSectionLayerManager component)
    {
        var rot = FullRotation;
        ((SectionLayer_TerrainOnVehicle)section.GetLayer(typeof(SectionLayer_TerrainOnVehicle))).DrawLayer(rot, drawPos, Transform.rotation);
        DrawLayer(component.GetLayer(section, typeof(SectionLayer_ThingsGeneral), rot), drawPos);
        DrawLayer(section, typeof(SectionLayer_BuildingsDamage), drawPos);
        if (OverlayDrawHandler.ShouldDrawPowerGrid)
        {
            DrawLayer(component.GetLayer(section, typeof(SectionLayer_ThingsPowerGrid), rot), drawPos.Yto0());
        }
        if (OverlayDrawHandler.ShouldDrawZones)
        {
            DrawLayer(section, t_SectionLayer_Zones, drawPos);
        }
        if (Find.CurrentMap == interiorMap && !VehicleMapFramework.settings.drawPlanet)
        {
            DrawLayer(section, typeof(SectionLayer_LightingOverlay), drawPos);
        }
        else
        {
            ((SectionLayer_LightingOnVehicle)section.GetLayer(typeof(SectionLayer_LightingOnVehicle))).DrawLayer(this, drawPos, Transform.rotation);
        }
        DrawModLayers(section, drawPos, component);
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

    protected virtual void DrawModLayers(Section section, Vector3 drawPos, VehicleSectionLayerManager component)
    {
        var rot = FullRotation;
        if (VFECore.Active)
        {
            var layer = section.GetLayer(VFECore.SectionLayer_Resource);
            if (layer != null && (bool)VFECore.ShouldDraw(layer))
            {
                var curPipeNetDef = VFECore.pipeNetDef();
                if (pipeNetDef != curPipeNetDef)
                {
                    pipeNetDef = curPipeNetDef;
                    CurrentLevel.mapDrawer.WholeMapChanged(455UL);
                }
                DrawLayer(layer, drawPos);
            }
        }
        if (DefenseGrid.Active)
        {
            var selDesignator = Find.DesignatorManager.SelectedDesignator;
            if (selDesignator is Designator_Build { PlacingDef: ThingDef thingDef } &&
                thingDef.HasComp(DefenseGrid.CompDefenseConduit) ||
                DefenseGrid.Designator_DeconstructConduit.IsInstanceOfType(selDesignator))
            {
                DrawLayer(section, DefenseGrid.SectionLayer_DefenseGridOverlay, drawPos.Yto0());
            }
        }
        if (DubsBadHygiene.Active && !DubsBadHygiene.LiteMode)
        {
            var selDesignator = Find.DesignatorManager.SelectedDesignator;
            var sewagePipeOverlay = section.GetLayer(DubsBadHygiene.SectionLayer_SewagePipeOverlay);
            var airDuctOverlay = section.GetLayer(DubsBadHygiene.SectionLayer_AirDuctOverlay);
            CompProperties compProperties;
            if (selDesignator is Designator_Build { PlacingDef: ThingDef thingDef } &&
                (compProperties = thingDef.comps.Find(c => DubsBadHygiene.CompProperties_Pipe?.IsAssignableFrom(c.GetType()) ?? false)) != null)
            {
                var mode = DubsBadHygiene.CompProperties_Pipe_mode(compProperties);
                if (sewagePipeOverlay != null & DubsBadHygiene.SectionLayer_PipeOverlay_mode(sewagePipeOverlay) == mode)
                {
                    DrawLayer(section, DubsBadHygiene.SectionLayer_SewagePipeOverlay, drawPos.Yto0());
                }
                if (airDuctOverlay != null && DubsBadHygiene.SectionLayer_PipeOverlay_mode(airDuctOverlay) == mode)
                {
                    DrawLayer(section, DubsBadHygiene.SectionLayer_AirDuctOverlay, drawPos.Yto0());
                }
                if (Time.frameCount % 120 == 0)
                {
                    section.GetLayer(DubsBadHygiene.SectionLayer_SewagePipeOverlay)?.Regenerate();
                    section.GetLayer(DubsBadHygiene.SectionLayer_AirDuctOverlay)?.Regenerate();
                }
            }
            DrawLayer(section, DubsBadHygiene.SectionLayer_Irrigation, drawPos);
            DrawLayer(section, DubsBadHygiene.SectionLayer_FertilizerGrid, drawPos);
        }
        if (Rimefeller.Active)
        {
            var selDesignator = Find.DesignatorManager.SelectedDesignator;
            var sewagePipeOverlay = section.GetLayer(Rimefeller.SectionLayer_SewagePipe);
            CompProperties compProperties;
            if (selDesignator is Designator_Build { PlacingDef: ThingDef thingDef } &&
                (compProperties = thingDef.comps.Find(c => Rimefeller.CompProperties_Pipe?.IsAssignableFrom(c.GetType()) ?? false)) != null)
            {
                var mode = Rimefeller.CompProperties_Pipe_mode(compProperties);
                if (sewagePipeOverlay != null & Rimefeller.SectionLayer_PipeOverlay_mode(sewagePipeOverlay) == mode)
                {
                    DrawLayer(section, Rimefeller.SectionLayer_SewagePipe, drawPos.Yto0());
                }
                if (Time.frameCount % 120 == 0)
                {
                    section.GetLayer(Rimefeller.SectionLayer_SewagePipe)?.Regenerate();
                }
            }
            DrawLayer(section, Rimefeller.XSectionLayer_Napalm, drawPos);
            DrawLayer(section, Rimefeller.XSectionLayer_OilSpill, drawPos);
            DrawLayer(component.GetLayer(section, Rimefeller.SectionLayer_ThingsPipe, rot), drawPos);
        }
        if (ModsConfig.OdysseyActive)
        {
            ((SectionLayer_SubstructurePropsOnVehicle)section.GetLayer(typeof(SectionLayer_SubstructurePropsOnVehicle)))?.DrawLayer(FullRotation, drawPos, Transform.rotation);
            ((SectionLayer_GravshipHullOnVehicle)section.GetLayer(typeof(SectionLayer_GravshipHullOnVehicle)))?.DrawLayer(FullRotation, drawPos, Transform.rotation);
        }
        if (MultiFloors.Active && CurrentLevel != interiorMap)
        {
            var layer = component.GetLayer(section, MultiFloors.SectionLayer_LowerLevel, rot);
            if (layer != null)
            {
                DrawLayer(layer, drawPos);
            }
        }
    }

    private void DrawLayer(Section section, Type layerType, Vector3 drawPos)
    {
        if (layerType == null) return;

        var layer = section.GetLayer(layerType);
        DrawLayer(layer, drawPos);
    }

    private void DrawLayer(SectionLayer layer, Vector3 drawPos)
    {
        if (!layer.Visible)
        {
            return;
        }
        var fullAngle = this.FullAngle();
        foreach (var subMesh in layer.subMeshes.Where(subMesh => subMesh.finalized && !subMesh.disabled))
        {
            Graphics.DrawMesh(subMesh.mesh, drawPos, Quaternion.AngleAxis(fullAngle, Vector3.up), subMesh.material, subMesh.renderLayer);
        }
    }

    private void DrawClippers(Map map)
    {
        if (Command_FocusVehicleMap.FocusLockedVehicle == this || Command_FocusVehicleMap.FocusedVehicle == this)
        {
            var material = ClipMat;
            var quat = this.FullAngleQuat();
            var size = map.Size;
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
            IEnumerable<IntVec3> cells = CachedStructureCells;
            if (Find.DesignatorManager.SelectedDesignator is Designator_Build { PlacingDef: ThingDef thingDef } &&
                thingDef.HasComp<CompMapExpander>())
            {
                cells = cells.Where(c => !CachedExpandableCells.Contains(c));
            }
            foreach (var c in cells)
            {
                matrix.SetTRS(c.ToVector3Shifted().ToBaseMapCoord(), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
            }
        }

        var currentMap = Find.CurrentMap;
        if ((currentMap == map || currentMap == interiorMap) && WorldRendererUtility.DrawingMap && VehicleMapFramework.settings.drawPlanet)
        {
            var material = MapEdgeClipDrawer.ClipMat;
            var size = Patch_Map_MapUpdate.MeshSize;
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
        VMF_Harmony.DynamicPatchAll(Level.All);
        base.PostLoad();
        CompVehicleTurrets?.RevalidateTurrets();
        ResetRenderStatus();
        CurrentLevel = interiorMap;
    }

    public override void PostMake()
    {
        base.PostMake();
        var props = def.GetModExtension<VehicleMapProps_Unique>();
        if (props != null && def.defName != props.defName)
        {
            def = UniqueVehicleUtility.GenerateUniqueVehicleDef(this);
            VehicleDef.components?.ForEach(component =>
            {
                component.hitbox.Hitbox.Clear();
                component.hitbox.Initialize(VehicleDef);
            });
        }
    }

    public override void PostGenerationSetup()
    {
        VMF_Harmony.DynamicPatchAll(Level.All);
        base.PostGenerationSetup();
    }
}