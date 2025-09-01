using System;

namespace ModuleKit.MVU;

public interface IState : ICloneable
{
    string Id { get; init; }
    long Version { get; init; }
}