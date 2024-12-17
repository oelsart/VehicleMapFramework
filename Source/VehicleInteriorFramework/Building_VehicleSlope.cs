using RimWorld;
using SmashTools;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class Building_VehicleSlope : Building_Door
    {
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var moverGraphic = this.def.building.upperMoverGraphic.Graphic;
            var rot = this.BaseFullRotationOfThing();
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