using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompDrawAdditionalGraphicsOpacity : CompDrawAdditionalGraphics
{
    private float opacity = 1f;
    
    private MaterialPropertyBlock propertyBlock;
    
    public List<ThingWithComps> children = [];

    public float Opacity
    {
        set => opacity = value;
    }
    
    private CompProperties_DrawAdditionalGraphics Props => (CompProperties_DrawAdditionalGraphics)this.props;
    
    public CompDrawAdditionalGraphicsOpacity()
    {
        LongEventHandler.ExecuteWhenFinished((() =>
        {
            propertyBlock = new MaterialPropertyBlock();
        }));
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (var gizmo in base.CompGetGizmosExtra())
            yield return gizmo;
        if (Props.graphics.NullOrEmpty())
            yield break;
        
        var tex = Props.graphics[0].Graphic.MatSouth.mainTexture;
        var proportion = tex.height > tex.width
            ? new Vector2(tex.height / (float)tex.height, 1f)
            : new Vector2(1f, tex.height / (float)tex.width);

        yield return new Command_Action
        {
            defaultLabel = parent.LabelCap,
            icon = tex,
            iconProportions = proportion,
            action = () =>
            {
                var rect = new Rect(UI.MousePositionOnUIInverted - new Vector2(75f, 18f), new Vector2(150f, 33f));
                Find.WindowStack.Add(new EphemenalWindow
                {
                    windowRect = rect,
                    doWindowFunc = () =>
                    {
                        Widgets.DrawWindowBackground(rect.AtZero(), GUI.color);
                        opacity = VMF_Widgets.HorizontalSlider(
                            new Rect(0f, 15f, rect.width, rect.height), opacity, 0f, 1f, false, null,
                            "0%", "100%", -1, GUI.color);
                        opacity = Mathf.Round(opacity * 100f) / 100f;
                        
                        var comps = Find.Selector.SelectedObjects
                            .OfType<ThingWithComps>()
                            .SelectMany(t => t.GetComps<CompDrawAdditionalGraphicsOpacity>());
                        foreach (var comp in comps)
                        {
                            comp.opacity = opacity;
                        }
                    }
                });
            }
        };
    }

    public override void PostDraw()
    {
        if (opacity == 0f)
            return;
        
        foreach (var graphic in Props.graphics.Select(g => g.Graphic)
                     .Concat(children.SelectMany(c => c.GetComp<CompAdditionalGraphicsChild>().Graphics)))
        {
            var loc = parent.DrawPos;
            var rot = parent.BaseRotationVehicleDraw();
            var extraRotation = 0f;
            Patch_Graphic_Draw.Prefix(ref loc, ref rot, parent, ref extraRotation,
                graphic is Graphic_Appearances appearance ? appearance.SubGraphicFor(parent) : graphic);
            if (parent.IsOnVehicleMapOf(out var vehicle))
            {
                var angle = vehicle.ExtraAngle;
                extraRotation += angle;
                var offset = graphic.DrawOffset(rot);
                var offset2 = offset.RotatedBy(angle);
                loc += new Vector3(offset2.x - offset.x, 0f, offset2.z - offset.z);
            }
            
            var mesh = graphic.MeshAt(rot);
            var quaternion = graphic.QuatFromRot(rot);
            if (extraRotation != 0f)
            {
                quaternion *= Quaternion.Euler(Vector3.up * extraRotation);
            }
            if (graphic.data is { addTopAltitudeBias: true })
            {
                quaternion *= Quaternion.Euler(Vector3.left * 2f);
            }
            loc += graphic.DrawOffset(rot);
            var material = graphic.MatAt(rot, parent);
            loc.y += 0.01f;
            loc.y -= loc.z * 0.00001f;
            loc.y -= loc.x * 0.000001f;
            var drawColor = parent.DrawColor.WithAlpha(opacity);
            propertyBlock.SetColor(ShaderPropertyIDs.Color, drawColor);
            propertyBlock.SetColor(AdditionalShaderPropertyIDs.ColorOne, drawColor);
            propertyBlock.SetFloat(Graphic_VehicleOpacity.OpacityID, opacity);
            Graphics.DrawMesh(mesh, loc, quaternion, material, 0, null, 0, propertyBlock);
            graphic.ShadowGraphic?.DrawWorker(loc, rot, parent.def, parent, extraRotation);
        }
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        for (var i = children.Count - 1; i >= 0; i--)
        {
            var child = children[i];
            if (child.Spawned) child.DeSpawn(mode);
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref opacity, "opacity");
        Scribe_Collections.Look(ref children, "children", LookMode.Reference);
    }
}