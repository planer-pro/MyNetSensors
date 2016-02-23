﻿/*  MyNetSensors 
    Copyright (C) 2015 Derwish <derwish.pro@gmail.com>
    License: http://www.gnu.org/licenses/gpl-3.0.txt  
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace MyNetSensors.Nodes
{
    public class TimeDelayedValueNode : Node
    {
        private class DelayedValue
        {
            public string Value { get; set; }
            public DateTime ArrivalTime { get; set; }

            public DelayedValue(string value)
            {
                Value = value;
                ArrivalTime = DateTime.Now;
            }
        }

        private readonly int DEFAULT_INTERVAL = 1000;

        private bool enabled;

        private double interval;

        private List<DelayedValue> delayedValues=new List<DelayedValue>();

        public TimeDelayedValueNode() : base("Time", "Delayed Value")
        {
            AddInput("Value", DataType.Number);
            AddInput("Interval", DataType.Number, true);

            AddOutput("Value", DataType.Logical);

            interval = DEFAULT_INTERVAL;
        }

        public override void Loop()
        {
            foreach (var delayedValue in delayedValues)
            {
                if ((DateTime.Now - delayedValue.ArrivalTime).TotalMilliseconds >= interval)
                {
                    delayedValues.Remove(delayedValue);
                    Outputs[0].Value = delayedValue.Value;
                    return;
                }
            }
        }
        

        public override void OnInputChange(Input input)
        {
            if (input == Inputs[0])
            {
                delayedValues.Add(new DelayedValue(input.Value));
            }


            if (input == Inputs[1])
            {
                if (input.Value == null)
                    interval = DEFAULT_INTERVAL;
                else
                    double.TryParse(input.Value, out interval);

                if (interval < 1)
                    interval = 1;

                LogInfo($"Interval changed to {interval} ms");
            }
        }
    }
}