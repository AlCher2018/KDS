using IntegraLib;
using System;
using System.Collections.Generic;


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

        public DateTime StartDT { get { return _dtStart; } }

        // возвращает кол-во секунд между текущим временем и _dtStart
        public int ValueTS
        {
            get
            {
                int retVal = 0;
                if (!_dtStart.IsZero())
                {
                    try
                    {
                        retVal = Convert.ToInt32((DateTime.Now - _dtStart).TotalSeconds);
                        if (_increment != 0) retVal += _increment;
                    }
                    catch (Exception ex)
                    {
                        throw new NotFiniteNumberException(string.Format("Класс TimeCounter: Не могу преобразовать период времени в секунды ({0})", ex.Message));
                    }
                }
                else
                    throw new NotFiniteNumberException("Класс TimeCounter: Не задана начальная дата для получения периода времени");

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
        public bool Enabled { get { return (!_dtStart.IsZero() && _dtStop.IsZero()); } }

        public string Name { get; set; }

        public void Start(int increment = 0)
        {
            _dtStop = DateTime.MinValue;
            _dtStart = DateTime.Now;
            _increment = increment;
        }
        public void Start(DateTime initDate, int increment = 0)
        {
            _dtStop = DateTime.MinValue;
            _dtStart = initDate;
            _increment = increment;
        }

        public void Stop()
        {
            _dtStop = DateTime.Now;
        }

    }  // class
}
