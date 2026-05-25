using System.Collections.Generic;
using System.Linq;
using DubsBadHygiene;
using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class CompPipeConnectorDBH : CompPipe, IPipeConnector
{

  private const float flowAmount = 3f;

  private static readonly ThingDef sewagePipeStuff = DefDatabase<ThingDef>.GetNamed("sewagePipeStuff");

  private static readonly ThingDef airPipe = DefDatabase<ThingDef>.GetNamed("airPipe");

  public new PipeType mode;

  private CompPipeConnectorDBH pairComp;

  public bool pumpUp = true;

  public new CompProperties_PipeConnectorDBH Props => (CompProperties_PipeConnectorDBH)props;

  public CompPipeConnector CompPipeConnector
  {
    get
    {
      if (field == null)
      {
        if (!parent.TryGetComp(out field))
        {
          Log.Error($"[VehicleMapFramework] CompPipeConnector not found with {parent.LabelCap}.");
        }
      }
      return field;
    }
  }

  private CompPipeConnectorDBH PairComp
  {
    get
    {
      if (pairComp == null)
      {
        pairComp = CompPipeConnector.Pair?.parent.TryGetComp<CompPipeConnectorDBH>();
      }
      return pairComp;
    }
  }

  public CompPipeConnector.PipeMod Mod => CompPipeConnector.PipeMod.DubsBadHygiene;

  public IEnumerable<FloatMenuOption> FloatMenuOptions
  {
    get
    {
      yield return new FloatMenuOption(sewagePipeStuff.LabelCap,
        () =>
        {
          parent.DrawColor = new Color(200f, 200f, 200f);
          mode = PipeType.Sewage;
        });
      //yield return new FloatMenuOption(airPipe.LabelCap, () =>
      //{
      //    this.parent.DrawColor = new Color(110f, 110f, 110f);
      //    mode = PipeType.Air;
      //});
    }
  }

  public Texture GizmoIcon => mode == PipeType.Air ? airPipe.uiIcon : sewagePipeStuff.uiIcon;

  public bool ConnectCondition(CompPipeConnector another)
  {
    return another.parent.TryGetComp<CompPipeConnectorDBH>(out var compPipeConnectorDBH) && mode == compPipeConnectorDBH.mode;
  }

  public void ConnectedTickAction()
  {
    if (PairComp is not { parent.Spawned: true })
    {
      pumpUp = pumpUp || pairComp.pumpUp;
      if (pumpUp)
      {
        var water = Mathf.Min(flowAmount * CompPipeConnector.TicksInterval, pipeNet.WaterStorage, pairComp.pipeNet.WaterTowers.Sum(w => w.space));
        pipeNet.PullWater(water, out _);
        pairComp.pipeNet.PushWater(water);
        var temp = pipeNet.HotWaterTanks.Empty() ? 0f : pipeNet.HotWaterTanks.Average(h => h.HeaterTemp);
        var pairTemp = pairComp.pipeNet.HotWaterTanks.Empty() ? 0f : pairComp.pipeNet.HotWaterTanks.Average(h => h.HeaterTemp);
        pairComp.pipeNet.HotWaterTanks.ForEach(h => h.HeaterTemp += (temp - pairTemp) * water / pairComp.pipeNet.WaterStorage);
      }
      else
      {
        var water = Mathf.Min(flowAmount * CompPipeConnector.TicksInterval, pipeNet.WaterTowers.Sum(w => w.space), pairComp.pipeNet.WaterStorage);
        pipeNet.PushWater(water);
        pairComp.pipeNet.PullWater(water, out _);
        var temp = pipeNet.HotWaterTanks.Empty() ? 0f : pipeNet.HotWaterTanks.Average(h => h.HeaterTemp);
        var pairTemp = pairComp.pipeNet.HotWaterTanks.Empty() ? 0f : pairComp.pipeNet.HotWaterTanks.Average(h => h.HeaterTemp);
        pipeNet.HotWaterTanks.ForEach(h => h.HeaterTemp += (pairTemp - temp) * water / pipeNet.WaterStorage);
      }
    }
  }

  public void DisconnectedAction()
  {
    pairComp = null;
  }

  public override IEnumerable<Gizmo> CompGetGizmosExtra()
  {
    foreach (var gizmo in base.CompGetGizmosExtra())
    {
      yield return gizmo;
    }

    if (CompPipeConnector.selectedComp == this && mode != PipeType.Air)
    {
      yield return new Command_ToggleIcon
      {
        defaultLabel = "VMF_PumpUp".Translate(),
        labelTwo = "VMF_Drain".Translate(),
        icon = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/PumpUp"),
        iconTwo = ContentFinder<Texture2D>.Get("VehicleMapFramework/UI/Drain"),
        isActive = () => pumpUp,
        toggleSound = SoundDefOf.Checkbox_TurnedOn,
        toggleAction = () =>
        {
          pumpUp = !pumpUp;
          if (PairComp != null)
          {
            pairComp.pumpUp = pumpUp;
          }
        }
      };
    }
  }

  public override void PostExposeData()
  {
    base.PostExposeData();
    Scribe_Values.Look(ref mode, "mode");
    Scribe_Values.Look(ref pumpUp, "pumpUp");
  }
}
