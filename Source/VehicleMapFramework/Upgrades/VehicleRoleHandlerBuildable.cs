using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Rendering;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehicleRoleHandlerBuildable : VehicleRoleHandler, IExposable, IThingHolderWithDrawnPawn, IParallelRenderer
{
    private static readonly AccessTools.FieldRef<VehicleRoleHandler, string> roleKey = AccessTools.FieldRefAccess<VehicleRoleHandler, string>("roleKey");

    float IThingHolderWithDrawnPawn.HeldPawnDrawPos_Y
    {
        get
        {
            Rot8 rot;
            if (this.role is VehicleRoleBuildable roleBuildable)
            {
                rot = roleBuildable.upgradeComp.parent.BaseFullRotation();
            }
            else
            {
                rot = vehicle.FullRotation;
            }
            return vehicle.DrawPos.y + AltitudeLayer.BuildingOnTop.AltitudeFor().YOffset() + this.role.PawnRenderer.LayerFor(rot);
        }
    }

    float IThingHolderWithDrawnPawn.HeldPawnBodyAngle
    {
        get
        {
            Rot8 rot;
            if (this.role is VehicleRoleBuildable roleBuildable)
            {
                rot = roleBuildable.upgradeComp.parent.BaseFullRotation();
            }
            else
            {
                rot = vehicle.FullRotation;
            }
            return this.role.PawnRenderer.AngleFor(rot) + vehicle.Transform.rotation;
        }
    }

    PawnPosture IThingHolderWithDrawnPawn.HeldPawnPosture => PawnPosture.LayingInBedFaceUp;

    void IParallelRenderer.DynamicDrawPhaseAt(DrawPhase phase, in TransformData transformData, bool forceDraw)
    {
        DynamicDrawPhaseAt(phase, in transformData, forceDraw);
    }

    public new void DynamicDrawPhaseAt(DrawPhase phase, in TransformData transformData, bool forceDraw = false)
    {
        foreach (var item in thingOwner)
        {
            var value = role.PawnRenderer.RotFor(transformData.orientation);
            var vector = role.PawnRenderer.DrawOffsetFor(transformData.orientation).RotatedBy(transformData.orientation == Rot8.West ? -transformData.rotation : transformData.rotation);
            item.Drawer.renderer.DynamicDrawPhaseAt(phase, transformData.position + vector, value, neverAimWeapon: true);
        }
    }


    public VehicleRoleHandlerBuildable()
    {
        thingOwner ??= new ThingOwner<Pawn>(this, false);
    }

    public VehicleRoleHandlerBuildable(VehiclePawn vehicle) : this()
    {
        uniqueID = VehicleIdManager.Instance.GetNextHandlerId();
        this.vehicle = vehicle;
    }

    public VehicleRoleHandlerBuildable(VehiclePawn vehicle, VehicleRoleBuildable role) : this(vehicle)
    {
        this.role = role;
        roleKey(this) = role.key;
    }

    public new void ExposeData()
    {
        Scribe_Values.Look(ref uniqueID, "uniqueID", -1);
        Scribe_References.Look(ref vehicle, "vehicle", true);
        Scribe_Values.Look(ref roleKey(this), "role", null, true);
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            ThingOwner owner = this.thingOwner;
            var pawn = this.thingOwner.InnerListForReading.FirstOrDefault();
            owner.contentsLookMode = (pawn != null && pawn.IsWorldPawn()) ? LookMode.Reference : LookMode.Deep;
        }
        Scribe_Deep.Look(ref thingOwner, "thingOwner", this);
        if (Scribe.mode != LoadSaveMode.ResolvingCrossRefs) return;
        role = new VehicleRole
        {
            key = $"{roleKey(this)}_INVALID",
            label = $"{roleKey(this)} (INVALID)"
        };
        role.AddUpgrade(new VehicleUpgrade.RoleUpgrade
        {
            key = role.key,
            label = role.label,
            handlingTypes = HandlingType.Movement,
        });
    }
}
