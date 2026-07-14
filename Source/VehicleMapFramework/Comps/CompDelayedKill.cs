using RimWorld;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompDelayedKill : VehicleComp
{
  private DestroyMode destroyMode;

  private Effecter effecter;
  private bool killTimerStarted;

  private bool spawnWreckage;

  private int ticksUntilKilled;

  public CompProperties_DelayedKill Props => (CompProperties_DelayedKill)props;

  public override bool TickByRequest => true;

  public bool KillOnTick => ticksUntilKilled <= 0;

  public bool KillStarted => killTimerStarted;

  public void StartKillTimer(DestroyMode _destroyMode, bool _spawnWreckage)
  {
    if (killTimerStarted) return;
    destroyMode = _destroyMode;
    spawnWreckage = _spawnWreckage;
    killTimerStarted = true;
    StartTicking();
    if (Props.message is not null)
      Messages.Message(Props.message.Translate(parent.LabelCap),
        parent.HostileTo(Faction.OfPlayer) ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.PawnDeath);
  }

  public override void Initialize(CompProperties _props)
  {
    base.Initialize(_props);
    ticksUntilKilled = Props.delayTicks;
  }

  public override void CompTick()
  {
    base.CompTick();
    ticksUntilKilled--;
    Vehicle.Transform.position += Props.moveVector / GenTicks.TicksPerRealSecond;
    if (KillOnTick)
    {
      Vehicle.Kill(null, destroyMode, spawnWreckage);
      effecter?.Cleanup();
      effecter = null;
      return;
    }

    effecter ??= Props.effecterDef?.Spawn(parent, parent.Map);
    effecter?.EffectTick(parent, parent);
  }

  public override void PostExposeData()
  {
    base.PostExposeData();
    Scribe_Values.Look(ref killTimerStarted, nameof(killTimerStarted));
    Scribe_Values.Look(ref ticksUntilKilled, nameof(ticksUntilKilled));
    Scribe_Values.Look(ref destroyMode, nameof(destroyMode));
    Scribe_Values.Look(ref spawnWreckage, nameof(spawnWreckage));
  }
}