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

        public bool AutoGetOff
        {
            get
            {
                return this.autoGetOff;
            }
            set
            {
                this.autoGetOff = value;
            }
        }

        public HashSet<IntVec3> CachedStructureCells
        {
            get
            {
                if (this.structureCellsCache == null || this.structureCellsDirty)
                {
                    this.structureCellsCache = this.interiorMap.listerThings.ThingsOfDef(VIF_DefOf.VIF_VehicleStructureFilled)
                            .Concat(this.interiorMap.listerThings.ThingsOfDef(VIF_DefOf.VIF_VehicleStructureEmpty)).Select(b => b.Position).ToHashSet();
                }
                return this.structureCellsCache;
            }
        }

        public override List<IntVec3> InteractionCells => this.interactionCellsInt;

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;

            yield return new Command_Toggle()
            {
                isActive = () => this.allowsHaulIn,
                toggleAction = () => this.allowsHaulIn = !this.allowsHaulIn,
                defaultLabel = "VIF_AllowsHaulIn".Translate(),
                defaultDesc = "VIF_AllowsHaulInDesc".Translate(),
                icon = VehiclePawnWithMap.iconAllowsHaulIn,
            };

            yield return new Command_Toggle()
            {
                isActive = () => this.allowsHaulOut,
                toggleAction = () => this.allowsHaulOut = !this.allowsHaulOut,
                defaultLabel = "VIF_AllowsHaulOut".Translate(),
                defaultDesc = "VIF_AllowsHaulOutDesc".Translate(),
                icon = VehiclePawnWithMap.iconAllowsHaulOut,
            };

            yield return new Command_Action()
            {
                action = () =>
                {
                    //リンクされたストレージの優先度が変わりすぎてしまうのを防ぎかつ全てのストレージにMoteを出したいので、一度優先度をキャッシュしておく
                    var priorityList = new List<StoragePriority>();
                    var allGroups = this.interiorMap.haulDestinationManager.AllGroups;
                    for (var i = 0; i < allGroups.Count(); i++)
                    {
                        priorityList.Add(allGroups.ElementAt(i).Settings.Priority);
                    }
                    for (var i = 0; i < allGroups.Count(); i++)
                    {
                        allGroups.ElementAt(i).Settings.Priority = (StoragePriority)Math.Min((sbyte)(priorityList[i] + 1), (sbyte)StoragePriority.Critical);
                        MoteMaker.ThrowText(allGroups.ElementAt(i).CellsList[0].ToVector3Shifted().OrigToVehicleMap(this), this.Map, allGroups.ElementAt(i).Settings.Priority.ToString(), Color.white, -1f);
                    }
                },
                defaultLabel = "VIF_IncreasePriority".Translate(),
                defaultDesc = "VIF_IncreasePriorityDesc".Translate(),
                icon = VehiclePawnWithMap.iconIncreasePriority,
            };

            yield return new Command_Action()
            {
                action = () =>
                {
                    var priorityList = new List<StoragePriority>();
                    var allGroups = this.interiorMap.haulDestinationManager.AllGroups;
                    for (var i = 0; i < allGroups.Count(); i++)
                    {
                        priorityList.Add(allGroups.ElementAt(i).Settings.Priority);
                    }
                    for (var i = 0; i < allGroups.Count(); i++)
                    {
                        allGroups.ElementAt(i).Settings.Priority = (StoragePriority)Math.Max((sbyte)(priorityList[i] - 1), (sbyte)StoragePriority.Low);
                        MoteMaker.ThrowText(allGroups.ElementAt(i).CellsList[0].ToVector3Shifted().OrigToVehicleMap(this), this.Map, allGroups.ElementAt(i).Settings.Priority.ToString(), Color.white, -1f);
                    }
                },
                defaultLabel = "VIF_DecreasePriority".Translate(),
                defaultDesc = "VIF_DecreasePriorityDesc".Translate(),
                icon = VehiclePawnWithMap.iconDecreasePriority,
            };

            yield return new Command_Toggle()
            {
                isActive = () => this.autoGetOff,
                toggleAction = () => this.autoGetOff = !this.autoGetOff,
                defaultLabel = "VIF_AutoGetOff".Translate(),
                defaultDesc = "VIF_AutoGetOffDesc".Translate(),
                icon = VehiclePawnWithMap.iconAutoGetOff,
            };

            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_FocusVehicleMap();
            }
        }

        public override string GetInspectString()
        {
            var str = base.GetInspectString();
            //if (Find.TickManager.TicksGame % 250 == 0)
            //{
            //    this.statHandler.MarkStatDirty(VIF_DefOf.MaximumPayload);
            //}
            var stat = this.GetStatValue(VIF_DefOf.MaximumPayload);

            str += $"\n{"MassCarriedSimple".Translate()}:" +
                $" {(VehicleMapUtility.VehicleMapMass(this) + MassUtility.InventoryMass(this)).ToStringEnsureThreshold(2, 0)} /" +
                $" {stat.ToStringEnsureThreshold(2, 0)} {"kg".Translate()}";
            return str;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            if (this.interiorMap == null)
            {
                VehicleMapProps vehicleMap;
                if ((vehicleMap = this.def.GetModExtension<VehicleMapProps>()) != null)
                {
                    var mapParent = (MapParent_Vehicle)WorldObjectMaker.MakeWorldObject(VIF_DefOf.VIF_VehicleMap);
                    mapParent.vehicle = this;
                    mapParent.Tile = 0;
                    mapParent.SetFaction(base.Faction);
                    this.interiorMap = MapGenerator.GenerateMap(new IntVec3(vehicleMap.size.x, 1, vehicleMap.size.z), mapParent, mapParent.MapGeneratorDef, mapParent.ExtraGenStepDefs, null, true);
                    Find.World.GetComponent<VehicleMapParentsComponent>().vehicleMaps.Add(mapParent);

                    foreach (var c in vehicleMap.EmptyStructureCells)
                    {
                        GenSpawn.Spawn(VIF_DefOf.VIF_VehicleStructureEmpty, c.ToIntVec3, this.interiorMap).SetFaction(Faction.OfPlayer);
                    }
                    foreach (var c in vehicleMap.FilledStructureCells)
                    {
                        GenSpawn.Spawn(VIF_DefOf.VIF_VehicleStructureFilled, c.ToIntVec3, this.interiorMap).SetFaction(Faction.OfPlayer);
                    }
                }
            }
            base.SpawnSetup(map, respawningAfterLoad);
            this.cachedDrawPos = this.DrawPos;
            if (!VehiclePawnWithMapCache.allVehicles.ContainsKey(map))
            {
                VehiclePawnWithMapCache.allVehicles[map] = new List<VehiclePawnWithMap>();
            }
            VehiclePawnWithMapCache.allVehicles[map].Add(this);

            this.interiorMap.skyManager = this.Map.skyManager;
            this.interiorMap.weatherDecider = this.Map.weatherDecider;
            this.interiorMap.weatherManager = this.Map.weatherManager;
        }

        public override void Tick()
        {
            if (this.Spawned)
            {
                this.cachedDrawPos = this.DrawPos;
                //PowerGridのメッシュがタイミング的に即時にRegenerateされないので、定期チェックしている。より良い方法を検討したい
                if (this.IsHashIntervalTick(250))
                {
                    var map = this.interiorMap;
                    for (int i = 0; i < map.Size.x; i += 17)
                    {
                        for (int j = 0; j < map.Size.z; j += 17)
                        {
                            map.mapDrawer.MapMeshDirty(new IntVec3(i, 0, j), MapMeshFlagDefOf.PowerGrid);
                        }
                    }
                }
            }
            else if (this.CompVehicleLauncher?.launchProtocol != null && Find.CurrentMap != this.interiorMap)
            {
                this.cachedDrawPos = this.CompVehicleLauncher.launchProtocol.DrawPos;
            }
            this.ResetCache();

            base.Tick();
        }

        public void ResetCache()
        {
            if (VehiclePawnWithMap.lastCachedTick != Find.TickManager.TicksGame)
            {
                VehiclePawnWithMap.lastCachedTick = Find.TickManager.TicksGame;
                VehiclePawnWithMapCache.cachedDrawPos.Clear();
                VehiclePawnWithMapCache.cachedPosOnBaseMap.Clear();
            }
            //else
            //{
            //    foreach (var thing in this.interiorMap.listerThings.AllThings)
            //    {
            //        VehiclePawnWithMapCache.cachedDrawPos.Remove(thing);
            //    }
            //}
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
                    else
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
            _ = this.interiorMap;
            base.Destroy(mode);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            VehiclePawnWithMapCache.allVehicles[this.Map].Remove(this);
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


            base.DeSpawn(mode);
            foreach (var thing in this.interiorMap.listerThings.AllThings)
            {
                VehiclePawnWithMapCache.cachedDrawPos.Remove(thing);
                VehiclePawnWithMapCache.cachedPosOnBaseMap.Remove(thing);
            }
        }

        public override void DrawAt(Vector3 drawLoc, Rot8 rot, float extraRotation, bool flip = false, bool compDraw = true)
        {
            this.ResetCache();
            if (this.Spawned || Find.CurrentMap == this.interiorMap)
            {
                this.cachedDrawPos = drawLoc;
                base.DrawAt(drawLoc, rot, extraRotation, flip, compDraw);
            }
            else
            {
                drawLoc.y = AltitudeLayer.PawnState.AltitudeFor();
                this.cachedDrawPos = drawLoc;
                bool northSouthRotation = (this.VehicleGraphic.EastDiagonalRotated && (this.FullRotation == Rot8.NorthEast || this.FullRotation == Rot8.SouthEast)) || (this.VehicleGraphic.WestDiagonalRotated && (this.FullRotation == Rot8.NorthWest || this.FullRotation == Rot8.SouthWest));
                this.Drawer.renderer.RenderPawnAt_TEMP(drawLoc, rot, extraRotation, northSouthRotation);
                if (compDraw)
                {
                    this.Comps_PostDrawUnspawned(drawLoc, rot, extraRotation);
                }
            }

            this.DrawVehicleMap(extraRotation);
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
            var drawPos = Vector3.zero.OrigToVehicleMap(this, extraRotation);
            this.DrawVehicleMapMesh(map, drawPos, extraRotation);
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                DynamicDrawManagerOnVehicle.DrawDynamicThings(map);
            });
            this.DrawClippers(map);
            map.designationManager.DrawDesignations();
            map.overlayDrawer.DrawAllOverlays();
            map.temporaryThingDrawer.Draw();
            //map.flecks.FleckManagerDraw();
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
            ((SectionLayer_ThingsPowerGridOnVehicle)section.GetLayer(typeof(SectionLayer_ThingsPowerGridOnVehicle))).DrawLayer(this.FullRotation, drawPos.WithY(0f), extraRotation);
            this.DrawLayer(section, t_SectionLayer_Zones, drawPos, extraRotation);
            ((SectionLayer_LightingOnVehicle)section.GetLayer(typeof(SectionLayer_LightingOnVehicle))).DrawLayer(this, drawPos, extraRotation);
            if (Find.CurrentMap == this.interiorMap)
            {
                this.DrawLayer(section, typeof(SectionLayer_IndoorMask), drawPos, extraRotation);
                this.DrawLayer(section, typeof(SectionLayer_LightingOverlay), drawPos, extraRotation);
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
                matrix.SetTRS(new Vector3(-250f, 0f, size.z / 2f).OrigToVehicleMap(this), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                matrix = default;
                matrix.SetTRS(new Vector3(size.x + 250f, 0f, size.z / 2f).OrigToVehicleMap(this), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                s = new Vector3(1000f, 1f, 500f);
                matrix = default;
                matrix.SetTRS(new Vector3(size.x / 2f, 0f, size.z + 250f).OrigToVehicleMap(this), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                matrix = default;
                matrix.SetTRS(new Vector3(size.x / 2f, 0f, -250f).OrigToVehicleMap(this), quat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);

                s = Vector3.one;
                foreach (var c in this.CachedStructureCells)
                {
                    matrix.SetTRS(c.ToVector3Shifted().OrigToVehicleMap(), quat, s);
                    Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref this.interiorMap, "interiorMap");
            Scribe_Values.Look(ref this.allowsHaulIn, "allowsHaulIn");
            Scribe_Values.Look(ref this.allowsHaulOut, "allowsHaulOut");
            Scribe_Values.Look(ref this.autoGetOff, "autoGetOff");
        }

        protected override void PostLoad()
        {
            base.PostLoad();
            if (!this.Spawned)
            {
                this.CompVehicleTurrets?.InitTurrets();
            }
        }

        private Map interiorMap;

        public Vector3 cachedDrawPos = Vector3.zero;
        
        private readonly List<IntVec3> interactionCellsInt = new List<IntVec3>();

        private bool allowsHaulIn = true;

        private bool allowsHaulOut = true;

        private bool autoGetOff = true;

        private HashSet<IntVec3> structureCellsCache;

        public bool structureCellsDirty;

        private static int lastCachedTick = -1;

        private static readonly Material ClipMat = SolidColorMaterials.NewSolidColorMaterial(new Color(0.3f, 0.1f, 0.1f, 0.5f), ShaderDatabase.MetaOverlay);

        //private static readonly float ClipAltitude = AltitudeLayer.WorldClipper.AltitudeFor();

        private static readonly Texture2D iconAllowsHaulIn = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/AllowsHaulIn");

        private static readonly Texture2D iconAllowsHaulOut = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/AllowsHaulOut");

        private static readonly Texture2D iconIncreasePriority = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/IncreasePriority");

        private static readonly Texture2D iconDecreasePriority = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/DecreasePriority");

        private static readonly Texture2D iconAutoGetOff = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/AutoGetOff");

        private static readonly Type t_SectionLayer_Zones = AccessTools.TypeByName("Verse.SectionLayer_Zones");
    }
}