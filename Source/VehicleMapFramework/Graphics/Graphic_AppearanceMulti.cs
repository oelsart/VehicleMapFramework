using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class Graphic_AppearanceMulti : Graphic_Appearances
{
    public override void Init(GraphicRequest req)
    {
        data = req.graphicData;
        path = req.path;
        color = req.color;
        drawSize = req.drawSize;
        var allDefsListForReading = DefDatabase<StuffAppearanceDef>.AllDefsListForReading;
        subGraphics = new Graphic[allDefsListForReading.Count];
        Graphic graphic = null;
        for (var i = 0; i < subGraphics.Length; i++)
        {
            var stuffAppearance = allDefsListForReading[i];
            var text = req.path;
            var last = text.Split('/').Last();
            if (!stuffAppearance.pathPrefix.NullOrEmpty())
            {
                text = text[..^last.Length] + stuffAppearance.pathPrefix + last;
            }
            var texture2D = ContentFinder<Texture2D>.Get(text + Graphic_Multi.NorthSuffix, false);
            if (texture2D)
            {
                subGraphics[i] = GraphicDatabase.Get<Graphic_Multi>(text, req.shader, drawSize, color);
                graphic ??= subGraphics[i];
            }
        }
        for (var num = 0; num < subGraphics.Length; num++)
        {
            subGraphics[num] ??= graphic;
        }
    }

    public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
    {
        if (newColorTwo != Color.white)
        {
            Log.ErrorOnce("Cannot use Graphic_AppearanceMulti.GetColoredVersion with a non-white colorTwo.", 9910251);
        }
        return GraphicDatabase.Get<Graphic_AppearanceMulti>(this.path, newShader, this.drawSize, newColor, Color.white, this.data);
    }
}