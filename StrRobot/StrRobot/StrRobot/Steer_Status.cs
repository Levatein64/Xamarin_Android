using System;
using System.Collections.Generic;
using System.Text;

namespace StrRobot
{
    class Steer_Status
    {
        //L2L Value
        public float init_target_torque = (float)2.40;
        public byte stuck_angle = 5;
        public byte stuck_time = 30;
        public float  gear_ratio = (float)263 / 72;
        public int offset = 600;

        //State
        public byte mtrOff = 0;
        public byte mtrOn = 1;
        public byte GOREADY = 2;
        public byte Pause = 3;
        public byte Restart = 4;
        public byte L2L = 5;
        public byte ConstStr = 6;
        public byte SineStr = 7;

        public bool l2l_pass = false;

        //Motor Parameter
        public float Kt = (float)0.31;
        public int enc_counter = 2^14;
        public int motor_speed = 490 * 6;
    }
}
