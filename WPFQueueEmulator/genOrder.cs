using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace WPFEmulator
{
    public class genOrder
    {
        private string[] _langNames = { "укр","рус","анг"};
        private string[] _statusNames = { "Готовится", "ГОТОВ", "ВЫДАН" };

        private Timer _timer;
        private Random _rnd;

        public int Number { get; set; }
        public DateTime Date { get; set; }
        public DateTime Date1 { get; set; }
        public DateTime Date2 { get; set; }

        public event EventHandler<genOrderStatusChangedArgs> OrderStatusChanged;

        private int _statusId;
        public int StatusId { get { return _statusId; } }
        public string StatusName { get { return _statusNames[StatusId]; } }

        public int LanguageTypeId { get; set; }

        // ctor
        public genOrder(bool isAutoChangeStatus)
        {
            LanguageTypeId = 2;

            _rnd = new Random();
            _timer = new Timer();
            _timer.AutoReset = false;
            _timer.Elapsed += _timer_Elapsed;

            _statusId = 0;
            this.Date = DateTime.Now;

            if (isAutoChangeStatus)
            {
                _timer.Interval = _rnd.Next(8, 20) * 1000d;
                _timer.Start();
            }
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _statusId++;

            if (_statusId == 2)
            {
                this.Date2 = DateTime.Now;
                _timer.Stop(); _timer.Close(); _timer = null;
            }
            else
            {
                this.Date1 = DateTime.Now;
                _timer.Interval = _rnd.Next(2, 5) * 1000d;
                _timer.Start();
            }

            if (OrderStatusChanged != null) OrderStatusChanged(this, new genOrderStatusChangedArgs() { StatusId = _statusId, StatusName = this.StatusName });

        }

        public void StartTimer()
        {
            _timer.Start();
        }
        public void StopTimer()
        {
            _timer.Stop();
        }


    }  // class genOrder

    public class genOrderStatusChangedArgs: EventArgs
    {
        public int StatusId { get; set; }
        public string StatusName { get; set; }
    }

}
