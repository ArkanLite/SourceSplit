﻿using LiveSplit.ComponentUtil;
using System;
using System.Diagnostics;
using System.Speech.Synthesis;

namespace LiveSplit.SourceSplit.GameSpecific
{
    class BMSRetail : GameSupport
    {
        // how to match with demos:
        // start: on map load
        // xen start: when view entity changes back to the player's
        // ending: first tick nihilanth's health is zero
        // earthbound ending: when view entity changes to the ending camera's

        private bool _onceFlag;

        // offsets and binary sizes
        private int _baseEntityHealthOffset = -1;
        private const int _serverModernModuleSize = 0x9D6000;
        private const int _serverModModuleSize = 0x81B000;
        private const int _nihiPhaseCounterOffset = 0x1a6e4;

        // earthbound start
        private string _ebEndMap = "bm_c3a2i";
        private CustomCommand _ebEndCommand = new CustomCommand("ebend", "0", "Split on Lambda Core teleport");
        private int _ebCamIndex;

        // xen start & run end
        private const string _xenStartMap = "bm_c4a1a";
        private CustomCommand _xenStartCommand = new CustomCommand(
            "xenstart", 
            "1",
            "Start upon gaining control in Xen\n- 0 disables this function\n- 1 starts the timer at 2493 ticks\n- 2 starts the timer with no offset.");
        private CustomCommand _xenSplitCommand = new CustomCommand("xensplit", "0", "Split upon gaining control in Xen");
        private CustomCommand _nihiSplitCommand = new CustomCommand("nihisplit", "0", "Split per phases of Nihilanth's fight");
        private MemoryWatcher<int> _nihiHP;
        private MemoryWatcher<int> _nihiPhaseCounter;
        private int _xenCamIndex;
        private IntPtr _getGlobalNameFuncPtr = IntPtr.Zero;
        private RemoteOpsHandler _roHandler;
        private const int _xenStartTick = 2493; // 38.953125s

        private CustomCommandHandler _cmdHandler;

        private BMSMods_HazardCourse _hazardCourse = new BMSMods_HazardCourse();
        private BMSMods_FurtherData _furtherData = new BMSMods_FurtherData();

        public BMSRetail()
        {
            this.GameTimingMethod = GameTimingMethod.EngineTicksWithPauses;
            this.StartOnFirstLoadMaps.Add("bm_c1a0a");
            this.AddFirstMap("bm_c1a0a");
            this.AddLastMap("bm_c4a4a");
            this.RequiredProperties = PlayerProperties.ViewEntity;

            this.AdditionalGameSupport.AddRange(new GameSupport[] { _hazardCourse, _furtherData });
            _cmdHandler = new CustomCommandHandler( _xenSplitCommand, _xenStartCommand, _nihiSplitCommand, _ebEndCommand);
        }

        public override void OnGameAttached(GameState state)
        {
            base.OnGameAttached(state);
            _cmdHandler.Init(state);

            ProcessModuleWow64Safe server = state.GetModule("server.dll");

            var scanner = new SignatureScanner(state.GameProcess, server.BaseAddress, server.ModuleMemorySize);
            _getGlobalNameFuncPtr = scanner.Scan(new SigScanTarget("55 8B EC 51 FF 75 ?? 8D 45 ??"));
            _roHandler = new RemoteOpsHandler(state.GameProcess);

            if (GameMemory.GetBaseEntityMemberOffset("m_iHealth", state.GameProcess, scanner, out _baseEntityHealthOffset))
                Debug.WriteLine("CBaseEntity::m_iHealth offset = 0x" + _baseEntityHealthOffset.ToString("X"));

            if (server.ModuleMemorySize < _serverModernModuleSize)
            {
                _ebEndCommand.BValue = true;
                // for mod, eb's final map name is different
                if (server.ModuleMemorySize <= _serverModModuleSize)
                    _ebEndMap = "bm_c3a2h";
            }
        }

        public override void OnSessionStart(GameState state)
        {
            base.OnSessionStart(state);

            _onceFlag = false;

            if (state.CurrentMap.ToLower() == _ebEndMap)
            {
                _ebCamIndex = state.GetEntIndexByName("locked_in");
            }
            else if (state.CurrentMap.ToLower() == _xenStartMap)
            {
                _xenCamIndex = state.GetEntIndexByName("stand_viewcontrol");
            }
            else if (this.IsLastMap && state.PlayerEntInfo.EntityPtr != IntPtr.Zero)
            {
                IntPtr nihiPtr = state.GetEntityByName("nihilanth");
                Debug.WriteLine("Nihilanth pointer = 0x" + nihiPtr.ToString("X"));

                _nihiHP = new MemoryWatcher<int>(nihiPtr + _baseEntityHealthOffset);
                _nihiPhaseCounter = new MemoryWatcher<int>(nihiPtr + _nihiPhaseCounterOffset);
            }
        }

        public GameSupportResult DefaultEnd(string endingname)
        {
            _onceFlag = true;
            Debug.WriteLine(endingname);
            return GameSupportResult.PlayerLostControl;
        }

        public override void OnGenericUpdate(GameState state)
        {
            base.OnGenericUpdate(state);
            _cmdHandler.Update(state);
        }

        public override GameSupportResult OnUpdate(GameState state)
        {
            StartOffsetTicks = EndOffsetTicks = 0;

            if (_onceFlag)
                return GameSupportResult.DoNothing;

            if (this.IsLastMap)
            {
                _nihiHP.Update(state.GameProcess);

                if (_nihiHP.Current <= 0 && _nihiHP.Old > 0)
                    return DefaultEnd("black mesa end");
                    
                if (_nihiSplitCommand.BValue)
                {
                    _nihiPhaseCounter.Update(state.GameProcess);

                    if (_nihiPhaseCounter.Current - _nihiPhaseCounter.Old == 1 && _nihiPhaseCounter.Old != 0)
                    {
                        Debug.WriteLine("black mesa nihilanth phase " + _nihiPhaseCounter.Old + " end");
                        return GameSupportResult.PlayerLostControl;
                    }
                }
            }
            else if (_ebEndCommand.BValue && state.CurrentMap.ToLower() == _ebEndMap)
            {
                if (state.PlayerViewEntityIndex.Current == _ebCamIndex && state.PlayerViewEntityIndex.Old == 1)
                    return DefaultEnd("bms eb end");
            }
            else if ((_xenStartCommand.BValue || _xenSplitCommand.BValue) && state.CurrentMap.ToLower() == _xenStartMap)
            {
                if (state.PlayerViewEntityIndex.Current == 1 && state.PlayerViewEntityIndex.Old == _xenCamIndex)
                {
                    _onceFlag = true;
                    Debug.WriteLine("bms xen start");

                    GameSupportResult result = GameSupportResult.DoNothing;

                    if (_xenStartCommand.BValue && _getGlobalNameFuncPtr != IntPtr.Zero)
                    {
                        if (_roHandler.CallFunctionString("LC2XEN", _getGlobalNameFuncPtr) == uint.MaxValue)
                        {
                            if (_xenStartCommand.IValue == 1)
                                StartOffsetTicks = -_xenStartTick;
                            result = GameSupportResult.PlayerGainedControl;
                        }
                    }

                    return _xenSplitCommand.BValue ? GameSupportResult.ManualSplit : result;
                }
            }

            return GameSupportResult.DoNothing;
        }
    }
}