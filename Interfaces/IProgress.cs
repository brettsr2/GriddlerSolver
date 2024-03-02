using System;

namespace Griddler_Solver
{
  interface IProgress
  {
    public abstract void AddMessage(String message);
    public abstract void AddDebugMessage(String message);
    public abstract void Completed();
    public abstract void ProgressWindowClosed();
  }
}
