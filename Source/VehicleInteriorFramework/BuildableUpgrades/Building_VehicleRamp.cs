using RimWorld;
using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class Building_VehicleRamp : Building_Door
    {
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var rot = this.BaseFullRotation();
            //マップ端オフセット
            VehicleMapProps mapProps;
            if (this.HasComp<CompVehicleEnterSpot>() && this.IsOnNonFocusedVehicleMapOf(out var vehicle) && (mapProps = vehicle.VehicleDef.GetModExtension<VehicleMapProps>()) != null)
            {
                drawLoc += rot.Opposite.AsVector2.ToVector3() * mapProps.EdgeSpaceValue(vehicle.FullRotation, this.Rotation.Opposite);
            }

            var moverGraphic = this.def.building.upperMoverGraphic.Graphic;
            var openPct = base.OpenPct;
            var facingVect = rot.FacingCell.ToVector3();
            var offset = rot.IsDiagonal ? facingVect * 0.35355339f : facingVect / 2f;
            var drawPos = drawLoc - offset - offset * openPct;
            facingVect.x = -facingVect.x;
            if (rot.IsDiagonal) drawPos += (facingVect.z < 0f ? facingVect : -facingVect) * 0.35355339f;
            else if (rot.IsHorizontal) drawPos.z -= 0.5f;
            var scale = rot == Rot8.North || rot == Rot8.South ? new Vector3(1f, 1f, openPct) : new Vector3(openPct, 1f, 1f);
            Graphics.DrawMesh(moverGraphic.MeshAt(rot), Matrix4x4.TRS(drawPos, Quaternion.AngleAxis(rot.AsRotationAngle, Vector3.up), scale), moverGraphic.MatAt(rot, this), 0);
        }
    }
}