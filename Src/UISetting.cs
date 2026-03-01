using System;
using System.Configuration;

namespace Griddler_Solver
{
  internal class UISetting : ConfigurationSection
  {
    [ConfigurationProperty("CheckBoxMultithread", DefaultValue = true)]
    public Boolean CheckBoxMultithread
    {
      get
      {
        return (Boolean)this["CheckBoxMultithread"];
      }
      set
      {
        this["CheckBoxMultithread"] = value;
      }
    }

  }
}
