using System;
using System.Runtime.CompilerServices;

namespace VehicleMapFramework;

public static class SingleParam
{
    [ThreadStatic] private static object[] singleParam;
    
    public static object[] Get(object param)
    {
        singleParam ??= new object[1];
        singleParam[0] = param;
        return singleParam;
    }
}

public class Params<T> where T : struct, ITuple
{
    // ReSharper disable once StaticMemberInGenericType
    [ThreadStatic] private static object[] @params;

    public static object[] Get(T tuple)
    {
        @params ??= new object[tuple.Length]; 
        for (var i = 0; i < tuple.Length; i++)
            @params[i] = tuple[i];
        return @params;
    }
}