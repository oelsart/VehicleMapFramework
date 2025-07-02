using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleInteriors;

public interface IPipeConnector
{
    CompPipeConnector.PipeMod Mod { get; }

    Texture GizmoIcon { get; }

    IEnumerable<FloatMenuOption> FloatMenuOptions { get; }

    bool ConnectCondition(CompPipeConnector another);

    void ConnectedTickAction();

    void DisconnectedAction();
}
