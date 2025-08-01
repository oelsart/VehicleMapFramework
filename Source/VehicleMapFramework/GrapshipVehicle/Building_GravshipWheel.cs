using HarmonyLib;
using RimWorld;
using SmashTools;
using System.Collections.Generic;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework
{
    public class Building_GravshipWheel : Building, IAttackTarget
    {
        [Unsaved(false)]
        private CompGravshipFacility gravshipGravshipFacility;

        private bool flipped;

        private Rot4? tmpRot;

        private static readonly AccessTools.FieldRef<Thing, Rot4> rotationInt = AccessTools.FieldRefAccess<Rot4>(typeof(Thing), "rotationInt");

        public Thing Thing => this;

        public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;

        public float TargetPriorityFactor => 0.2f;

        public bool ThreatDisabled(IAttackTargetSearcher disabledFor) => !this.IsOnVehicleMapOf(out _) || !ValidFor(Rot4.North);

        //車上でPrintする時のオフセット
        public override Vector3 DrawPos
        {
            get
            {
                if (Map?.GetCachedMapComponent<VehiclePawnWithMapCache>()?.cacheMode ?? false)
                {
                    var drawPos = base.DrawPos;
                    if (tmpRot is null) return drawPos;
                    if (!ValidFor(Rot4.North)) return drawPos;

                    drawPos += new Vector3(0f, 0f, -0.2f).RotatedBy(VehicleMapUtility.RotForPrintCounter);
                    var offset = new Vector3(DrawSize.x * 0.07f, 0f, 0f);
                    if (VehicleMapUtility.RotForPrint.IsVertical || tmpRot == VehicleMapUtility.RotForPrint)
                    {
                        offset.y -= Altitudes.AltInc * 250f;
                    }
                    if (tmpRot == Rot4.West)
                    {
                        offset.x = -offset.x;
                    }
                    drawPos += offset;
                    return drawPos;
                }
                return base.DrawPos;
            }
        }

        public CompGravshipFacility CompGravshipFacility
        {
            get
            {
                gravshipGravshipFacility ??= GetComp<CompGravshipFacility>();
                return gravshipGravshipFacility;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;

            if (!this.IsOnVehicleMapOf(out var vehicle))
            {
                yield return new Command_Toggle()
                {
                    defaultLabel = "VMF_VehicleMode".Translate(),
                    icon = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/GravshipVehicleMode"),
                    toggleAction = GenerateGravshipVehicle,
                    isActive = () => false,
                };
            }
            else if (vehicle.Spawned && vehicle.def.HasModExtension<VehicleMapProps_Gravship>())
            {
                yield return new Command_Toggle()
                {
                    defaultLabel = "VMF_VehicleMode".Translate(),
                    icon = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/GravshipVehicleMode"),
                    toggleAction = () => PlaceGravship(vehicle),
                    isActive = () => true
                };
            }

            if (vehicle == null || !ValidFor(Rot4.North))
            {
                var des = BuildCopyCommandUtility.FindAllowedDesignator(def);
                yield return new Command_FlipBuilding()
                {
                    defaultLabel = "VMF_Flip".Translate(),
                    action = () =>
                    {
                        flipped = !flipped;
                        DirtyMapMesh(Map);
                    },
                    icon = des?.ResolvedIcon(StyleDef),
                    iconProportions = des?.iconProportions ?? default,
                    iconDrawScale = des?.iconDrawScale ?? default,
                    iconTexCoords = des?.iconTexCoords ?? default,
                    iconAngle = des?.iconAngle ?? default,
                    iconOffset = des?.iconOffset ?? default,
                    commandIcon = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/FlipIcon")
                };
            }
        }

        public void GenerateGravshipVehicle()
        {
            var report = GravshipVehicleUtility.GenerateGravshipVehicle(CompGravshipFacility?.engine);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
            }
        }

        public void PlaceGravship(VehiclePawnWithMap vehicle)
        {
            var report = GravshipVehicleUtility.PlaceGravshipVehicle(CompGravshipFacility?.engine, vehicle);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            var engine = CompGravshipFacility?.engine;
            var onVehicle = this.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned;
            base.DeSpawn(mode);
            AcceptanceReport report;
            if (onVehicle && engine is not null && !GravshipVehicleUtility.GravshipProcessInProgress && !(report = GravshipVehicleUtility.CheckGravshipVehicleStability(engine, Rot4.North, out _)).Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.NegativeEvent);
                LongEventHandler.QueueLongEvent(() =>
                {
                    GravshipVehicleUtility.PlaceGravshipVehicle(engine, vehicle, true);
                }, "VMF_GravshipVehicleDestroyed".Translate(), false, null, false);
            }
        }

        public override void Print(SectionLayer layer)
        {
            var pos = Position;
            tmpRot = Rotation;
            if (flipped)
            {
                rotationInt(this) = tmpRot.Value.Opposite;
                SetPositionDirect(pos + new IntVec3(1 - def.Size.x % 2, 0, 1 - def.Size.z % 2).RotatedBy(tmpRot.Value));
            }
            base.Print(layer);
            if (flipped)
            {
                rotationInt(this) = tmpRot.Value;
                SetPositionDirect(pos);
            }
            tmpRot = null;
        }

        public bool ValidFor(Rot4 rot)
        {
            return (tmpRot ?? Rotation) == rot.Rotated(flipped ? RotationDirection.Clockwise : RotationDirection.Counterclockwise);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref flipped, "flipped");
        }
    }
}
