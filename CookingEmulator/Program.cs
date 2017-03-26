using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace CookingEmulator
{
    class Program
    {
        private static List<Order> _orders = new List<Order>();
        private static Timer _orderTimer = new Timer();
        private static Random rnd = new Random();

        private static int _currNumber = 123;
        private static object _threadLockObj;
        private static KDS_06_10Entities _db;



        static void Main(string[] args)
        {
            _db = new KDS_06_10Entities();

            _threadLockObj = new object();
            _orderTimer.AutoReset = false;
            _orderTimer.Elapsed += _orderTimer_Elapsed;

            _orderTimer_Elapsed(null, null);

            Console.Read();
        }

        private static void _orderTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Order newOrder = new Order() { Number = _currNumber++, Date = DateTime.Now };
            newOrder.StatusEventHandler += NewOrder_StatusEventHandler;
            lock (_threadLockObj)
            {
                _db.Orders.Add(new Orders() { OrderStatusId = 0, CreateDate = newOrder.Date, LanguageTypeId = 2, Number = newOrder.Number, QueueStatusId = 0 });
                _db.SaveChanges();
                _orders.Add(newOrder);
            }

            Console.WriteLine("{0}. Создан заказ № {1}", newOrder.Date, newOrder.Number);

            _orderTimer.Interval = rnd.Next(3, 10) * 1000d;
            _orderTimer.Start();

        }

        private static void NewOrder_StatusEventHandler(object sender, OrderStatusArgs e)
        {
            Order order = (Order)sender;
            if (e.Status == 1)
            {
                lock (_threadLockObj)
                {
                    Orders dbOrder = _db.Orders.FirstOrDefault(o => o.Number == order.Number);
                    if (dbOrder != null)
                    {
                        dbOrder.QueueStatusId = order.Status;
                        _db.SaveChanges();
                    }
                }
                Console.WriteLine("{0}. Заказ {1} - готов.", DateTime.Now, order.Number);
            }
            else if (e.Status == 2)
            {
                Console.WriteLine("{0}. Заказ {1} - выдан.", DateTime.Now, order.Number);
                lock (_threadLockObj)
                {
                    Orders dbOrder = _db.Orders.FirstOrDefault(o => o.Number == order.Number);
                    if (dbOrder != null)
                    {
                        dbOrder.QueueStatusId = order.Status;
                        _db.SaveChanges();
                    }

                    order.StatusEventHandler -= NewOrder_StatusEventHandler;
                    _orders.Remove(order);
                    order = null;
                }
            }
        }
    }

    public class Order
    {
        public int Number { get; set; }
        public DateTime Date { get; set; }
        public int Status { get { return _status; }  set { _status = value; } }

        public event EventHandler<OrderStatusArgs> StatusEventHandler;

        private int _status;
        private Timer _statusTimer;
        private Random _rnd;

        public Order()
        {
            _rnd = new Random();
            _status = 0;

            _statusTimer = new Timer();
            _statusTimer.Interval = _rnd.Next(3, 10) * 1000;
            _statusTimer.Start();
            _statusTimer.Elapsed += _statusTimer_Elapsed;
        }

        private void _statusTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _status++;
            if (StatusEventHandler != null) StatusEventHandler(this, new OrderStatusArgs() { Status = _status });

            if (_status == 1)
            {
                _statusTimer.Interval = _rnd.Next(3, 10) * 1000;
            }
            else if (_status == 2)
            {
                _statusTimer.Stop();
                _statusTimer.Close(); _statusTimer = null;
            }

        }
    }

    public class OrderStatusArgs : EventArgs
    {
        public int Status { get; set; }
    }

}
