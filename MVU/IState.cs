using System;

namespace TLink.Core.MVU;

public interface IState : ICloneable
{
    string Id { get; init; }
    long Version { get; init; }
}