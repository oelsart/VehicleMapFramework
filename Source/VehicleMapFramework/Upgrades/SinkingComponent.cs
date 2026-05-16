// =====================================================================
// Copyright (c) 2019-2025 Phil
// This file is a derivative work based on Vehicle Framework / GraphicOverlay.cs
// Modified and restructured by OELS 2026.
// Released under the MIT License
// =====================================================================

using System;
using SmashTools.Rendering;
using UnityEngine;
using Vehicles;
using Vehicles.Rendering;
using Verse;

namespace VehicleMapFramework;

public class SinkingComponent(GraphicOverlay component, FlyingObject mote, Reactor_Sink reactor) : IParallelRenderer
{
    private PreRenderResults resultsComponent;
    private PreRenderResults resultsOverlay;
    
    bool IParallelRenderer.IsDirty { get; set; }
    
    public Graphic ColorOverlayGraphic
    {
        get
        {
            field ??= GraphicDatabase.Get<Graphic_Multi>(
                component.Graphic.path,
                ShaderDatabase.Silhouette,
                component.Graphic.drawSize,
                Color.white,
                Color.white);
            WaterColorPropertyBlock ??= new MaterialPropertyBlock();
            return field;
        }
    }
    
    private MaterialPropertyBlock WaterColorPropertyBlock { get; set; }

    public void DynamicDrawPhaseAt(DrawPhase phase, in TransformData transformData,
        bool forceDraw = false)
    {
        switch (phase)
        {
            case DrawPhase.EnsureInitialized:
                // Ensure meshes are cached beforehand
                for (var i = 0; i < 4; i++)
                    _ = ColorOverlayGraphic.MeshAt(new Rot4(i));
                break;
            case DrawPhase.ParallelPreDraw:
                resultsComponent = ParallelGetPreRenderResults(in transformData, forceDraw: forceDraw);
                resultsOverlay = CopyResultsToColorOverlay(resultsComponent);
                break;
            case DrawPhase.Draw:
                // Out of phase drawing must immediately generate pre-render results for valid data.
                if (!resultsComponent.valid)
                {
                    resultsComponent = ParallelGetPreRenderResults(in transformData, forceDraw: forceDraw);
                    resultsOverlay = CopyResultsToColorOverlay(resultsComponent);
                }
                Draw(in transformData);
                resultsComponent = default;
                resultsOverlay = default;
                break;
            default:
                throw new NotImplementedException();
        }
    }

    private PreRenderResults ParallelGetPreRenderResults(ref readonly TransformData transformData,
        bool forceDraw = false)
    {
        if (component.data.component is { MeetsRequirements: false })
        {
            // Skip rendering if health percent is below set amount for rendering
            return new PreRenderResults { valid = true, draw = false };
        }

        if (component.Graphic is Graphic_Rgb graphicRgb)
        {
            var extraRotation = component.Transform.rotation + component.data.rotation;
            var render =
                graphicRgb.ParallelGetPreRenderResults(in transformData, forceDraw: forceDraw,
                    thing: component.Vehicle, extraRotation: extraRotation);
            render.material = graphicRgb.MatAt(component.Vehicle.Rotation);
            render.position += component.Transform.position;
            return render;
        }
        return new PreRenderResults { valid = true, draw = true };
    }

    private PreRenderResults CopyResultsToColorOverlay(PreRenderResults results)
    {
        return results with
        {
            material = ColorOverlayGraphic.MatAt(component.Vehicle.Rotation),
            position = results.position.WithYOffset(0.001f)
        };
    }

    private void Draw(ref readonly TransformData _)
    {
        if (!resultsComponent.draw)
            return;

        var properties = WaterColorPropertyBlock;
        properties.Clear();
        var alpha = mote.Alpha;
        var material = resultsComponent.material;
        SetAlpha(material, properties, AdditionalShaderPropertyIDs.ColorOne, alpha);
        SetAlpha(material, properties, ShaderPropertyIDs.ColorTwo, alpha);
        SetAlpha(material, properties, AdditionalShaderPropertyIDs.ColorThree, alpha);
        Graphics.DrawMesh(resultsComponent.mesh, resultsComponent.position, resultsComponent.quaternion, material, 0, null, 0, properties);
        
        properties.SetColor(ShaderPropertyIDs.Color, reactor.overlayColor.WithAlpha(reactor.colorOverlayAlphaCurve.Evaluate(mote.AgeSecs / mote.def.mote.Lifespan)));
        Graphics.DrawMesh(resultsOverlay.mesh, resultsOverlay.position, resultsOverlay.quaternion, resultsOverlay.material, 0, null, 0, properties);
        return;


        static void SetAlpha(Material mat, MaterialPropertyBlock properties, int id, float alpha)
        {
            properties.SetColor(id, mat.GetColor(id).WithAlpha(alpha));
        }
    }
}
