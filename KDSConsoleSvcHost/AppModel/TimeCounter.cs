﻿using KDSService.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.AppModel
{
    // класс, в котором хранятся даты От (_dtStart) и До (_dtStop)
    // эти даты заносятся при вызове соотв.методов: при вызове Start(initDate=0) устанавливается _dtStart и обнуляется _dtStop
    // в ValueTS возвращается промежуеток времени между _dtStart и Now
    // Когда вызывается Stop(), то в _dtStop заносится текущая дата, а в IncrementTS возвращается промежуток между _dtStop и _dtStart
    public class TimeCounter
    {
        private DateTime _dtStart, _dtStop;
        // постоянная величина в секундах, которая прибавляется к возвращаемому значению в ValueTS
        private int _increment;

        // возвращает кол-во секунд между текущим временем и _dtStart
        public int ValueTS
        {
            get
            {
                int retVal = Convert.ToInt32((DateTime.Now - _dtStart).TotalSeconds);
                if (_increment != 0) retVal += _increment;
                return  retVal;
            }
        }

        // если "таймер" не остановлен, то возвращаетс 0
        // иначе - кол-во секунд между _dtStop и _dtStart
        public int IncrementTS
        {
            get
            {
                return (_dtStop.IsZero()) ? 0 : Convert.ToInt32((_dtStop - _dtStart).TotalSeconds);
            }
        }

        // активность счетчика
        public bool Enabled { get { return _dtStop.IsZero(); } }

        public string Name { get; set; }

        public void Start(int increment = 0)
        {
            _dtStart = DateTime.Now;
            _increment = increment;
        }
        public void Start(DateTime initDate, int increment = 0)
        {
            _dtStart = initDate;
            _increment = increment;
        }

        public void Stop()
        {
            _dtStop = DateTime.Now;
        }

    }  // class
}
