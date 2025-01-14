using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class Graphic_LinkedCornerOverlaySingle : Graphic_Linked
    {
        public Graphic_LinkedCornerOverlaySingle(Graphic subGraphic) : base(subGraphic)
        {
            this.subGraphic = subGraphic;
            this.data = subGraphic.data;
            this.overlayGraphic = GraphicDatabase.Get<Graphic_Single>(this.data.cornerOverlayPath, this.Shader, this.drawSize, subGraphic.color) as Graphic_Single;
        }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
        {
            base.Print(layer, thing, extraRotation);
            IntVec3 position = thing.Position;
            if (this.ShouldLinkWith(position + IntVec3.East, thing) && this.ShouldLinkWith(position + IntVec3.North, thing) && this.ShouldLinkWith(position + IntVec3.NorthEast, thing))
            {
                Material mat = this.overlayGraphic.MatSingleFor(thing);
                Graphic.TryGetTextureAtlasReplacementInfo(mat, TextureAtlasGroup.Building, false, false, out mat, out Vector2[] uvs, out _);
                var rot = -VehicleMapUtility.rotForPrint.AsAngle;
                Printer_Plane.PrintPlane(layer, thing.TrueCenter() + VehicleMapUtility.RotateForPrintNegate(new Vector3(0.5f, 0.1f, 0.5f)), Vector3.one, mat, rot, false, uvs, null, 0.01f, 0f);
            }
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            return new Graphic_LinkedCornerOverlaySingle(this.subGraphic.GetColoredVersion(newShader, newColor, newColorTwo))
            {
                data = this.data
            };
        }

        public Graphic_Single overlayGraphic;
    }
}