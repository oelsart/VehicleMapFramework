using PipeSystem;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleInteriors;

public class SectionLayer_ResourceOnVehicle : SectionLayer_ThingsOnVehicle
{
    private static int lastFrameDraw;

    private static PipeNetDef pipeNet;

    public virtual bool ShouldDraw => lastFrameDraw + 1 >= Time.frameCount;

    public SectionLayer_ResourceOnVehicle(Section section)
        : base(section)
    {
        requireAddToMapMesh = false;
        relevantChangeTypes = (ulong)MapMeshFlagDefOf.Buildings | 0x1C7;
    }

    public static void UpdateAndDrawFor(PipeNetDef pipeNetDef)
    {
        if (pipeNetDef != pipeNet)
        {
            pipeNet = pipeNetDef;
            Find.CurrentMap.mapDrawer.WholeMapChanged(455uL);
        }

        lastFrameDraw = Time.frameCount;
    }

    public override void DrawLayer()
    {
        if (ShouldDraw)
        {
            base.DrawLayer();
        }
    }

    protected override void TakePrintFrom(Thing t)
    {
        if (!(t is ThingWithComps thingWithComps))
        {
            return;
        }

        List<ThingComp> allComps = thingWithComps.AllComps;
        if (allComps == null)
        {
            return;
        }

        for (int i = 0; i < allComps.Count; i++)
        {
            if (allComps[i] is CompResource compResource && compResource.Props.pipeNet == pipeNet)
            {
                compResource.CompPrintForResourceGrid(this);
                break;
            }
        }
    }
}
