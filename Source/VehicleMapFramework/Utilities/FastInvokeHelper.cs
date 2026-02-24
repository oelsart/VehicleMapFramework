namespace VehicleMapFramework;

public static class FastInvokeHelper
{
    private static readonly object[] singleParam = new object[1];
    
    public static object[] SingleParam(object param)
    {
        singleParam[0] = param;
        return singleParam;
    }
}