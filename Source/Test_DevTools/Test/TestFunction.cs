// Copyright (c) 2025 Phil
// Derived from DevTools - Modified and rewritten by OELS (2026)
// Licensed under the MIT License.

using System.Collections;
using System.Diagnostics;
using System.Reflection;
using DevTools;
using DevTools.Testing;
using UnityEngine.Assertions;

namespace VehicleMapFramework.Test_Logics;

[DebuggerDisplay("Name = {Name}")]
internal class TestFunction(ITestFixture fixture, MethodInfo method, MethodType methodType) : ITestFunction
{

  public Type Type => MethodInfo.DeclaringType;

  public object ExpectedResult { get; private set; }

  public ITestFixture Fixture { get; } = fixture;

  public MethodType MethodType { get; } = methodType;

  public ITestModule Module => Fixture.Module;

  public object[] Args { get; set; }

  public MetaDataContainer MetaData { get; } = new();

  public Status Status { get; set; }

  public MethodInfo MethodInfo { get; } = method;

  public string Name => MethodInfo.Name;

  object ITestFunction.ExpectedResult
  {
    get => ExpectedResult;
    set => ExpectedResult = value;
  }

  public void Execute(object instance)
  {
    try
    {
      var result = MethodInfo.Invoke(instance, Args);
      if (ExpectedResult != null)
      {
        Assert.AreEqual(ExpectedResult, result);
      }
    }
    catch (Exception ex)
    {
      Test.Fail(ex);
    }
  }

  public IEnumerator ExecuteRoutine(object instance)
  {
    Assert.AreEqual(MethodInfo.ReturnType, typeof(IEnumerator));
    var enumerator = (IEnumerator)MethodInfo.Invoke(instance, Args);
    while (true)
    {
      object current;
      try
      {
        if (!enumerator.MoveNext())
          break;

        current = enumerator.Current;
      }
      catch (Exception ex)
      {
        Test.Fail(ex);
        yield break;
      }
      yield return current;
    }
  }
}
