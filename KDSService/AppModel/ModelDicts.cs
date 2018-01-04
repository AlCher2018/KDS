using IntegraLib;
using KDSService.DataSource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;


namespace KDSService.AppModel
{
    // служебный класс для получения словарей
    public static class ModelDicts
    {
        // хранить в классе словари справочников
        private static Dictionary<int, OrderStatusModel> _statuses;
        private static Dictionary<int, DepartmentModel> _departments;

        public static bool UpdateModelDictsFromDB(out string errMsg)
        {
            errMsg = "";
            // список статусов -> в словарь
            List<OrderStatusModel> list1 = DBOrderHelper.GetOrderStatusesList();
            if (list1 == null)
            {
                errMsg = DBOrderHelper.ErrorMessage;
                return false;
            }
            _statuses = new Dictionary<int, OrderStatusModel>();
            list1.ForEach(item => _statuses.Add(item.Id, item));

            // список отделов -> в словарь
            // а также обновить словарь кол-ва блюд по цехам
            Dictionary<int, decimal> depQty = (Dictionary<int, decimal>)AppProperties.GetProperty("dishesQty");
            depQty.Clear();
            _departments = new Dictionary<int, DepartmentModel>();
            List<DepartmentModel> list2 = DBOrderHelper.GetDepartmentsList();
            if (list2 == null)
            {
                errMsg = DBOrderHelper.ErrorMessage;
                return false;
            }
            list2.ForEach(item =>
            {
                _departments.Add(item.Id, item);
                depQty.Add(item.Id, 0m);
            });

            return true;
        }

        #region get app dict item
        public static OrderStatusModel GetOrderStatusModelById(int statusId)
        {
            if (_statuses.ContainsKey(statusId)) return _statuses[statusId];
            return null;
        }

        public static DepartmentModel GetDepartmentById(int depId)
        {
            if (_departments.ContainsKey(depId)) return _departments[depId];
            return null;
        }

        #endregion

        // является ли цех автостартуемым
        public static bool GetDepAutoStart(int depId)
        {
            if (_departments.ContainsKey(depId)) return _departments[depId].IsAutoStart;
            else return false;
        }

        // глубина очереди цеха
        public static int GetDepDepthCount(int depId)
        {
            if (_departments.ContainsKey(depId)) return _departments[depId].DishQuantity;
            else return 0;
        }

    }  // class ModelDicts


    // СТАТУС ЗАКАЗА/БЛЮДА
    [DataContract]
    public class OrderStatusModel
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string AppName { get; set; }

        [DataMember]
        public string Description { get; set; }

    }  // class OrderStatusModel


    // ОТДЕЛ
    [DataContract]
    public class DepartmentModel
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string UID { get; set; }

        [DataMember]
        public bool IsAutoStart { get; set; }

        [DataMember]
        public int DishQuantity { get; set; }

        public DepartmentModel()
        {
        }

    }  // class Department


}
