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

        private Timer _timer;
        
        // properties
        public int Interval { get { return _interval; } }
        public int Value { get { return _value; } }
        public int Increment { get { return _increment; } }

        public TimeSpan IntervalTS { get { return _tsInterval; } }
        public TimeSpan ValueTS { get { return _tsValue; } }
        public TimeSpan IncrementTS { get { return _tsIncrement; } }


        // ctor
        public IncrementalTimer(int interval)
        {
            _interval = interval;
            _value = 0;

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

        public void Start()
        {
            _increment = 0;
            _tsIncrement = TimeSpan.Zero;

            if (_timer != null) _timer.Start();
        }

        public int Stop()
        {
            if (_timer != null) _timer.Stop();
            return _increment;
        }

        public void Reset()
        {
            _value = 0;
            _tsValue = TimeSpan.Zero;
        }

        public void Dispose()
        {
            if (_timer != null) { _timer.Stop(); _timer.Dispose(); _timer = null; }
        }
    }  // class
}
