using System;

namespace TLink.Core.MVU;

public interface IAction
{
    string Type { get; }
    DateTime Timestamp { get; }
}