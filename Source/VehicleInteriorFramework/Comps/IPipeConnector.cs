using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public interface IPipeConnector
    {
        Texture GizmoIcon { get; }

        IEnumerable<FloatMenuOption> FloatMenuOptions { get; }
    }
}
