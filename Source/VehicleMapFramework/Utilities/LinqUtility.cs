using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace VehicleMapFramework;

public static class LinqUtility
{
  extension<T>([ItemCanBeNull] IEnumerable<T> enumerable) where T : class
  {
    [ItemNotNull] public IEnumerable<T> NonNull => enumerable.Where(x => x is not null)!;
  }
  
  extension<T>([ItemCanBeNull] IEnumerable<T?> enumerable) where T : struct
  {
    public IEnumerable<T> NonNull => enumerable.OfType<T>();
  }
}