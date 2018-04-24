using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.Lib
{
    // класс для хранения Ид заблокированных заказов и блюд
    public static class OrderLocker
    {
        private static Dictionary<int, LockInfo> _orders, _dishes;

        private static string _errMsg = null;
        public static string ErrMsg { get { return ErrMsg; } }

        private static object _locker = new object();


        //  static CTOR
        static OrderLocker()
        {
            _orders = new Dictionary<int, LockInfo>();
            _dishes = new Dictionary<int, LockInfo>();
        }

        #region Orders
        public static bool LockOrder(int orderId)
        {
            lock (_orders)
            {
                if (_errMsg != null) _errMsg = null;

                if (_orders.ContainsKey(orderId))
                {
                    //_orders[orderId] = false;
                }
                else
                {
                    // добавить заказ в коллекцию заблокированных
                    _orders.Add(orderId, new LockInfo() { LockDate = DateTime.Now});
                }

                return (_errMsg == null);
            }
        }
        public static bool DelockOrder(int orderId)
        {
            lock (_orders)
            {
                if (_errMsg != null) _errMsg = null;

                if (_orders.ContainsKey(orderId)) _orders.Remove(orderId);

                return (_errMsg == null);
            }
        }

        public static int[] GetLockedOrders()
        {
            lock (_orders)
            {
                return (_orders.Keys.ToArray());
            }
        }

        public static TimeSpan GetTimeOrderLocked(int orderId)
        {
            lock (_orders)
            {
                TimeSpan retVal = new TimeSpan(-1);
                if (_orders.ContainsKey(orderId))
                {
                    retVal = (DateTime.Now - _orders[orderId].LockDate);
                }
                return retVal;
            }
        }

        public static bool IsLockOrders() { return (_orders.Count > 0); }

        public static bool IsLockOrder(int orderId) { return (_orders.ContainsKey(orderId)); }

        internal static void ClearOrders()
        {
            _orders.Clear();
        }

        #endregion

        #region Dishes
        public static bool LockDish(int dishId)
        {
            lock (_dishes)
            {
                if (_errMsg != null) _errMsg = null;

                if (_dishes.ContainsKey(dishId))
                {
                }
                else
                {
                    // добавить заказ в коллекцию заблокированных
                    _dishes.Add(dishId, new LockInfo() { LockDate = DateTime.Now });
                }

                return (_errMsg == null);
            }
        }
        public static bool DelockDish(int dishId)
        {
            lock (_dishes)
            {
                if (_errMsg != null) _errMsg = null;

                if (_dishes.ContainsKey(dishId)) _dishes.Remove(dishId);

                return (_errMsg == null);
            }
        }

        public static TimeSpan GetTimeDishLocked(int dishId)
        {
            lock (_dishes)
            {
                TimeSpan retVal = new TimeSpan(-1);
                if (_dishes.ContainsKey(dishId))
                {
                    retVal = (DateTime.Now - _dishes[dishId].LockDate);
                }
                return retVal;
            }
        }

        public static bool IsLockDishes() { return (_dishes.Count > 0); }

        public static bool IsLockDish(int dishId) { return (_dishes.ContainsKey(dishId)); }

        internal static void ClearDishes()
        {
            _dishes.Clear();
        }
        #endregion

        private class LockInfo
        {
            public DateTime LockDate { get; set; }
            public bool Status { get; set; }
        }

    }  // class
}
