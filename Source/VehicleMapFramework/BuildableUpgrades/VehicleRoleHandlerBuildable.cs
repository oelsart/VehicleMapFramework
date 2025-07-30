using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Rendering;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehicleRoleHandlerBuildable : VehicleRoleHandler, IExposable, IThingHolderWithDrawnPawn, IParallelRenderer
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
            return vehicle.DrawPos.y + this.role.PawnRenderer.LayerFor(rot).YOffset();
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
            return this.role.PawnRenderer.AngleFor(rot) + vehicle.Transform.rotation;
        }
    }

    PawnPosture IThingHolderWithDrawnPawn.HeldPawnPosture
    {
        get
        {
            return PawnPosture.LayingInBedFaceUp;
        }
    }

    void IParallelRenderer.DynamicDrawPhaseAt(DrawPhase phase, in TransformData transformData, bool forceDraw)
    {
        DynamicDrawPhaseAt(phase, in transformData, forceDraw);
    }

#pragma warning disable IDE0060 // 未使用のパラメーターを削除します
    new public void DynamicDrawPhaseAt(DrawPhase phase, in TransformData transformData, bool forceDraw = false)
#pragma warning restore IDE0060 // 未使用のパラメーターを削除します
    {
        foreach (Pawn item in thingOwner)
        {
            Rot4 value = role.PawnRenderer.RotFor(transformData.orientation);
            Vector3 vector = role.PawnRenderer.DrawOffsetFor(transformData.orientation).RotatedBy(transformData.orientation == Rot8.West ? -transformData.rotation : transformData.rotation);
            item.Drawer.renderer.DynamicDrawPhaseAt(phase, transformData.position + vector, value, neverAimWeapon: true);
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
