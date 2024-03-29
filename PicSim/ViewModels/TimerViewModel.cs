﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;

namespace PicSim.ViewModels {
  class TimerViewModel : PropertyChangedBase{

    #region Fields

    private string _crystalFrequency;
    private string _runtime;

    #endregion //Fields

    #region Properties

    public string CrystalFrequency {
      get {
        return _crystalFrequency;
      }

      set {
        _crystalFrequency = value;
        NotifyOfPropertyChange(() => CrystalFrequency);
      }
    }

    public string Runtime {
      get {
        return _runtime;
      }

      set {
        _runtime = value;
        NotifyOfPropertyChange(() => Runtime);
      }
    }

    #endregion //Properties

    #region Constructors

    public TimerViewModel() {
      Runtime = "0";
      CrystalFrequency = "4";
    }

    #endregion //Constructors

    #region Methods

    public void RefreshTime(int cycles) {
      try {
        double frequency = (Convert.ToDouble(CrystalFrequency));
        Runtime = Tools.CalculateRuntime(cycles, frequency).ToString();
      }
      catch {
        Runtime = "NaN";
      }
    }

    #endregion //Methods

  }
}
