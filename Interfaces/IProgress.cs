using System;

namespace Griddler_Solver
{
  internal interface IProgress
  {
    public abstract void AddMessage(String message);
    public abstract void Completed();
  }
}
