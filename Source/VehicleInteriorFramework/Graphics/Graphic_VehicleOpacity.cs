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
            return GraphicDatabase.Get<Graphic_VehicleOpacity>(this.path, newShader, this.drawSize, newColor, newColorTwo, this.DataRGB, null);
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

        public override void DrawWorker(Vector3 loc, Rot8 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            if (this.Opacity == 0f) return;

            //VehicleGraphicOverlay.RenderGraphicOverlaysではなくGraphicOverlay.Drawが使われてるのでAsRotationAngleの調整が為されてない
            var num = -rot.AsRotationAngle;
            if (num != 0f) num++;
            base.DrawWorker(loc, rot, thingDef, thing, extraRotation + num);
        }

        private float opacityInt = 1f;
    }
}
