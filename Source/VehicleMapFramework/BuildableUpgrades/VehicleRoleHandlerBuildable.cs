using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehicleRoleHandlerBuildable : VehicleRoleHandler, IExposable, IThingHolderWithDrawnPawn
{
    float IThingHolderWithDrawnPawn.HeldPawnDrawPos_Y
    {
        get
        {
            Rot8 rot;
            if (this.role is VehicleRoleBuildable role)
            {
                rot = role.upgradeComp.parent.BaseFullRotation();
            }
            else
            {
                rot = vehicle.FullRotation;
            }
            return vehicle.DrawPos.y + this.role.PawnRenderer.LayerFor(rot);
        }
    }

    float IThingHolderWithDrawnPawn.HeldPawnBodyAngle
    {
        get
        {
            Rot8 rot;
            if (this.role is VehicleRoleBuildable role)
            {
                rot = role.upgradeComp.parent.BaseFullRotation();
            }
            else
            {
                rot = vehicle.FullRotation;
            }
            return this.role.PawnRenderer.AngleFor(rot);
        }
    }

    PawnPosture IThingHolderWithDrawnPawn.HeldPawnPosture
    {
        get
        {
            return PawnPosture.LayingInBedFaceUp;
        }
    }

    public VehicleRoleHandlerBuildable()
    {
        thingOwner ??= new ThingOwner<Pawn>(this, false, LookMode.Deep);
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

    new public void ExposeData()
    {
        Scribe_Values.Look<int>(ref uniqueID, "uniqueID", -1, false);
        Scribe_References.Look<VehiclePawn>(ref vehicle, "vehicle", true);
        Scribe_Values.Look<string>(ref roleKey(this), "role", null, true);
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            ThingOwner thingOwner = this.thingOwner;
            Pawn pawn = this.thingOwner.InnerListForReading.FirstOrDefault<Pawn>();
            thingOwner.contentsLookMode = (pawn != null && pawn.IsWorldPawn()) ? LookMode.Reference : LookMode.Deep;
        }
        Scribe_Deep.Look(ref thingOwner, "thingOwner", this);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            role = new VehicleRole
            {
                key = roleKey(this) + "_INVALID",
                label = roleKey(this) + " (INVALID)"
            };
            role.AddUpgrade(new VehicleUpgrade.RoleUpgrade()
            {
                key = role.key,
                label = role.label,
                handlingTypes = HandlingType.Movement,
            });
        }
    }

    private static readonly AccessTools.FieldRef<VehicleRoleHandler, string> roleKey = AccessTools.FieldRefAccess<VehicleRoleHandler, string>("roleKey");
}
