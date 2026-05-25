using System;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public abstract class CompPowerNetLink : CompPowerTrader
{
    private const float BatteryDischargingWatts = 5f;

    public bool Connected => LinkedTo is not null && LinkedComp is not null;

    protected abstract float Radius { get; }
    
    protected abstract float MaxPowerPush { get; }
    
    protected abstract float PowerLossFactor { get; }
    
    private ThingWithComps linkedTo;
    
    protected ThingWithComps LinkedTo
    {
        get => linkedTo;
        set
        {
            linkedTo = value;
            LinkedComp = linkedTo?.TryGetComp<CompPowerNetLink>();
        }
    }

    public CompPowerNetLink LinkedComp { get; private set; }

    protected abstract PowerTransferMode Mode { get; }

    public virtual int UpdateRateIntervalTicks => Connected ? 30 : 180;
    
    public override void CompTick()
    {
        if (!parent.Spawned) return;
        base.CompTick();
        
        if (!parent.IsHashIntervalTick(UpdateRateIntervalTicks)) return;
        
        if (Connected)
        {
            if (parent.BaseMapOrCaravan != LinkedTo.BaseMapOrCaravan ||
                (LinkedTo.DrawPos - parent.DrawPos).MagnitudeHorizontalSquared() > Radius * Radius)
            {
                Disconnect();
                return;
            }
            
            var output = PowerOutput;
            switch (Mode)
            {
                case PowerTransferMode.Push:
                    var amount = PushAmount(this, LinkedComp);
                    PowerOutput = -amount;
                    break;
                case PowerTransferMode.Draw:
                    var amount2 = PushAmount(LinkedComp, this);
                    LinkedComp.PowerOutput = -amount2;
                    break;
                case PowerTransferMode.Transmit:
                    var amount3 = TransmitAmount(this, LinkedComp);
                    switch (amount3)
                    {
                        case > 0f:
                            PowerOutput = -amount3;
                            break;
                        case < 0f:
                            LinkedComp.PowerOutput = amount3;
                            break;
                        case 0f:
                            PowerOutput = 0f;
                            LinkedComp.PowerOutput = 0f;
                            break;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            if (PowerOutput < 0f && PowerOn)
            {
                LinkedComp.PowerOutput = -PowerOutput * PowerLossFactor;
            }
            
            if (output == 0f && PowerOutput != 0f || PowerOutput == 0f && output != 0f)
            {
                // UpdateLitのため
                parent.BroadcastCompSignal(PowerTurnedOnSignal);
            }
            return;
        }

        if (TryFindConnection(out var pair))
            Connect(pair);
    }

    public virtual bool CanLinkTo(CompPowerNetLink other)
    {
        return !Connected && !other.Connected && other.Mode == Mode switch
        {
            PowerTransferMode.Push => PowerTransferMode.Draw,
            PowerTransferMode.Draw => PowerTransferMode.Push,
            _ => PowerTransferMode.Transmit
        };
    }

    protected abstract bool TryFindConnection(out CompPowerNetLink linkTo);
    
    public virtual void Connect(CompPowerNetLink other)
    {
        LinkedTo = other.parent;
        other.LinkedTo = parent;
    }

    public virtual void Disconnect()
    {
        LinkedComp?.PowerOutput = 0f;
        LinkedComp?.LinkedTo = null;
        PowerOutput = 0f;
        LinkedTo = null;
        parent.BroadcastCompSignal(PowerTurnedOffSignal);
    }
    
    protected static float PushAmount(CompPowerNetLink pusher, CompPowerNetLink drawer)
    {
        var powerNet = drawer.PowerNet;
        var sumBatteriesDiscarge = powerNet.batteryComps.Count * BatteryDischargingWatts;
        var needs = PowerNetNeeds(drawer.PowerNet, drawer) + PowerNetBatteryAccepts(drawer.PowerNet) / WattsToWattDaysPerTick;
        var supply = Mathf.Max(0f, -PowerNetNeeds(pusher.PowerNet, pusher) + pusher.PowerNet.CurrentStoredEnergy() / WattsToWattDaysPerTick);
        var wantsToPush = Mathf.Clamp((needs / pusher.PowerLossFactor) + 1E-07f, sumBatteriesDiscarge, pusher.MaxPowerPush);
        return Mathf.Clamp(wantsToPush, 0f, supply);
    }

    protected static float TransmitAmount(CompPowerNetLink me, CompPowerNetLink other)
    {
        var powerNet = me.PowerNet;
        var powerNet2 = other.PowerNet;
        if (powerNet is null || powerNet2 is null) return 0f;

        var voltage = -PowerNetNeeds(powerNet, me);
        var stored = powerNet.CurrentStoredEnergy();
        var hasBattery = powerNet.batteryComps.Any(b => b.AmountCanAccept != 0f);
        var voltage2 = -PowerNetNeeds(powerNet2, other);
        var stored2 = powerNet2.CurrentStoredEnergy();
        var hasBattery2 = powerNet2.batteryComps.Any(b => b.AmountCanAccept != 0f);
        
        var diff = stored - stored2;
        
        switch (voltage)
        {
            case > 0f when voltage2 < 0f: // こっちが正圧むこうが負圧
                var needs = -voltage2 / me.PowerLossFactor;
                var num = voltage > needs // バッテリーを使わず供給可
                    ? Mathf.Min(diff > 0f
                        ? hasBattery2 ? voltage : needs
                        : needs, me.MaxPowerPush) // 過剰供給分は彼我のバッテリー差によってどっちが使うか決める
                    : Mathf.Min(diff > 0f
                        ? hasBattery2 ? needs : voltage
                        : voltage, me.MaxPowerPush); // 不足分はよりバッテリー量を持ってる方が補う
                me.DebugMessage($"正圧: voltage: {voltage}, voltage2: {voltage2}, needs: {needs}, num: {num}");
                return num;
            
            case < 0f when voltage2 > 0f: // こっちが負圧むこうが正圧
                var needs2 = -voltage / other.PowerLossFactor;
                var num2 = voltage2 > needs2 // むこうがバッテリーを使わず供給可
                    ? -Mathf.Min(diff > 0f
                        ? hasBattery ? voltage2 : needs2
                        : needs2, other.MaxPowerPush) // 過剰供給分は彼我のバッテリー差によってどっちが使うか決める
                    : -Mathf.Min(diff > 0f
                        ? hasBattery ? needs2 : voltage2
                        : voltage2, other.MaxPowerPush); // 不足分はよりバッテリー量を持ってる方が補う
                me.DebugMessage($"負圧: voltage: {voltage}, voltage2: {voltage2}, needs2: {needs2}, num2: {num2}");
                return num2;
            
            case > 0f when voltage2 > 0f:
                var num3 = diff > 0f // 両方正圧なら、よりバッテリー量を持ってる方が供給する（余剰分）
                    ? hasBattery2
                        ? Mathf.Clamp(voltage, 0f, me.MaxPowerPush) // こっちが供給
                        : -Mathf.Clamp(voltage2, 0f, other.MaxPowerPush) // むこうが供給
                    : hasBattery
                        ? -Mathf.Clamp(voltage2, 0f, other.MaxPowerPush) // むこうが供給
                        : Mathf.Clamp(voltage, 0f, me.MaxPowerPush); // こっちが供給
                me.DebugMessage($"両正圧: voltage: {voltage}, voltage2: {voltage2}, num3: {num3}");
                return num3;
                
            
            case <= 0f when voltage2 <= 0f:
                var needs3 = -voltage2 / me.PowerLossFactor;
                var needs4 = -voltage / other.PowerLossFactor;
                var num4 = diff > 0f // 両方負圧なら、よりバッテリー量を持ってる方が供給する（必要分）
                    ? Mathf.Clamp(needs3, 0f, me.MaxPowerPush) // こっちが供給
                    : diff < 0f
                        ? -Mathf.Clamp(needs4, 0f, other.MaxPowerPush) // むこうが供給
                        : Mathf.Max(Mathf.Min(needs3, me.MaxPowerPush), Mathf.Min(needs4, other.MaxPowerPush)); // バッテリー量が同じ場合需要を伝える
                me.DebugMessage($"両負圧: voltage: {voltage}, voltage2: {voltage2}, needs3: {needs3}, needs4: {needs4}, num4: {num4}");
                return num4;
        }
        return 0f;
    }

    private static float PowerNetNeeds(PowerNet powerNet, CompPowerTrader ignore = null)
    {
        var needs = 0f;
        foreach (var comp in powerNet.powerComps)
        {
            if (comp == ignore) continue;
            if (comp.PowerOn || FlickUtility.WantsToBeOn(comp.parent) && !comp.parent.IsBrokenDown())
            {
                needs -= comp.PowerOutput;
            }
        }
        return needs;
    }

    private static float PowerNetBatteryAccepts(PowerNet powerNet)
    {
        return powerNet.batteryComps.Sum(b => b.AmountCanAccept);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_References.Look(ref linkedTo, nameof(linkedTo));
        LinkedTo = linkedTo; // LinkedCompを初期化
    }

    [Conditional("DEBUG")]
    protected void DebugMessage(string message)
    {
        if ((parent.DrawPos - UI.MouseMapPosition()).MagnitudeHorizontalSquared() < 1.5f)
            Log.Message(message);
    }

    public enum PowerTransferMode
    {
        Push,
        Draw,
        Transmit
    }
}
