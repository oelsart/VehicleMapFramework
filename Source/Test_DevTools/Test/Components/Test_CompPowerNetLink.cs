using DevTools.Testing;
using RimWorld;
using SmashTools;
using UnityEngine;
using Verse;
using UnityEngine.Assertions;
using Vehicles.Testing;

namespace VehicleMapFramework.Test_Logics;

[TestFixture(TestType.Playing)]
internal class Test_CompPowerNetLink : IGenericTest
{
    public required VehicleGroup Group { get; set; }
    IGenericTest Test => this;
    
    private CompWirelessTransmitter sourceLink;
    private CompWirelessTransmitter sinkLink;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Test.SetGroup();
        var def = DefDatabase<ThingDef>.GetNamed("VMF_WirelessTransmitter");
        var sourceThing = ThingMaker.MakeThing(def);
        var sinkThing = ThingMaker.MakeThing(def);

        GenSpawn.Spawn(sourceThing, IntVec3.NorthEast.ToBaseMapCoord(Test.Vehicle) + IntVec3.West, Test.Map);
        GenSpawn.Spawn(sinkThing, IntVec3.NorthEast, Test.VehicleMap);
        sourceLink = sourceThing.TryGetComp<CompWirelessTransmitter>();
        sinkLink = sinkThing.TryGetComp<CompWirelessTransmitter>();
        sourceLink.SetMaxPush(10000f);
        sinkLink.SetMaxPush(10000f);
        sourceLink.PowerOn = true;
        sinkLink.PowerOn = true;
        Test.Map.powerNetManager.UpdatePowerNetsAndConnections_First();
        Test.VehicleMap.powerNetManager.UpdatePowerNetsAndConnections_First();
        Expect.IsNotNull(sourceLink, "Source thing should have CompWirelessTransmitter.");
        Expect.IsNotNull(sinkLink, "Sync thing should have CompWirelessTransmitter.");
        Expect.IsNotNull(sourceLink.PowerNet, "Source link should be part of a PowerNet.");
        Expect.IsNotNull(sinkLink.PowerNet, "Sync link should be part of a PowerNet.");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        sourceLink.parent.Destroy();
        sinkLink.parent.Destroy();
        Test.DisposeGroup();
    }

    [TearDown]
    public void TearDown()
    {
        sourceLink.Disconnect();
        Assert.IsFalse(sourceLink.Connected);
        Assert.IsFalse(sinkLink.Connected);
    }

    [Test]
    public void Test_Connection()
    {
        sourceLink.SetMode(CompPowerNetLink.PowerTransferMode.Transmit);
        sinkLink.SetMode(CompPowerNetLink.PowerTransferMode.Transmit);
        EnsureTicks(sourceLink);
        Expect.IsTrue(sourceLink.Connected && sinkLink.Connected, "Links should connect when in range and modes are compatible.");
        sourceLink.Disconnect();
        Expect.IsFalse(sourceLink.Connected || sinkLink.Connected, "Links should disconnect when manually disconnected.");
        
        EnsureTicks(sinkLink);
        Expect.IsTrue(sourceLink.Connected && sinkLink.Connected, "Links should reconnect after being disconnected if conditions are met.");
        sourceLink.Disconnect();
        Expect.IsFalse(sourceLink.Connected || sinkLink.Connected, "Links should disconnect when manually disconnected.");
    }

    [Test]
    public void Test_DistanceDisconnection()
    {
        sourceLink.SetMode(CompPowerNetLink.PowerTransferMode.Transmit);
        sinkLink.SetMode(CompPowerNetLink.PowerTransferMode.Transmit);

        var pos = Test.Vehicle.Position;
        try
        {
            Test.Vehicle.Position += IntVec3.East * (Mathf.CeilToInt(sourceLink.Props.radius + 5));
            Test.Vehicle.DrawTracker.tweener.ResetTweenedPosToRoot();
            Test.Vehicle.DoTick();
            Test.VehicleMap.GetCachedMapComponent<VehiclePawnWithMapCache>().ForceResetDrawPosCache();
            
            sourceLink.Connect(sinkLink);
            EnsureTicks(sourceLink);
            Expect.IsFalse(sourceLink.Connected || sinkLink.Connected, "A: Links should disconnect when out of range.");

            sinkLink.Connect(sourceLink);
            Expect.IsTrue(sourceLink.Connected && sinkLink.Connected, "Links should reconnect");
            EnsureTicks(sinkLink);
            Expect.IsFalse(sourceLink.Connected || sinkLink.Connected, "B: Links should disconnect when out of range.");
        }
        finally
        {
            Test.Vehicle.Position = pos;
            Test.Vehicle.DrawTracker.tweener.ResetTweenedPosToRoot();
            Test.Vehicle.DoTick();
            Test.VehicleMap.GetCachedMapComponent<VehiclePawnWithMapCache>().ForceResetDrawPosCache();
        }
    }

    [Test]
    public void Test_PowerTransfer_Push_Calculations()
    {
        sourceLink.SetMode(CompPowerNetLink.PowerTransferMode.Push);
        sinkLink.SetMode(CompPowerNetLink.PowerTransferMode.Draw);

        // Source側に発電機を置く
        var generator = AddGenerator(sourceLink.PowerNet);
        using var scope = new ScopeEntity(generator);
        
        // Sink側にバッテリーを置く
        using var scope2 = new ScopeEntity(AddBattery(sinkLink.PowerNet));
        sourceLink.Connect(sinkLink);
        
        // 送電実行
        EnsureTicks(sourceLink);

        // 期待値:
        // ワノメトリック発電機の発電量: 1000f
        const float output = 1000f;
        Expect.AreApproximatelyEqual(-output, sourceLink.PowerOutput, "Source should output negative (consuming from its net)");

        EnsureTicks(sinkLink);
        Expect.AreApproximatelyEqual(output * sourceLink.Props.powerLossFactor, sinkLink.PowerOutput, "Sink should receive positive (supplying to its net)");
        
        sourceLink.Disconnect();
        sourceLink.Connect(sinkLink);
        
        // 逆側からも確認
        EnsureTicks(sinkLink);
        Expect.AreApproximatelyEqual(-output, sourceLink.PowerOutput, "Source should output negative (consuming from its net)");
        EnsureTicks(sourceLink);
        Expect.AreApproximatelyEqual(output * sourceLink.Props.powerLossFactor, sinkLink.PowerOutput, "Sink should receive positive (supplying to its net)");
    }

    [Test]
    public void Test_PowerTransfer_Transmit_Batteries()
    {
        sourceLink.SetMode(CompPowerNetLink.PowerTransferMode.Transmit);
        sinkLink.SetMode(CompPowerNetLink.PowerTransferMode.Transmit);
        
        // A: 1000W 蓄電, B: 0W 蓄電
        using var scope = new ScopeEntity(AddBattery(sourceLink.PowerNet, 1f));
        using var scope2 = new ScopeEntity(AddBattery(sinkLink.PowerNet));

        sourceLink.Connect(sinkLink);
        EnsureTicks(sourceLink);
        EnsureTicks(sinkLink);

        Expect.AreApproximatelyEqual(sourceLink.PowerOutput, 0f, "バッテリーだけでは送電は発生しない");

        const float consume = 175f;
        using var scope3 = new ScopeEntity(AddPowerConsumer(sinkLink.PowerNet, consume));
        EnsureTicks(sourceLink);
        Expect.AreApproximatelyEqual(sourceLink.PowerOutput, -consume / sourceLink.Props.powerLossFactor, "A: ヒーターの消費分PowerOutputが発生");
        EnsureTicks(sinkLink);
        Expect.AreApproximatelyEqual(sinkLink.PowerOutput, consume, "B: ヒーターの消費分PowerOutputが発生");
    }

    [Test]
    public void Test_DeficitHandling_Transmit()
    {
        sourceLink.SetMode(CompPowerNetLink.PowerTransferMode.Transmit);
        sinkLink.SetMode(CompPowerNetLink.PowerTransferMode.Transmit);

        // 両方のネットワークが電力不足（負圧）の場合
        // A: -500W (不足大), B: -100W (不足小)
        using var scope = new ScopeEntity(AddPowerConsumer(sourceLink.PowerNet, 500f));
        using var scope2 = new ScopeEntity(AddPowerConsumer(sinkLink.PowerNet, 100f));
        using var scope3 = new ScopeEntity(AddBattery(sourceLink.PowerNet, 0.5f));
        using var scope4 = new ScopeEntity(AddBattery(sinkLink.PowerNet, 0.6f));

        sourceLink.Connect(sinkLink);
        EnsureTicks(sourceLink);
        EnsureTicks(sinkLink);

        // 余裕がある方（B）から、より困っている方（A）へ電力が流れるべき
        Expect.LessThan(sinkLink.PowerOutput, 0f, "Less deficit net should help more deficit net.");
        Expect.GreaterThan(sourceLink.PowerOutput , 0f, "More deficit net should receive help.");
    }

    #region Helpers

    private static void EnsureTicks(CompWirelessTransmitter comp)
    {
        var init = GenTicks.TicksGame;
        for (var i = 0; i < comp.UpdateRateIntervalTicks + 1; i++)
        {
            using var _ = new MockGameTicks(init + i);
            comp.CompTick();
        }
    }
    
    private static Thing AddBattery(PowerNet net, float energyPct = 0f)
    {
        var thing = ThingMaker.MakeThing(ThingDefOf.Battery);
        GenPlace.TryPlaceThing(thing, net.transmitters.First().parent.Position, net.Map, ThingPlaceMode.Near);
        var comp = thing.TryGetComp<CompPowerBattery>();
        comp.SetStoredEnergyPct(energyPct);
        net.Map.powerNetManager.UpdatePowerNetsAndConnections_First();
        return thing;
    }

    private static Thing AddGenerator(PowerNet net)
    {
        var thing = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("VanometricPowerCell"));
        GenPlace.TryPlaceThing(thing, net.transmitters.First().parent.Position, net.Map, ThingPlaceMode.Near);
        net.Map.powerNetManager.UpdatePowerNetsAndConnections_First();
        return thing;
    }

    private static Thing AddPowerConsumer(PowerNet net, float consumption)
    {
        var thing = ThingMaker.MakeThing(ThingDefOf.Heater);
        GenPlace.TryPlaceThing(thing, net.transmitters.First().parent.Position, net.Map, ThingPlaceMode.Near);
        net.Map.powerNetManager.UpdatePowerNetsAndConnections_First();
        var comp = thing.TryGetComp<CompPowerTrader>();
        comp.PowerOutput = -consumption;
        return thing;
    }

    #endregion
}
