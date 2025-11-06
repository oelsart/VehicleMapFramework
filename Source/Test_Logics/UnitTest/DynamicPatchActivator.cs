namespace VehicleMapFramework.Test_Logics;

public readonly struct DynamicPatchActivator : IDisposable
{
    private readonly bool dynamicPatchEnabled = VehicleMapFramework.settings.dynamicPatchEnabled;
    
    private readonly bool dynamicUnPatchEnabled = VehicleMapFramework.settings.dynamicUnpatchEnabled;
    
    public DynamicPatchActivator()
    {
        VehicleMapFramework.settings.dynamicPatchEnabled = true;
        VehicleMapFramework.settings.dynamicUnpatchEnabled = true;
    }

    void IDisposable.Dispose()
    {
        VehicleMapFramework.settings.dynamicPatchEnabled = dynamicPatchEnabled;
        VehicleMapFramework.settings.dynamicUnpatchEnabled = dynamicUnPatchEnabled;
    }
}