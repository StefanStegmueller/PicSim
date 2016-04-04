﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicSim.Models {
  class ProgramModel {

    #region Fields

    private Dictionary<int, int> _opcodes = new Dictionary<int, int>();
    private List<OperationModel> _operations = new List<OperationModel>();

    #endregion //Fields

    #region Properties

    public List<OperationModel> Operations
    {
      get
      {
        return _operations;
      }

      set
      {
        _operations = value;
      }
    }

    #endregion //Properties

    #region Constructors

    public ProgramModel(string filePath) {
      ParseFile(filePath);
      ObjectifyOPCodes();
    }

    #endregion //Constructors

    #region Methods

    private void ParseFile(string filePath) {
      int counter = 0;
      string line;

      // Read the file and display it line by line.
      System.IO.StreamReader file = new System.IO.StreamReader(filePath);
      while ((line = file.ReadLine()) != null) {
        char[] chars = line.ToCharArray();
        if (char.IsNumber(chars[0])) {
          _opcodes.Add(ParseOPCodeIndex(line), ParseOPCodeCommand(line));
        }
        counter++;
      }
      file.Close();
    }

    private int ParseOPCodeIndex(string line) {
      string subString;
      subString = line.Substring(0, 4);
      return Convert.ToInt32(subString);
    }

    private int ParseOPCodeCommand(string line) {
      string subString = line.Substring(5, 4);
      int hex = Convert.ToInt32(subString, 16);
      return hex;
    }

    private void ObjectifyOPCodes() {
      Operation[] sortedOpValues = SortEnumValues();
      foreach (KeyValuePair<int, int> opcode in _opcodes) {
        ObjectifyOperation(sortedOpValues, opcode);
      }
    }

    private Operation[] SortEnumValues() {
      Operation[] opValues = Enum.GetValues(typeof(Operation)).Cast<Operation>().ToArray();
      Array.Sort(opValues);
      Array.Reverse(opValues);
      return opValues;
    }

    private void ObjectifyOperation(Operation[] opValues, KeyValuePair<int, int> opcode) {
      foreach (Operation opMask in opValues) {
        if (HasFlag(opcode, opMask)) {
          Operations.Add(new OperationModel(opcode.Key, opMask));
          break;
        }
        Console.WriteLine("Unknown Operation");
      }
    }

    private bool HasFlag(KeyValuePair<int, int> opcode, Operation op) {
      if ((Convert.ToInt32(op) & opcode.Value) == Convert.ToInt32(op)) {
        return true;
      }
      else {
        return false;
      }
    }

    #endregion //Methods

  }
}
