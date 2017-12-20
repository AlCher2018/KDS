using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.AppModel
{
    // информация о кдс-клиенте
    public class ClientInfo
    {
        public string Name { get; set; }

        // флаг чтения клиентом заказов из внутренней коллекции
        public bool GetOrdersFlag { get; set; }

        // флаг инициирования клиентом изменения данных в БД
        public bool SetDataFlag { get; set; }

        private DateTime _lastRequestDate { get; set; }

        // для каждого клиента хранить набор Id заказов из БД для определения появления нового заказа (проигрывание мелодии на клиенте)
        // набор пар <Id заказа>, <набор Id блюд>
        // причем Id заказа может повторяться
        private List<KeyValuePair<int, List<int>>> _currentOrderIdsList;

        // группировка по блюдам и суммирование количества
        public bool IsDishGroupAndSumQuatity { get; set; }


        public ClientInfo()
        {
            _currentOrderIdsList = new List<KeyValuePair<int, List<int>>>();
        }


        // orders - набор заказов для отображения их клиентом
        public List<OrderModel> IsAppearNewOrder(List<OrderModel> orders)
        {
            List<OrderModel> newOrdersList = null;

            // собрать уникальные Id из переданного набора заказов
            List<KeyValuePair<int, List<int>>> uniqOrdersId = getOrderIdsList(orders);
            
            // если дата запроса превышает 10 секунд, то хранимый набор заказов считается устаревшим и уничтожается
            if ((DateTime.Now - _lastRequestDate).TotalSeconds > 10) _currentOrderIdsList.Clear();
            _lastRequestDate = DateTime.Now;

            // сохраненного нет
            if (_currentOrderIdsList.Count == 0)
            {
                // а для клиента есть - вернуть переданный набор
                if (uniqOrdersId.Count > 0)
                {
                    if (newOrdersList == null)
                        newOrdersList = new List<OrderModel>(orders);
                    else
                    {
                        newOrdersList.Clear();
                        newOrdersList.AddRange(orders);
                    }
                }
            }
            else
            {
                // удалить из сохраненной коллекции Ид те, которых уже нет в клиентской
                List<KeyValuePair<int, List<int>>> excludeIds = new List<KeyValuePair<int, List<int>>>();
                foreach (KeyValuePair<int, List<int>> itemCheck in _currentOrderIdsList)
                {
                    if (!findKVPair(itemCheck, uniqOrdersId)) excludeIds.Add(itemCheck);
                }
                if (excludeIds.Count > 0)
                {
                    foreach (KeyValuePair<int, List<int>> item in excludeIds) _currentOrderIdsList.Remove(item);
                }
                excludeIds.Clear(); excludeIds = null;

                // поиск клиентских заказов в сохраненном наборе
                if (newOrdersList == null)
                    newOrdersList = new List<OrderModel>();
                else
                    newOrdersList.Clear();
                foreach (KeyValuePair<int, List<int>> itemCheck in uniqOrdersId)
                {
                    if (!findKVPair(itemCheck, _currentOrderIdsList))
                    {
                        newOrdersList.Add(orders.FirstOrDefault(o => o.Id == itemCheck.Key));
                    }
                }
            }

            // сохранить новый набор Id-ов
            if (newOrdersList != null)
            {
                _currentOrderIdsList = uniqOrdersId;
            }

            return newOrdersList;
        }


        private bool findKVPair(KeyValuePair<int, List<int>> itemCheck, List<KeyValuePair<int, List<int>>> whereList)
        {
            KeyValuePair<int, List<int>>[] foundOrderId = whereList.Where(i => itemCheck.Key == i.Key).ToArray();

            return (foundOrderId.Length > 0);

            // цикл по элементам с равенством ключей
            //foreach (KeyValuePair<int, List<int>> findKeyList in whereList.Where(i => itemCheck.Key == i.Key))
            //{
            //    // сравнение внутренних массивов
            //    List<int> l1 = itemCheck.Value, l2 = findKeyList.Value;
            //    if (((l1 == null) && (l2 != null)) || ((l1 != null) && (l2 == null))) continue;
            //    if (l1.Count != l2.Count) continue;
            //    for (int i = 0; i < l1.Count; i++) if (l1[i] != l2[i]) continue;

            //    // внутренние массивы одинаковые
            //    return true;
            //}
            //return false;
        }

        private List<KeyValuePair<int, List<int>>> getOrderIdsList(List<OrderModel> orders)
        {
            List<KeyValuePair<int, List<int>>> retVal = new List<KeyValuePair<int, List<int>>>();

            foreach (OrderModel item in orders)
            {
                List<int> dishIds = item.Dishes.Select(d => d.Value.Id).ToList();
                retVal.Add(new KeyValuePair<int, List<int>>(item.Id, dishIds));
            }

            return retVal;
        }


    }  // class
}
