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
        // причем набор ИдБлюд не зависит от времени создания, чтобы не было разницы между режимами ПоВремени и ПоЗаказам
        private Dictionary<int, List<int>> _currentOrderIdsList;

        // группировка по блюдам и суммирование количества
        public bool IsDishGroupAndSumQuantity { get; set; }


        public ClientInfo()
        {
            _currentOrderIdsList = new Dictionary<int, List<int>>();
        }


        // orders - набор заказов для отображения их клиентом
        public List<OrderModel> IsAppearNewOrder(List<OrderModel> orders)
        {
            List<OrderModel> newOrdersList = null;

            // собрать уникальные Id из переданного набора заказов
            Dictionary<int, List<int>> uniqOrdersId = getOrderIdsList(orders);
            
            // если дата запроса превышает 10 секунд, то хранимый набор заказов считается устаревшим и уничтожается
            if ((DateTime.Now - _lastRequestDate).TotalSeconds > 10) _currentOrderIdsList.Clear();
            _lastRequestDate = DateTime.Now;
            bool isNeedUpdate = false;

            // сохраненного нет
            if (_currentOrderIdsList.Count == 0)
            {
                // а для клиента есть - вернуть переданный набор
                if (uniqOrdersId.Count > 0) newOrdersList = new List<OrderModel>(orders);
            }
            else
            {
                // удалить из сохраненной коллекции заказы, которых уже нет в клиентской
                List<int> excludeIds = _currentOrderIdsList.Keys.Except(uniqOrdersId.Keys).ToList();
                if (excludeIds.Count > 0)
                {
                    excludeIds.ForEach(id => _currentOrderIdsList.Remove(id));
                    excludeIds.Clear(); excludeIds = null;
                }

//                OrderModel om = orders.FirstOrDefault(o => o.Id==260);

                // поиск клиентских заказов в сохраненном наборе
                newOrdersList = new List<OrderModel>();
                bool needUpdate;
                foreach (KeyValuePair<int, List<int>> itemCheck in uniqOrdersId)
                {
                    if (!findKVPair(itemCheck, _currentOrderIdsList, out needUpdate))
                    {
                        newOrdersList.Add(orders.FirstOrDefault(o => o.Id == itemCheck.Key));
                    }

                    if (needUpdate && !isNeedUpdate) isNeedUpdate = true;
                }
            }

            // сохранить новый набор Id-ов
            if ((newOrdersList != null) || isNeedUpdate)
            {
                _currentOrderIdsList = uniqOrdersId;
            }

            return newOrdersList;
        }


        private bool findKVPair(KeyValuePair<int, List<int>> itemCheck, Dictionary<int, List<int>> whereList, out bool needUpdate)
        {
            needUpdate = false;
            bool localNeedUpdate = false;

            bool retVal = whereList.Any(i =>
            {
                if (itemCheck.Key == i.Key)
                {
                    bool isEqual = itemCheck.Value.SequenceEqual(i.Value);
                    // если набор Ид блюд не равны
                    if (isEqual == false)
                    {
                        // то проверить наличие нового Ид в проверяемом наборе блюд, и если новых нет
                        // то считаем наборы блюд одинаковыми, но обновить в вызывающем модуле надо
                        if (itemCheck.Value.Any(dishId => !i.Value.Contains(dishId)) == false)
                        {
                            isEqual = true;
                            localNeedUpdate = true;
                        }
                    }
                    return isEqual;
                }
                else
                    return false;
            });

            needUpdate = localNeedUpdate;
            return retVal;
        }

        private Dictionary<int, List<int>> getOrderIdsList(List<OrderModel> orders)
        {
            Dictionary<int, List<int>> retVal = new Dictionary<int, List<int>>();

            foreach (OrderModel item in orders)
            {
                List<int> dishIds = item.Dishes.Select(d => d.Value.Id).ToList();

                if (retVal.ContainsKey(item.Id))
                {
                    retVal[item.Id].AddRange(dishIds);
                }
                else
                {
                    retVal.Add(item.Id, dishIds);
                }
            }

            return retVal;
        }


    }  // class
}
