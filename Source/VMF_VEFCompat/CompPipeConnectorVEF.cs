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
        public CompPipeConnector.PipeMod Mod => CompPipeConnector.PipeMod.VanillaExpandedFramework;

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

        private CompPipeConnectorVEF PairComp
        {
            get
            {
                if (pairComp == null)
                {
                    CompPipeConnector.Pair?.parent.TryGetComp(out pairComp);
                }
                return pairComp;
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

        public bool ConnectCondition(CompPipeConnector another)
        {
            return another.parent.TryGetComp<CompPipeConnectorVEF>(out var CompPipeConnectorVEF) && this.pipeNet == CompPipeConnectorVEF.pipeNet;
        }

        public void ConnectedTickAction()
        {
            if (PairComp != null && this.PipeNet != pairComp.PipeNet)
            {
                var pipeNet = pairComp.PipeNet;
                for (var i = 0; i < pipeNet.connectors.Count; i++)
                {
                    this.PipeNet.RegisterComp(pipeNet.connectors[i]);
                }
                pairComp.PipeNet = this.PipeNet;
                pipeNet.Destroy();
                var component = MapComponentCache<PipeNetManager>.GetComponent(pairComp.parent.Map);
                pipeNetCount(component) = component.pipeNets.Count;
                this.parent.DirtyMapMesh(this.parent.Map);
            }
        }

        public void DisconnectedAction()
        {
            var pipeNetManager = MapComponentCache<PipeNetManager>.GetComponent(this.parent.Map);
            var newConnectors = this.PipeNet.connectors.Where(c => c.parent.Map == this.parent.Map);
            if (!pipeNetManager.pipeNets.Remove(this.PipeNet))
            {
                pipeNetCount(pipeNetManager)++;
            }
            this.PipeNet = PipeNetMaker.MakePipeNet(newConnectors, this.parent.Map, this.pipeNet);
            pipeNetManager.pipeNets.Add(this.PipeNet);
            if (PairComp != null)
            {
                foreach (var connector in newConnectors.ToArray())
                {
                    pairComp.PipeNet.UnregisterComp(connector);
                }
                pairComp = null;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref this.pipeNet, "pipeNetDef");
        }

        public PipeNetDef pipeNet = DefDatabase<PipeNetDef>.GetNamed("VMF_UnassignedNet");

        private CompPipeConnector compPipeConnector;

        private CompPipeConnectorVEF pairComp;

        public static readonly AccessTools.FieldRef<PipeNetManager, int> pipeNetCount = AccessTools.FieldRefAccess<PipeNetManager, int>("pipeNetsCount");
    }
}