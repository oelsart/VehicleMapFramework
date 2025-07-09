using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class Graphic_VehicleOpacity : Graphic_Vehicle
{
    public float Opacity
    {
        get
        {
            return opacityInt;
        }
        set
        {
            opacityInt = value;
            Notify_OpacityChanged();
        }
    }

    public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
    {
        Log.Warning(string.Format("Retrieving {0} Colored Graphic from vanilla GraphicDatabase which will result in redundant graphic creation.", base.GetType()));
        return GraphicDatabase.Get<Graphic_VehicleOpacity>(path, newShader, drawSize, newColor, newColorTwo, DataRgb, null);
    }

    private void Notify_OpacityChanged()
    {
        if (!materials.NullOrEmpty())
        {
            foreach (var mat in materials)
            {
                mat?.SetFloat("_Opacity", opacityInt);
            }
        }
    }

    private float opacityInt = 1f;
}
