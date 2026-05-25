using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class Graphic_LinkedCornerOverlaySingle : Graphic_Linked
{

  public readonly Graphic_Single overlayGraphic;

  public Graphic_LinkedCornerOverlaySingle(Graphic subGraphic) : base(subGraphic)
  {
    this.subGraphic = subGraphic;
    data = subGraphic.data;
    overlayGraphic = GraphicDatabase.Get<Graphic_Single>(data.cornerOverlayPath, Shader, drawSize, subGraphic.color) as Graphic_Single;
  }

  public override void Print(SectionLayer layer, Thing thing, float extraRotation)
  {
    base.Print(layer, thing, extraRotation);
    var position = thing.Position;
    if (ShouldLinkWith(position + IntVec3.East, thing) && ShouldLinkWith(position + IntVec3.North, thing) && ShouldLinkWith(position + IntVec3.NorthEast, thing))
    {
      var mat = overlayGraphic.MatSingleFor(thing);
      TryGetTextureAtlasReplacementInfo(mat, TextureAtlasGroup.Building, false, false, out mat, out var uvs, out _);
      var rot = -VehicleSectionLayerManager.RotForPrint.AsAngle;
      Printer_Plane.PrintPlane(layer, thing.TrueCenter() + VehicleMapUtility.RotateForPrintNegate(new Vector3(0.5f, 0.1f, 0.5f)), Vector3.one, mat, rot, false, uvs);
    }
  }

  public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
  {
    return new Graphic_LinkedCornerOverlaySingle(subGraphic.GetColoredVersion(newShader, newColor, newColorTwo))
    {
      data = data
    };
  }
}
