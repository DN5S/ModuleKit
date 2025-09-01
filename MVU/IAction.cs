using System;

namespace ModuleKit.MVU;

public interface IAction
{
    string Type { get; }
    DateTime Timestamp { get; }
}