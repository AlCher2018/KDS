using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace KDSConsoleSvcHost.AppModel
{
    /// <summary>
    /// IncrementalTimer - инкрементирующий таймер
    /// сохраняет свое значение между вызовами
    /// В свойстве Value возвращает число миллисекунд. 
    /// Параметр конструктора interval используется и как интервал внутреннего таймера, и как величина приращения Value.
    /// </summary>
    
    public class IncrementalTimer: IDisposable
    {
        // fields
        private int _interval;
        private int _value;
        private int _increment;

        private TimeSpan _tsInterval;
        private TimeSpan _tsValue;
        private TimeSpan _tsIncrement;

        private DateTime _dtBase, _dtStart, _dtStop;

        private Timer _timer;
        
        // properties
        public int Interval { get { return _interval; } }
        public int Value { get { return _value; } }
        public int Increment { get { return _increment; } }

        //public TimeSpan ValueTS { get { return _tsValue; } }
        //public TimeSpan IncrementTS { get { return _tsIncrement; } }
        public int ValueTS {
            get {
                return (int)(DateTime.Now - _dtBase).TotalSeconds;
            }
        }
        public int IncrementTS {
            get {
                return (int)(DateTime.Now - _dtStop).TotalSeconds;
            }
        }

        public bool Enabled { get { return _timer.Enabled; } }
        

        // CTOR
        public IncrementalTimer(int interval)
        {
            _interval = interval;
            _value = 0; _dtBase = DateTime.MinValue;

            _tsInterval = TimeSpan.FromMilliseconds(interval);
            _tsValue = TimeSpan.Zero;

            _timer = new Timer(interval);
            _timer.Elapsed += _timer_Elapsed;

        }  // ctor

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _increment += _interval;
            _value += _interval;

            _tsIncrement += _tsInterval;
            _tsValue += _tsInterval;
        }

        public void InitDateTimeValue(DateTime value)
        {
            _dtBase = value;
        }

        public void Start()
        {
            _increment = 0;
            _tsIncrement = TimeSpan.Zero;

            _dtStart = DateTime.Now;
            if (_dtBase.Equals(DateTime.MinValue)) _dtBase = _dtStart;

            if (_timer != null) _timer.Start();
        }

        public int Stop()
        {
            if (_timer != null) _timer.Stop();
            _dtStop = DateTime.Now;
            return _increment;
        }

        public void Reset()
        {
            _value = 0;
            _tsValue = TimeSpan.Zero;
            _dtBase = DateTime.MinValue;
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                if (_timer.Enabled) _timer.Stop();
                _timer.Dispose(); _timer = null;
            }
        }
    }  // class
}
