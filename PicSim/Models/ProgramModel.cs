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

    private List<OperationModel> _operations = new List<OperationModel>();
    private RamModel _ram;
    private int _progCounter;
    private int _cycles = 0;
    private int _timer = 0;
    private bool _tempRB0;
    private int _tempPortB;

    #endregion //Fields

    #region Properties

    public List<OperationModel> Operations {
      get {
        return _operations;
      }

      set {
        _operations = value;
      }
    }

    public int ProgCounter {
      get {
        return _progCounter;
      }

      set {
        _progCounter = value;
      }
    }

    public RamModel Ram {
      get {
        return _ram;
      }
      set {
        _ram = value;
      }
    }

    private int Timer {
      get {
        return _timer;
      }

      set {
        _timer = value;
        if (!Ram.DirectGetRegisterBit((int)SFR.TMR0, 3) && (_timer > 255 * CalcPrescaler())) {
          Ram.DirectToggleRegisterBit((int)SFR.INTCON, 2, true);
          _timer = 0;
        }
        else if (_timer > 255) {
          Ram.DirectToggleRegisterBit((int)SFR.INTCON, 2, true);
          _timer = 0;
        }
      }
    }

    public int Cycles {
      get {
        return _cycles;
      }

      set {
        _cycles = value;
        Timer = value;
      }
    }

    #endregion //Properties

    #region Constructors

    public ProgramModel(string filePath) {
      Dictionary<int, int> opcodes = ParseFile(filePath);
      ObjectifyOPCodes(opcodes);
      ProgCounter = 0;
      _ram = new RamModel();
    }

    #endregion //Constructors

    #region Methods

    private Dictionary<int, int> ParseFile(string filePath) {
      int counter = 0;
      string line;
      Dictionary<int, int> opcodes = new Dictionary<int, int>();

      // Read the file save OP-Codes
      System.IO.StreamReader file = new System.IO.StreamReader(filePath);
      while ((line = file.ReadLine()) != null) {
        char[] chars = line.ToCharArray();
        if (char.IsNumber(chars[0])) {
          opcodes.Add(ParseOPCodeIndex(line), ParseOPCodeCommand(line));
        }
        counter++;
      }
      file.Close();
      return opcodes;
    }

    private int ParseOPCodeIndex(string line) {
      string subString;
      subString = line.Substring(0, 4);
      return Convert.ToInt32(subString, 16);
    }

    private int ParseOPCodeCommand(string line) {
      string subString = line.Substring(5, 4);
      int hex = Convert.ToInt32(subString, 16);
      return hex;
    }

    private void ObjectifyOPCodes(Dictionary<int, int> opcodes) {
      Operation[] sortedOpValues = SortEnumValues();
      foreach (KeyValuePair<int, int> opcode in opcodes) {
        ObjectifyOperation(sortedOpValues, opcode);
        ObjectifyArgs(opcode.Value);
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
        if (HasMask(opcode.Value, opMask)) {
          Operations.Add(new OperationModel(opcode.Key, opMask));
          break;
        }
      }
    }

    private bool HasMask(int opcode, Operation op) {
      if ((Convert.ToInt32(op) & opcode) == Convert.ToInt32(op)) {
        return true;
      }
      else {
        return false;
      }
    }

    private void ObjectifyArgs(int opcode) {
      OperationModel opModel = Operations.Last();
      if (TypeHasFlag(new TypeList(OperationType.ByteOrientedFD).OpsOfType, opModel.Operation)) {
        ParseFDArgs(opcode, opModel);
        return;
      }
      if (TypeHasFlag(new TypeList(OperationType.ByteOrientedF).OpsOfType, opModel.Operation)) {
        ParseFArgs(opcode, opModel);
        return;
      }
      if (TypeHasFlag(new TypeList(OperationType.BitOriented).OpsOfType, opModel.Operation)) {
        ParseBFArgs(opcode, opModel);
        return;
      }
      if (TypeHasFlag(new TypeList(OperationType.LiteralControl).OpsOfType, opModel.Operation)) {
        ParseKArgs(opcode, opModel);
        return;
      }
      if (TypeHasFlag(new TypeList(OperationType.NoArgs).OpsOfType, opModel.Operation)) {
        opModel.OpType = OperationType.NoArgs;
        return;
      }
    }

    private void ParseFDArgs(int opcode, OperationModel opModel) {
      int intD = opcode & Convert.ToInt32(0x0080);
      BitArray byteD = new BitArray(new int[] { intD });
      int f = opcode & Convert.ToInt32(0x007F);
      CheckFSR(opModel, f);
      opModel.SetArgs(byteD[7], f);
      opModel.OpType = OperationType.ByteOrientedFD;
    }

    private void ParseFArgs(int opcode, OperationModel opModel) {
      int f = opcode & Convert.ToInt32(0x007F);
      CheckFSR(opModel, f);
      opModel.SetArgs(f);
      opModel.OpType = OperationType.ByteOrientedF;
    }

    private void ParseBFArgs(int opcode, OperationModel opModel) {
      int b = (opcode & Convert.ToInt32(0x0380)) / 0x80;
      int f = opcode & Convert.ToInt32(0x007F);
      CheckFSR(opModel, f);
      opModel.SetArgs(b, f);
      opModel.OpType = OperationType.BitOriented;
    }

    private void ParseKArgs(int opcode, OperationModel opModel) {
      int k;
      if (opModel.Operation == Operation.CALL || opModel.Operation == Operation.GOTO) {
        k = opcode & Convert.ToInt32(0x07FF);
      }
      else {
        k = opcode & Convert.ToInt32(0x00FF);
      }
      opModel.SetArgs(k);
      opModel.OpType = OperationType.LiteralControl;
    }

    private bool TypeHasFlag(List<Operation> ops, Operation op) {
      if (ops.Contains(op)) {
        return true;
      }
      else {
        return false;
      }
    }

    public void ExecuteCommand(int index) {
      OperationModel op = GetOpByIndex(index);
      UseFSRValue(op);
      CheckInterrupt();
      ChooseCommand(op);
      _progCounter++;
      Cycles++;
      _ram.DirectSetRegisterValue((int)SFR.PCL, _progCounter);
      _tempRB0 = Ram.DirectGetRegisterBit((int)SFR.PORTB, 0);
      _tempPortB = GetPortBInterruptPins();
    }

    private void CheckInterrupt() {
      bool GIE = Ram.DirectGetRegisterBit((int)SFR.INTCON, 7);
      bool EEIE = Ram.DirectGetRegisterBit((int)SFR.INTCON, 6);
      bool T0IE = Ram.DirectGetRegisterBit((int)SFR.INTCON, 5);
      bool INTE = Ram.DirectGetRegisterBit((int)SFR.INTCON, 4);
      bool RBIE = Ram.DirectGetRegisterBit((int)SFR.INTCON, 3);
      bool T0IF = Ram.DirectGetRegisterBit((int)SFR.INTCON, 2);
      if (GIE) {
        if (T0IE && T0IF) {
          ExecuteInterrupt();
        }
        if (INTE && CheckExternalInterrupt()) {
          Ram.ToggleRegisterBit((int)SFR.INTCON, 1, true);
          ExecuteInterrupt();
        }
        if (EEIE) {
          ExecuteInterrupt();
          //TODO: EEPROM implementation
        }
        if (RBIE && CheckPortBInterrupt()) {
          Ram.ToggleRegisterBit((int)SFR.INTCON, 0, true);
          ExecuteInterrupt();
        }
      }
    }

    private void ExecuteInterrupt() {
      Ram.DirectToggleRegisterBit((int)SFR.INTCON, 7, false);
      Ram.PushStack(ProgCounter);
      ProgCounter = 0x04;
    }

    private int CalcPrescaler() {
      int mask = 0x07;
      int prescaleValue = Ram.DirectGetRegisterValue((int)SFR.OPTION_REG) & mask;
      return 2 ^ (prescaleValue + 1);
    }

    private bool CheckExternalInterrupt() {
      bool INTEDG = Ram.DirectGetRegisterBit((int)SFR.OPTION_REG, 6);
      bool currentRB0 = Ram.DirectGetRegisterBit((int)SFR.PORTB, 0);
      if (INTEDG && !_tempRB0 && currentRB0) {
        return true;
      }
      if (!INTEDG && _tempRB0 && !currentRB0) {
        return true;
      }
      return false;
    }

    private bool CheckPortBInterrupt() {
      if ((GetPortBInterruptPins() & _tempPortB) != _tempPortB) {
        return true;
      }
      return false;
    }

    private int GetPortBInterruptPins() {
      int inputPins = Ram.DirectGetRegisterValue((int)SFR.PORTB) & Ram.DirectGetRegisterValue((int)SFR.TRISB);
      int result = inputPins & 0xF0;
      return result;
    }

    public OperationModel GetOpByIndex(int index) {
      foreach (OperationModel opModel in Operations) {
        if (index == opModel.Index) {
          return opModel;
        }
      }
      return null;
    }

    private void ChooseCommand(OperationModel opModel) {
      switch (opModel.Operation) {
        case Operation.ADDLW:
          ADDLWCommand(opModel);
          break;
        case Operation.ADDWF:
          ADDWFCommand(opModel);
          break;
        case Operation.ANDLW:
          ANDLWCommand(opModel);
          break;
        case Operation.ANDWF:
          ANDWFCommand(opModel);
          break;
        case Operation.BCF:
          BCFCommand(opModel);
          break;
        case Operation.BSF:
          BSFCommand(opModel);
          break;
        case Operation.BTFSC:
          BTFSCCommand(opModel);
          break;
        case Operation.BTFSS:
          BTFSSCommand(opModel);
          break;
        case Operation.CALL:
          CALLCommand(opModel);
          break;
        case Operation.CLRF:
          CLRFCommand(opModel);
          break;
        case Operation.CLRW:
          CLRWCommand(opModel);
          break;
        case Operation.CLRWDT:
          CLRWDTCommand(opModel);
          break;
        case Operation.COMF:
          COMFCommand(opModel);
          break;
        case Operation.DECF:
          DECFCommand(opModel);
          break;
        case Operation.DECFSZ:
          DECFSZCommand(opModel);
          break;
        case Operation.GOTO:
          GOTOCommand(opModel);
          break;
        case Operation.INCF:
          INCFCommand(opModel);
          break;
        case Operation.INCFSZ:
          INCFSZCommand(opModel);
          break;
        case Operation.IORLW:
          IORLWCommand(opModel);
          break;
        case Operation.IORWF:
          IORWFCommand(opModel);
          break;
        case Operation.MOVF:
          MOVFCommand(opModel);
          break;
        case Operation.MOVLW:
          MOVLWCommand(opModel);
          break;
        case Operation.MOVWF:
          MOVWFCommand(opModel);
          break;
        case Operation.NOP:
          NOPCommand();
          break;
        case Operation.RETFIE:
          RETFIECommand(opModel);
          break;
        case Operation.RETLW:
          RETLWCommand(opModel);
          break;
        case Operation.RETURN:
          RETURNCommand(opModel);
          break;
        case Operation.RLF:
          RLFCommand(opModel);
          break;
        case Operation.RRF:
          RRFCommand(opModel);
          break;
        case Operation.SLEEP:
          SLEEPCommand(opModel);
          break;
        case Operation.SUBLW:
          SUBLWCommand(opModel);
          break;
        case Operation.SUBWF:
          SUBWFCommand(opModel);
          break;
        case Operation.SWAPF:
          SWAPFCommand(opModel);
          break;
        case Operation.XORLW:
          XORLWCommand(opModel);
          break;
        case Operation.XORWF:
          XORWFCommand(opModel);
          break;
      }
    }

    private void ADDWFCommand(OperationModel opModel) {
      if (opModel.Args.Bool1) {
        Ram.SetRegisterValue(opModel.Args.Byte2, Ram.GetRegisterValue() + Ram.GetRegisterValue(opModel.Args.Byte2));
        CheckZeroBit(opModel.Args.Byte2);
      }
      else {
        Ram.SetRegisterValue(Ram.GetRegisterValue() + Ram.GetRegisterValue(opModel.Args.Byte2));
        CheckZeroBit();
      }
      CheckCarryBit(Ram.GetRegisterValue(), Ram.GetRegisterValue(opModel.Args.Byte2));
      CheckDigitCarryBit(Ram.GetRegisterValue(), Ram.GetRegisterValue(opModel.Args.Byte2));
    }

    private void ANDWFCommand(OperationModel opModel) {
      if (opModel.Args.Bool1) {
        Ram.SetRegisterValue(opModel.Args.Byte2, Ram.GetRegisterValue() & Ram.GetRegisterValue(opModel.Args.Byte2));
        CheckZeroBit(opModel.Args.Byte2);
      }
      else {
        Ram.SetRegisterValue(Ram.GetRegisterValue() & Ram.GetRegisterValue(opModel.Args.Byte2));
        CheckZeroBit();
      }
    }

    private void CLRFCommand(OperationModel opModel) {
      Ram.SetRegisterValue(opModel.Args.Byte1, 0);
      CheckZeroBit(opModel.Args.Byte2);
    }

    private void CLRWCommand(OperationModel opModel) {
      Ram.SetRegisterValue(0);
      CheckZeroBit();
    }

    private void COMFCommand(OperationModel opModel) {
      if (opModel.Args.Bool1) {
        Ram.SetRegisterValue(opModel.Args.Byte2, ~Ram.GetRegisterValue(opModel.Args.Byte2));
        CheckZeroBit(opModel.Args.Byte2);
      }
      else {
        Ram.SetRegisterValue(~Ram.GetRegisterValue(opModel.Args.Byte2));
        CheckZeroBit(opModel.Args.Byte2);
      }
    }

    private void DECFCommand(OperationModel opModel) {
      int regValue = Ram.GetRegisterValue(opModel.Args.Byte2);
      int dec = regValue - 1;
      if (opModel.Args.Bool1) {
        Ram.SetRegisterValue(opModel.Args.Byte2, dec);
        CheckZeroBit(opModel.Args.Byte2);
      }
      else {
        Ram.SetRegisterValue(dec);
        CheckZeroBit();
      }
    }

    private void DECFSZCommand(OperationModel opModel) {
      DECFCommand(opModel);
      if (Ram.GetRegisterBit((int)SFR.STATUS, 2)) {
        NOPCommand();
        _progCounter++;
      }
    }

    private void INCFCommand(OperationModel opModel) {
      int inc = Ram.GetRegisterValue(opModel.Args.Byte2) + 1;
      if (opModel.Args.Bool1) {
        Ram.SetRegisterValue(opModel.Args.Byte2, inc);
        CheckZeroBit(opModel.Args.Byte2);
      }
      else {
        Ram.SetRegisterValue(inc);
        CheckZeroBit();
      }
    }

    private void INCFSZCommand(OperationModel opModel) {
      INCFCommand(opModel);
      if (Ram.GetRegisterBit((int)SFR.STATUS, 2)) {
        NOPCommand();
        _progCounter++;
      }
    }

    private void IORWFCommand(OperationModel opModel) {
      if (opModel.Args.Bool1) {
        Ram.SetRegisterValue(opModel.Args.Byte2, Ram.GetRegisterValue() | Ram.GetRegisterValue(opModel.Args.Byte2));
        CheckZeroBit(opModel.Args.Byte2);
      }
      else {
        Ram.SetRegisterValue(Ram.GetRegisterValue() | Ram.GetRegisterValue(opModel.Args.Byte2));
        CheckZeroBit();
      }
    }

    private void MOVFCommand(OperationModel opModel) {
      if (opModel.Args.Bool1) {
        Ram.SetRegisterValue(opModel.Args.Byte2, Ram.GetRegisterValue(opModel.Args.Byte2));
        CheckZeroBit(opModel.Args.Byte2);
      }
      else {
        Ram.SetRegisterValue(Ram.GetRegisterValue(opModel.Args.Byte2));
        CheckZeroBit();
      }
    }

    private void MOVWFCommand(OperationModel opModel) {
      Ram.SetRegisterValue(opModel.Args.Byte1, Ram.GetRegisterValue());
    }

    private void NOPCommand() {
      Cycles++;
    }

    private void RLFCommand(OperationModel opModel) {
      bool msb = Ram.GetRegisterBit(opModel.Args.Byte2, 7);
      bool carry = Ram.GetRegisterBit((int)SFR.STATUS, 0);
      int shift = 2 * Ram.GetRegisterValue(opModel.Args.Byte2);
      if (carry) {
        shift++;
      }
      if (msb) {
        Ram.ToggleRegisterBit((int)SFR.STATUS, 0, true);
      }
      else {
        Ram.ToggleRegisterBit((int)SFR.STATUS, 0, false);
      }
      if (opModel.Args.Bool1) {
        Ram.SetRegisterValue(opModel.Args.Byte2, shift);
      }
      else {
        Ram.SetRegisterValue(shift);
      }
    }

    private void RRFCommand(OperationModel opModel) {
      bool lsb = Ram.GetRegisterBit(opModel.Args.Byte2, 0);
      bool carry = Ram.GetRegisterBit((int)SFR.STATUS, 0);
      int shift = Ram.GetRegisterValue(opModel.Args.Byte2) / 2;
      if (carry) {
        shift += 128;
      }
      if (lsb) {
        Ram.ToggleRegisterBit((int)SFR.STATUS, 0, true);
      }
      else {
        Ram.ToggleRegisterBit((int)SFR.STATUS, 0, false);
      }
      if (opModel.Args.Bool1) {
        Ram.SetRegisterValue(opModel.Args.Byte2, shift);
      }
      else {
        Ram.SetRegisterValue(shift);
      }
    }

    private void SUBWFCommand(OperationModel opModel) {
      int f = Ram.GetRegisterValue(opModel.Args.Byte2);
      int sub = ~Ram.GetRegisterValue() + 1;
      int subtraction = f + sub;
      CheckCarryBit(f, sub);
      CheckDigitCarryBit(f, sub);
      if (opModel.Args.Bool1) {
        Ram.SetRegisterValue(opModel.Args.Byte2, subtraction);
        CheckZeroBit(opModel.Args.Byte2);
      }
      else {
        Ram.SetRegisterValue(subtraction);
        CheckZeroBit();
      }
    }

    private void SWAPFCommand(OperationModel opModel) {
      int byteVal = Ram.GetRegisterValue(opModel.Args.Byte2);
      byte lowNibble = (byte)((byteVal & 0x0F) << 4);
      byte highNibble = (byte)((byteVal & 0xF0) >> 4);
      int switchedByte = lowNibble | highNibble;
      if (opModel.Args.Bool1) {
        Ram.SetRegisterValue(opModel.Args.Byte2, switchedByte);
      }
      else {
        Ram.SetRegisterValue(switchedByte);
      }
    }

    private void XORWFCommand(OperationModel opModel) {
      int xorVal = Ram.GetRegisterValue(opModel.Args.Byte2) ^ Ram.GetRegisterValue();
      if (opModel.Args.Bool1) {
        Ram.SetRegisterValue(opModel.Args.Byte2, xorVal);
        CheckZeroBit(Ram.GetRegisterValue(opModel.Args.Byte2));
      }
      else {
        Ram.SetRegisterValue(xorVal);
        CheckZeroBit(Ram.GetRegisterValue());
      }
    }

    private void BCFCommand(OperationModel opModel) {
      Ram.ToggleRegisterBit(opModel.Args.Byte2, opModel.Args.Byte1, false);
    }

    private void BSFCommand(OperationModel opModel) {
      Ram.ToggleRegisterBit(opModel.Args.Byte2, opModel.Args.Byte1, true);
    }

    private void BTFSCCommand(OperationModel opModel) {
      if (!Ram.GetRegisterBit(opModel.Args.Byte2, opModel.Args.Byte1)) {
        NOPCommand();
        ProgCounter++;
      }
    }

    private void BTFSSCommand(OperationModel opModel) {
      if (Ram.GetRegisterBit(opModel.Args.Byte2, opModel.Args.Byte1)) {
        NOPCommand();
        ProgCounter++;
      }
    }

    private void ADDLWCommand(OperationModel opModel) {
      int literal = opModel.Args.Byte1;
      int wValue = Ram.GetRegisterValue();
      CheckCarryBit(literal, wValue);
      CheckDigitCarryBit(literal, wValue);
      Ram.SetRegisterValue(literal + wValue);
      CheckZeroBit();
    }

    private void ANDLWCommand(OperationModel opModel) {
      int literal = opModel.Args.Byte1;
      int wValue = Ram.GetRegisterValue();
      Ram.SetRegisterValue(literal & wValue);
      CheckZeroBit();
    }

    private void CALLCommand(OperationModel opModel) {
      Ram.PushStack(ProgCounter);
      ProgCounter = opModel.Args.Byte1 - 1;
      //TODO: PCLATH logic
      Cycles++;
    }

    private void CLRWDTCommand(OperationModel opModel) {
      //TODO: Watchdog timer logic
    }

    private void GOTOCommand(OperationModel opModel) {
      ProgCounter = opModel.Args.Byte1 - 1;
      //TODO: PCLATH logic
      Cycles++;
    }

    private void IORLWCommand(OperationModel opModel) {
      int literal = opModel.Args.Byte1;
      int wValue = Ram.GetRegisterValue();
      Ram.SetRegisterValue(literal | wValue);
      CheckZeroBit();
    }

    private void MOVLWCommand(OperationModel opModel) {
      int literal = opModel.Args.Byte1;
      Ram.SetRegisterValue(literal);
    }

    private void RETFIECommand(OperationModel opModel) {
      ProgCounter = Ram.PopStack();
      Ram.ToggleRegisterBit((int)SFR.INTCON, 7, true);
      Cycles++;
    }

    private void RETLWCommand(OperationModel opModel) {
      Ram.SetRegisterValue(opModel.Args.Byte1);
      ProgCounter = Ram.PopStack();
      Cycles++;
    }

    private void RETURNCommand(OperationModel opModel) {
      ProgCounter = Ram.PopStack();
      Cycles++;
    }

    private void SLEEPCommand(OperationModel opModel) {
      Ram.ToggleRegisterBit((int)SFR.STATUS, 3, false);
      Ram.ToggleRegisterBit((int)SFR.STATUS, 4, true);
      Ram.ToggleRegisterBit((int)SFR.OPTION_REG, 0, false);
      Ram.ToggleRegisterBit((int)SFR.OPTION_REG, 1, false);
      Ram.ToggleRegisterBit((int)SFR.OPTION_REG, 2, false);
      //TODO: Put into sleep mode
    }

    private void SUBLWCommand(OperationModel opModel) {
      int literal = opModel.Args.Byte1;
      int wValue = Ram.GetRegisterValue();
      int sub = ~wValue + 1;
      int subtraction = literal + sub;
      CheckCarryBit(literal, sub);
      CheckDigitCarryBit(literal, sub);
      Ram.SetRegisterValue(subtraction);
      CheckZeroBit();
    }

    private void XORLWCommand(OperationModel opModel) {
      int literal = opModel.Args.Byte1;
      Ram.SetRegisterValue(literal ^ Ram.GetRegisterValue());
      CheckZeroBit();
    }

    private void CheckFSR(OperationModel opModel, int adress) {
      if (adress == 0) {
        opModel.IsIndirect = true;
      }
    }

    private void UseFSRValue(OperationModel opModel) {
      if (opModel.OpType != OperationType.LiteralControl &&
        opModel.OpType != OperationType.NoArgs &&
        opModel.OpType == OperationType.ByteOrientedF &&
        opModel.IsIndirect) {
        opModel.Args.Byte1 = Ram.DirectGetRegisterValue((int)SFR.FSR);
        return;
      }

      if (opModel.OpType != OperationType.LiteralControl &&
        opModel.OpType != OperationType.NoArgs &&
        opModel.IsIndirect) {
        opModel.Args.Byte2 = Ram.DirectGetRegisterValue((int)SFR.FSR);
      }
    }

    private void CheckCarryBit(int byte1, int byte2) {
      if (byte1 + byte2 > 255) {
        Ram.DirectToggleRegisterBit((int)SFR.STATUS, 0, true);
      }
      else {
        Ram.DirectToggleRegisterBit((int)SFR.STATUS, 0, false);
      }
    }

    private void CheckDigitCarryBit(int byte1, int byte2) {
      int mask = 15;
      int lowValue1 = mask & byte1;
      int lowValue2 = mask & byte2;
      if ((lowValue1 + lowValue2) > 15) {
        Ram.DirectToggleRegisterBit((int)SFR.STATUS, 1, true);
      }
      else {
        Ram.DirectToggleRegisterBit((int)SFR.STATUS, 1, false);
      }
    }

    private void CheckZeroBit(int adress) {
      if (Ram.GetRegisterValue(adress) == 0) {
        Ram.DirectToggleRegisterBit((int)SFR.STATUS, 2, true);
      }
      else {
        Ram.DirectToggleRegisterBit((int)SFR.STATUS, 2, false);
      }
    }

    private void CheckZeroBit() {
      if (Ram.GetRegisterValue() == 0) {
        Ram.DirectToggleRegisterBit((int)SFR.STATUS, 2, true);
      }
      else {
        Ram.DirectToggleRegisterBit((int)SFR.STATUS, 2, false);
      }
    }

    #endregion //Methods

  }
}
