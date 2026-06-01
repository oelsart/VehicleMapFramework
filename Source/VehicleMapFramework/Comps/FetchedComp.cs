using System;
using Verse;

namespace VehicleMapFramework;

public class FetchedComp<T>(ThingWithComps parent, Type compClass = null) where T : ThingComp
{
  private bool fetched;

  public T Value
  {
    get
    {
      if (!fetched)
      {
        field = compClass is not null ? GetComp(compClass) : parent.GetComp<T>();
        fetched = true;
      }
      return field;
    }
  }
  
  public T GetComp(Type type)
  {
    foreach (var comp in parent.AllComps)
    {
      if (comp.props.compClass.SameOrSubclassOf(type) && comp is T t)
        return t;
    }
    return null;
  }
}