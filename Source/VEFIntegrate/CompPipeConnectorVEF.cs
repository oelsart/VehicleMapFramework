using HarmonyLib;
using PipeSystem;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class CompPipeConnectorVEF : CompResource, IPipeConnector
    {
        public CompPipeConnector CompPipeConnector
        {
            get
            {
                if (compPipeConnector == null)
                {
                    if (!parent.TryGetComp(out compPipeConnector))
                    {
                        Log.Error($"[VehicleMapFramework] CompPipeConnector not found with {parent.LabelCap}.");
                    }
                }
                return compPipeConnector;
            }
        }

        public Texture GizmoIcon
        {
            get
            {
                return pipeNet?.pipeDefs?.FirstOrDefault()?.uiIcon;
            }
        }

        public IEnumerable<FloatMenuOption> FloatMenuOptions
        {
            get
            {
                return DefDatabase<PipeNetDef>.AllDefs.Select(d =>
                {
                    return new FloatMenuOption(d.resource.name, () =>
                    {
                        this.pipeNet = d;
                        this.parent.DrawColor = d.resource.color;
                        this.PipeNet.UnregisterComp(this);
                        this.PipeNetManager.RegisterConnector(this);
                        pipeNetCount(this.PipeNetManager) = this.PipeNetManager.pipeNets.Count;

                    });
                });
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (Find.TickManager.TicksGame % CompPipeConnector.ticksInterval != 0 || this.PipeNet == null) return;

            if (this.parent.IsOnVehicleMapOf(out _))
            {
                if (CompPipeConnector.Pair != null && !this.connectReq)
                {
                    var pipeNetManager = MapComponentCache<PipeNetManager>.GetComponent(this.parent.Map);
                    var newConnectors = this.PipeNet.connectors.Where(c => c.parent.Map == this.parent.Map);
                    if (!pipeNetManager.pipeNets.Remove(this.PipeNet))
                    {
                        pipeNetCount(pipeNetManager)++;
                    }
                    this.PipeNet = PipeNetMaker.MakePipeNet(newConnectors, this.parent.Map, this.pipeNet);
                    pipeNetManager.pipeNets.Add(this.PipeNet);
                    if (CompPipeConnector.Pair.parent.TryGetComp<CompPipeConnectorVEF>(out var pairComp))
                    {
                        foreach (var connector in newConnectors.ToArray())
                        {
                            pairComp.PipeNet.UnregisterComp(connector);
                        }
                    }
                    CompPipeConnector.Pair = null;
                }
                this.connectReq = false;
            }   
            else
            {
                var flag = false;
                foreach (var c in this.parent.OccupiedRect().ExpandedBy(1).Cells)
                {
                    if (!c.InBounds(this.parent.Map)) continue;

                    if (c.TryGetVehicleMap(this.parent.Map, out var vehicle))
                    {
                        var c2 = c.ToVehicleMapCoord(vehicle);
                        if (!c2.InBounds(vehicle.VehicleMap)) continue;

                        var connector = c2.GetFirstThingWithComp<CompPipeConnectorVEF>(vehicle.VehicleMap);
                        if (connector != null)
                        {
                            var compConnector = connector.GetComp<CompPipeConnectorVEF>();
                            if (compConnector.pipeNet == this.pipeNet)
                            {
                                compConnector.connectReq = true;
                                if (this.PipeNet != compConnector.PipeNet)
                                {
                                    CompPipeConnector.Pair = compConnector.CompPipeConnector;
                                    compConnector.CompPipeConnector.Pair = CompPipeConnector;
                                    var pipeNet = compConnector.PipeNet;
                                    for (var i = 0; i < pipeNet.connectors.Count; i++)
                                    {
                                        this.PipeNet.RegisterComp(pipeNet.connectors[i]);
                                    }
                                    compConnector.PipeNet = this.PipeNet;
                                    pipeNet.Destroy();
                                    var component = MapComponentCache<PipeNetManager>.GetComponent(compConnector.parent.Map);
                                    pipeNetCount(component) = component.pipeNets.Count;
                                    this.parent.DirtyMapMesh(this.parent.Map);
                                }
                                flag = true;
                                break;
                            }
                        }
                    }
                }
                if (!flag)
                {
                    CompPipeConnector.Pair = null;
                }
            }
        }

        public static readonly AccessTools.FieldRef<PipeNetManager, int> pipeNetCount = AccessTools.FieldRefAccess<PipeNetManager, int>("pipeNetsCount");

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref this.pipeNet, "pipeNetDef");
        }

        public PipeNetDef pipeNet = DefDatabase<PipeNetDef>.GetNamed("VMF_UnassignedNet");

        private CompPipeConnector compPipeConnector;

        protected bool connectReq;
    }
}