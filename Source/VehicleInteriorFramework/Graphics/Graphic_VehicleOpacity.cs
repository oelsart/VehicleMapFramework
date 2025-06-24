using SmashTools;
using System;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class Graphic_VehicleOpacity : Graphic_Vehicle
    {
        public float Opacity
        {
            get
            {
                return this.opacityInt;
            }
            set
            {
                this.opacityInt = value;
                this.Notify_OpacityChanged();
            }
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            Log.Warning(string.Format("Retrieving {0} Colored Graphic from vanilla GraphicDatabase which will result in redundant graphic creation.", base.GetType()));
            return GraphicDatabase.Get<Graphic_VehicleOpacity>(this.path, newShader, this.drawSize, newColor, newColorTwo, this.DataRgb, null);
        }

        private void Notify_OpacityChanged()
        {
            if (!this.materials.NullOrEmpty())
            {
                foreach (var mat in this.materials)
                {
                    mat?.SetFloat("_Opacity", this.opacityInt);
                }
            }
        }

        private float opacityInt = 1f;
    }
}
