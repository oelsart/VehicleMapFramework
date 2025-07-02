using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors;

public class CompPipeConnector : ThingComp
{
    public CompPipeConnector Pair { get; set; }

    public List<IPipeConnector> ConnectorComps
    {
        get
        {
            connectorComps ??= [.. parent.AllComps.OfType<IPipeConnector>()];
            return connectorComps;
        }
    }

    private Material PipeMat
    {
        get
        {
            if (pipeMat == null)
            {
                pipeMat = MaterialPool.MatFrom("VehicleInteriors/Things/PipeConnector/Pipe", ShaderDatabase.Cutout);
            }
            return pipeMat;
        }
    }

    private Graphic PipeEndGraphic
    {
        get
        {
            pipeEndGraphic ??= GraphicDatabase.Get<Graphic_Single>("VehicleInteriors/Things/PipeConnector/PipeEnd", ShaderDatabase.CutoutComplex);
            return pipeEndGraphic.GetColoredVersion(ShaderDatabase.CutoutComplex, parent.DrawColor, parent.DrawColorTwo);
        }
    }

    public override void CompTick()
    {
        base.CompTick();
        if (Find.TickManager.TicksGame % ticksInterval != 0 || selectedComp == null) return;

        if (parent.IsOnVehicleMapOf(out _))
        {
            if (Pair != null && !connectReq)
            {
                selectedComp.DisconnectedAction();
                Pair = null;
            }
            connectReq = false;
        }
        else
        {
            var flag = false;
            foreach (var c in parent.OccupiedRect().ExpandedBy(2).Cells)
            {
                if (!c.InBounds(parent.Map)) continue;

                if (c.TryGetVehicleMap(parent.Map, out var vehicle))
                {
                    var c2 = c.ToVehicleMapCoord(vehicle);
                    if (!c2.InBounds(vehicle.VehicleMap)) continue;

                    var connector = c2.GetFirstThingWithComp<CompPipeConnector>(vehicle.VehicleMap);
                    if (connector != null)
                    {
                        var compConnector = connector.GetComp<CompPipeConnector>();
                        if (compConnector.selectedComp?.Mod != selectedComp.Mod) continue;

                        if (selectedComp.ConnectCondition(compConnector))
                        {
                            compConnector.connectReq = true;
                            if (Pair != compConnector || compConnector.Pair != this)
                            {
                                Pair = compConnector;
                                compConnector.Pair = this;
                            }
                            selectedComp.ConnectedTickAction();
                            flag = true;
                            break;
                        }
                    }
                }
            }
            if (!flag)
            {
                Pair = null;
            }
        }
    }

    public override void PostDraw()
    {
        base.PostDraw();
        if (Pair != null && parent.IsOnVehicleMapOf(out _))
        {
            var y = AltitudeLayer.LightingOverlay.AltitudeFor() - 0.001f;
            var drawPosA = parent.DrawPos.WithY(y);
            var drawPosB = Pair.parent.DrawPos.WithY(y);
            var graphic = PipeEndGraphic;
            var angle = (drawPosB - drawPosA).AngleFlat();
            Graphics.DrawMesh(MeshPool.plane10, drawPosA, Quaternion.AngleAxis(angle, Vector3.up), graphic.MatSingle, 0);
            Graphics.DrawMesh(MeshPool.plane10, drawPosB, Quaternion.AngleAxis(angle + 180f, Vector3.up), graphic.MatSingle, 0);
            GenDraw.DrawLineBetween(drawPosA, drawPosB, -0.001f, PipeMat, 1f);
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        yield return new Command_Action
        {
            defaultLabel = "VMF_AssignPipeNet".Translate(),
            icon = selectedComp?.GizmoIcon ?? BaseContent.ClearTex,
            action = () =>
            {
                Find.WindowStack.Add(new FloatMenu([.. ConnectorComps.SelectMany(c => c.FloatMenuOptions.Select(f =>
                {
                    f.action += () =>
                    {
                        selectedComp = c;
                    };
                    return f;
                }))]));
            }
        };
    }

    public override string CompInspectStringExtra()
    {
        return (selectedComp as ThingComp)?.CompInspectStringExtra();
    }

    public override void PostExposeData()
    {
        var mod = selectedComp?.Mod;
        Scribe_Values.Look(ref mod, "selectedComp");
        selectedComp = ConnectorComps.FirstOrDefault(c => c.Mod == mod);
    }

    public IPipeConnector selectedComp;

    private Material pipeMat;

    private Graphic pipeEndGraphic;

    protected bool connectReq;

    private List<IPipeConnector> connectorComps;

    public const int ticksInterval = 30;

    public enum PipeMod
    {
        VanillaExpandedFramework,
        DubsBadHygiene,
        Rimefeller
    }
}
