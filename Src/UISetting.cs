using System;
using System.Configuration;

namespace Griddler_Solver
{
  internal class UISetting : ConfigurationSection
  {
    [ConfigurationProperty("CheckBoxScoreSorting", DefaultValue = true)]
    public Boolean CheckBoxScoreSorting
    {
      get
      {
        return (Boolean)this["CheckBoxScoreSorting"];
      }
      set
      {
        this["CheckBoxScoreSorting"] = value;
      }
    }

    [ConfigurationProperty("CheckBoxPermutaionAnalysis", DefaultValue = true)]
    public Boolean CheckBoxPermutaionAnalysis
    {
      get
      {
        return (Boolean)this["CheckBoxPermutaionAnalysis"];
      }
      set
      {
        this["CheckBoxPermutaionAnalysis"] = value;
      }
    }

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

    [ConfigurationProperty("CheckBoxPermutationsLimit", DefaultValue = true)]
    public Boolean CheckBoxPermutationsLimit
    {
      get
      {
        return (Boolean)this["CheckBoxPermutationsLimit"];
      }
      set
      {
        this["CheckBoxPermutationsLimit"] = value;
      }
    }

    [ConfigurationProperty("CheckBoxStaticAnalysis", DefaultValue = true)]
    public Boolean CheckBoxStaticAnalysis
    {
      get
      {
        return (Boolean)this["CheckBoxStaticAnalysis"];
      }
      set
      {
        this["CheckBoxStaticAnalysis"] = value;
      }
    }

    [ConfigurationProperty("CheckBoxStepMode", DefaultValue = false)]
    public Boolean CheckBoxStepMode
    {
      get
      {
        return (Boolean)this["CheckBoxStepMode"];
      }
      set
      {
        this["CheckBoxStepMode"] = value;
      }
    }
  }
}
