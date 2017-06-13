using System.Collections.Generic;
using System.ServiceModel;
using KDSService.AppModel;


namespace KDSService
{
    // внешний инрефейс службы

    [ServiceContract]
    public interface IKDSService
    {
        // получить словари
        //    статусов заказа/блюда
        [OperationContract]
        List<OrderStatusModel> GetOrderStatuses();

        //    отделы
        [OperationContract]
        List<DepartmentModel> GetDepartments();


        // ПОЛУЧИТЬ СПИСОК ЗАКАЗОВ
        [OperationContract]
        List<OrderModel> GetOrders();

        // *** ПОЛУЧИТЬ НАСТРОЙКИ ИЗ CONFIG-ФАЙЛА ХОСТА ***
        [OperationContract]
        Dictionary<string, object> GetHostAppSettings();

        [OperationContract]
        void SetExpectedTakeValue(int value);

    }  // class
}
