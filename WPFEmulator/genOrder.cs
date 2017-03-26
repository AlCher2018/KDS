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
        private string[] _statusNames = { "Готовится", "ГОТОВ", "ВЫДАН" };

        private Timer _timer;
        private Random _rnd;

        public int Number { get; set; }
        public DateTime Date { get; set; }

        public event EventHandler<genOrderStatusChangedArgs> OrderStatusChanged;

        private int _statusId;
        public int StatusId { get { return _statusId; } }
        public string StatusName { get { return _statusNames[StatusId]; } }

        public genOrder(bool isAutoChangeStatus)
        {
            _rnd = new Random();
            _timer = new Timer();
            _timer.AutoReset = false;
            _timer.Elapsed += _timer_Elapsed;

            _statusId = 0;

            if (isAutoChangeStatus)
            {
                _timer.Interval = _rnd.Next(3, 7) * 1000d;
                _timer.Start();
            }
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _statusId++;

            if (OrderStatusChanged != null) OrderStatusChanged(this, new genOrderStatusChangedArgs() { StatusId = _statusId, StatusName = this.StatusName });

            if (_statusId == 2)
            {
                _timer.Stop(); _timer.Close(); _timer = null;
            }
            else
            {
                _timer.Interval = _rnd.Next(3, 7) * 1000d;
                _timer.Start();
            }
        }
    }  // class genOrder

    public class genOrderStatusChangedArgs: EventArgs
    {
        public int StatusId { get; set; }
        public string StatusName { get; set; }
    }

}
