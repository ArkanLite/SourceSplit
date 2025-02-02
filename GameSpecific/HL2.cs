﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using LiveSplit.ComponentUtil;
using LiveSplit.SourceSplit.GameHandling;
using LiveSplit.SourceSplit.Utilities;

namespace LiveSplit.SourceSplit.GameSpecific
{
    class HL2 : GameSupport
    {
        // how to match with demos:
        // start: first tick when your position is at -9419 -2483 22 (cl_showpos 1)
        // ending: first tick when screen flashes white

        private ValueWatcher<float> _splitTime = new ValueWatcher<float>();

        private Vector3f _startPos = new Vector3f(-9419f, -2483f, 22f);

        private HL2Mods_TheLostCity _lostCity = new HL2Mods_TheLostCity();
        private HL2Mods_Tinje _tinje = new HL2Mods_Tinje();
        private HL2Mods_ExperimentalFuel _experimentalFuel = new HL2Mods_ExperimentalFuel();

        public HL2()
        {
            this.AddFirstMap("d1_trainstation_01");
            this.AddLastMap("d3_breen_01");
            AdditionalGameSupport = new List<GameSupport>() { _lostCity, _tinje, _experimentalFuel };
        }

        protected override void OnSessionStartInternal(GameState state, TimerActions actions)
        {
            if (this.IsLastMap)
                _splitTime.Current = state.GameEngine.GetOutputFireTime("sprite_end_final_explosion_1", "ShowSprite", "", 70);
        }

        protected override void OnUpdateInternal(GameState state, TimerActions actions)
        {
            if (OnceFlag)
                return;

            if (this.IsFirstMap) 
            {
                // "OnTrigger" "point_teleport_destination,Teleport,,0.1,-1"

                // first tick player is moveable and on the train
                if (state.PlayerPosition.Current.DistanceXY(_startPos) <= 1.0)
                {
                    Debug.WriteLine("hl2 start");
                    OnceFlag = true;
                    actions.Start(StartOffsetMilliseconds); return;
                }
            }
            else if (this.IsLastMap)
            {
                _splitTime.Current = state.GameEngine.GetOutputFireTime("sprite_end_final_explosion_1", "ShowSprite", "", 70);
                if (_splitTime.Current > 0 && _splitTime.Old == 0)
                {
                    Debug.WriteLine("hl2 end");
                    OnceFlag = true;
                    actions.End(EndOffsetMilliseconds); 
                }
            }

            return;
        }
    }
}
