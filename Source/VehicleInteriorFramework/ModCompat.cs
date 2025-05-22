using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors
{
    [StaticConstructorOnStartup]
    public static class ModCompat
    {
        public static class AdaptiveStorage
        {
            public static readonly bool Active = ModsConfig.IsActive("adaptive.storage.framework");

            public static readonly Type TransformData;

            public static readonly Type RotationAngle;

            public static readonly Type ThingClass;

            public static readonly FastInvokeHandler Renderer;

            public static readonly FastInvokeHandler SetAllPrintDatasDirty;

            static AdaptiveStorage()
            {
                if (Active)
                {
                    TransformData = AccessTools.TypeByName("ITransformable.TransformData");
                    RotationAngle = AccessTools.TypeByName("ITransformable.RotationAngle");
                    ThingClass = AccessTools.TypeByName("AdaptiveStorage.ThingClass");
                    Renderer = MethodInvoker.GetHandler(AccessTools.PropertyGetter(ThingClass, "Renderer"));
                    SetAllPrintDatasDirty = MethodInvoker.GetHandler(AccessTools.Method("AdaptiveStorage.StorageRenderer:SetAllPrintDatasDirty"));
                }
            }
        }

        public static readonly bool AllowTool = ModsConfig.IsActive("UnlimitedHugs.AllowTool");

        public static readonly bool BillDoorsFramework = ModsConfig.IsActive("3HSTltd.Framework");

        public static readonly bool BiomesCaverns = ModsConfig.IsActive("BiomesTeam.BiomesCaverns");

        public static readonly bool CallTradeShips = ModsConfig.IsActive("calltradeships.kv.rw");

        public static readonly bool CombatExtended = ModsConfig.IsActive("CETeam.CombatExtended");

        public static readonly bool ColonyGroups = ModsConfig.IsActive("DerekBickley.LTOColonyGroupsFinal");

        public static class DeepStorage
        {
            public static readonly bool Active = ModsConfig.IsActive("LWM.DeepStorage");

            public static readonly FastInvokeHandler ThingListToDisplay;

            static DeepStorage()
            {
                if (Active)
                {
                    ThingListToDisplay = MethodInvoker.GetHandler(AccessTools.Method("LWM.DeepStorage.PatchDisplay_SectionLayer_Things_Regenerate:ThingListToDisplay"));
                }
            }
        }

        public static readonly bool DeadMansSwitch = ModsConfig.IsActive("Aoba.DeadManSwitch.AncientCorps");

        public static readonly bool DrakkenLaserDrill = ModsConfig.IsActive("MYDE.DrakkenLaserDrill");

        public static readonly bool DrillTurret = ModsConfig.IsActive("Mlie.MiningCoDrillTurret");

        public static class DubsBadHygiene
        {
            public static readonly bool Active = ModsConfig.IsActive("Dubwise.DubsBadHygiene") || ModsConfig.IsActive("Dubwise.DubsBadHygiene.Lite");

            public static readonly Type SectionLayer_SewagePipeOverlay;

            public static readonly Type SectionLayer_AirDuctOverlay;

            public static readonly Type SectionLayer_Irrigation;

            public static readonly Type SectionLayer_FertilizerGrid;

            public static readonly Type Building_Pipe;

            public static FastInvokeHandler PrintForGrid;

            public static readonly Type CompProperties_Pipe;

            public static readonly AccessTools.FieldRef<object, int> CompProperties_Pipe_mode;

            public static readonly AccessTools.FieldRef<object, int> SectionLayer_PipeOverlay_mode;

            static DubsBadHygiene()
            {
                if (Active)
                {
                    SectionLayer_SewagePipeOverlay = AccessTools.TypeByName("DubsBadHygiene.SectionLayer_SewagePipeOverlay");
                    SectionLayer_AirDuctOverlay = AccessTools.TypeByName("DubsBadHygiene.SectionLayer_AirDuctOverlay");
                    SectionLayer_Irrigation = AccessTools.TypeByName("DubsBadHygiene.SectionLayer_Irrigation");
                    SectionLayer_FertilizerGrid = AccessTools.TypeByName("DubsBadHygiene.SectionLayer_FertilizerGrid");
                    Building_Pipe = AccessTools.TypeByName("DubsBadHygiene.Building_Pipe");
                    PrintForGrid = MethodInvoker.GetHandler(AccessTools.Method(Building_Pipe, "PrintForGrid"));
                    CompProperties_Pipe = AccessTools.TypeByName("DubsBadHygiene.CompProperties_Pipe");
                    CompProperties_Pipe_mode = AccessTools.FieldRefAccess<int>(CompProperties_Pipe, "mode");
                    SectionLayer_PipeOverlay_mode = AccessTools.FieldRefAccess<int>("DubsBadHygiene.SectionLayer_PipeOverlay:mode");
                }
            }
        }

        public static class Rimefeller
        {
            public static readonly bool Active = ModsConfig.IsActive("Dubwise.Rimefeller");

            public static readonly Type SectionLayer_SewagePipe;

            public static readonly Type SectionLayer_ThingsPipe;

            public static readonly Type XSectionLayer_Napalm;

            public static readonly Type XSectionLayer_OilSpill;

            public static readonly Type Building_Pipe;

            public static FastInvokeHandler PrintForGrid;

            public static readonly Type CompProperties_Pipe;

            public static readonly AccessTools.FieldRef<object, int> CompProperties_Pipe_mode;

            public static readonly AccessTools.FieldRef<object, int> SectionLayer_PipeOverlay_mode;

            static Rimefeller()
            {
                if (Active)
                {
                    SectionLayer_SewagePipe = AccessTools.TypeByName("Rimefeller.SectionLayer_SewagePipe");
                    SectionLayer_ThingsPipe = AccessTools.TypeByName("Rimefeller.SectionLayer_ThingsPipe");
                    XSectionLayer_Napalm = AccessTools.TypeByName("Rimefeller.XSectionLayer_Napalm");
                    XSectionLayer_OilSpill = AccessTools.TypeByName("Rimefeller.XSectionLayer_OilSpill");
                    Building_Pipe = AccessTools.TypeByName("Rimefeller.Building_Pipe");
                    PrintForGrid = MethodInvoker.GetHandler(AccessTools.Method(Building_Pipe, "PrintForGrid"));
                    CompProperties_Pipe = AccessTools.TypeByName("Rimefeller.CompProperties_Pipe");
                    CompProperties_Pipe_mode = AccessTools.FieldRefAccess<int>(CompProperties_Pipe, "mode");
                    SectionLayer_PipeOverlay_mode = AccessTools.FieldRefAccess<int>("Rimefeller.SectionLayer_PipeOverlay:mode");
                }
            }
        }

        public static class DefenseGrid
        {
            public static readonly bool Active = ModsConfig.IsActive("Aelanna.EccentricTech.DefenseGrid");

            public static readonly Type SectionLayer_DefenseGridOverlay;

            public static readonly Type CompDefenseConduit;

            public static readonly Type Designator_DeconstructConduit;

            static DefenseGrid()
            {
                if (Active)
                {
                    SectionLayer_DefenseGridOverlay = AccessTools.TypeByName("EccentricDefenseGrid.SectionLayer_DefenseGridOverlay");
                    CompDefenseConduit = AccessTools.TypeByName("EccentricDefenseGrid.CompDefenseConduit");
                    Designator_DeconstructConduit = AccessTools.TypeByName("EccentricDefenseGrid.Designator_DeconstructConduit");
                }
            }
        }

        public static readonly bool EnterHere = ModsConfig.IsActive("Mlie.EnterHere");

        public static readonly bool ExosuitFramework = ModsConfig.IsActive("Aoba.Exosuit.Framework");

        public static readonly bool GiantImperialTurret = ModsConfig.IsActive("XMB.Giantimperialcannonturret.MO");

        public static readonly bool Gunplay = ModsConfig.IsActive("automatic.gunplay");

        public static readonly bool MeleeAnimation = ModsConfig.IsActive("co.uk.epicguru.meleeanimation");

        public static readonly bool MiscRobots = ModsConfig.IsActive("Haplo.Miscellaneous.Robots");

        public static readonly bool MuzzleFlash = ModsConfig.IsActive("IssacZhuang.MuzzleFlash");

        public static readonly bool PathfindingFramework = ModsConfig.IsActive("pathfinding.framework");

        public static readonly bool ProjectRimFactory = ModsConfig.IsActive("spdskatr.projectrimfactory");

        public static readonly bool SmarterConstruction = ModsConfig.IsActive("dhultgren.smarterconstruction");

        public static readonly bool TabulaRasa = ModsConfig.IsActive("neronix17.toolbox");

        public static class VFECore
        {
            public static readonly bool Active = ModsConfig.IsActive("OskarPotocki.VanillaFactionsExpanded.Core");

            public static readonly Type SectionLayer_ResourceOnVehicle;

            public static readonly Type PipeNetDef;

            static VFECore()
            {
                if (Active)
                {
                    SectionLayer_ResourceOnVehicle = AccessTools.TypeByName("VehicleInteriors.SectionLayer_ResourceOnVehicle");
                    PipeNetDef = AccessTools.TypeByName("PipeSystem.PipeNetDef");
                }
            }
        }

        public static readonly bool VFEArchitect = ModsConfig.IsActive("VanillaExpanded.VFEArchitect");

        public static readonly bool VFESecurity = ModsConfig.IsActive("VanillaExpanded.VFESecurity");

        public static readonly bool VVE = ModsConfig.IsActive("OskarPotocki.VanillaVehiclesExpanded");

        public static readonly bool VFEPirates = ModsConfig.IsActive("OskarPotocki.VFE.Pirates");

        public static readonly bool VFEMechanoid = ModsConfig.IsActive("OskarPotocki.VFE.Mechanoid");

        public static readonly bool Vivi = ModsConfig.IsActive("gguake.race.vivi");

        public static readonly bool WhileYoureUp = ModsConfig.IsActive("CodeOptimist.JobsOfOpportunity");

        public static readonly bool YayosCombat3 = ModsConfig.IsActive("Mlie.YayosCombat3");

        public static class TakeItToStorage
        {
            public static readonly bool Active = ModsConfig.IsActive("legodude17.htsb");

            public delegate bool FindCellGetter(Pawn pawn, List<Thing> things, ref IntVec3 cell);

            public static readonly FindCellGetter FindCell;

            static TakeItToStorage()
            {
                if (Active)
                {
                    FindCell = AccessTools.MethodDelegate<FindCellGetter>("HaulToBuilding.Toils_Recipe_Patches:FindCell");
                }
            }
        }

        public static readonly bool NoJobAuthors = ModsConfig.IsActive("Doug.NoJobAuthors");

        public static class PickUpAndHaul
        {
            public static readonly bool Active = ModsConfig.IsActive("Mehni.PickUpAndHaul");

            public static readonly Func<RaceProperties, bool> IsAllowedRace;

            public static readonly WorkGiverDef HaulToInventory;

            static PickUpAndHaul()
            {
                if (Active)
                {
                    IsAllowedRace = AccessTools.MethodDelegate<Func<RaceProperties, bool>>("PickUpAndHaul.Settings:IsAllowedRace");
                    HaulToInventory = DefDatabase<WorkGiverDef>.GetNamed("HaulToInventory");
                }
            }
        }

        public static class EnergyShield
        {
            public static readonly bool Active = ModsConfig.IsActive("zhuzi.AdvancedEnergy.Shields");

            public static readonly Type Building_Shield;

            public static readonly bool CECompat;

            static EnergyShield()
            {
                if (Active)
                {
                    Building_Shield = AccessTools.TypeByName("zhuzi.AdvancedEnergy.Shields.Shields.Building_Shield");
                    CECompat = ModsConfig.IsActive("cn.zhuzijun.EnergyShieldCECompat");
                }
            }
        }

        public static readonly bool TraderShips = ModsConfig.IsActive("automatic.traderships");

        public static readonly bool NightmareCore = ModsConfig.IsActive("Nightmare.Core");

        public static readonly bool Aquariums = ModsConfig.IsActive("Nightmare.Aquariums");

        public static readonly bool SmartPistol = ModsConfig.IsActive("rabiosus.smartpistol");
    }
}
