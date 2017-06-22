using KDSService.AppModel;
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
            List<OrderStatusModel> list1 = ModelDicts.GetOrderStatusesList(out errMsg);
            if (list1 == null) return false;
            _statuses = new Dictionary<int, OrderStatusModel>();
            list1.ForEach(item => _statuses.Add(item.Id, item));

            // список отделов -> в словарь
            List<DepartmentModel> list2 = ModelDicts.GetDepartmentsList(out errMsg);
            if (list2 == null) return false;
            _departments = new Dictionary<int, DepartmentModel>();
            list2.ForEach(item => _departments.Add(item.Id, item));

            return true;
        }

        // СПРАВОЧНИК СТАТУСОВ ЗАКАЗА/БЛЮДА
        public static List<OrderStatusModel> GetOrderStatusesList(out string errMsg)
        {
            List<OrderStatusModel> retVal = null;
            errMsg = null;
            try
            {
                using (KDSEntities db = new KDSEntities())
                {
                    retVal = db.OrderStatus
                        .Select(os => new OrderStatusModel() {
                            Id = os.Id, Name = os.Name, AppName = os.AppName, Description = os.Description })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                errMsg = string.Format("{0}{1}", ex.Message, (ex.InnerException == null)? "" : " (" + ex.InnerException.Message + ")");
            }
            return retVal;
        }  // method


        // СПРАВОЧНИК ОТДЕЛОВ
        public static List<DepartmentModel> GetDepartmentsList(out string errMsg)
        {
            List<DepartmentModel> retVal = null;
            errMsg = null;
            try
            {
                using (KDSEntities db = new KDSEntities())
                {
                    retVal = new List<DepartmentModel>();
                    foreach (Department item in db.Department)
                    {
                        retVal.Add(new DepartmentModel(item));
                    }
                }
            }
            catch (Exception ex)
            {
                errMsg = string.Format("{0}{1}", ex.Message, (ex.InnerException == null) ? "" : " (" + ex.InnerException.Message + ")");
            }

            return retVal;
        }  // method

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
        public decimal DishQuantity { get; set; }

        public DepartmentModel()
        {
        }

        public DepartmentModel(Department dbDep): this()
        {
            Id = dbDep.Id;
            IsAutoStart = dbDep.IsAutoStart ?? false;
            Name = dbDep.Name;
            if (IsAutoStart) Name += " (автостарт)";
            UID = dbDep.UID;
            DishQuantity = dbDep.DishQuantity ?? 0;
        }

    }  // class Department


}
