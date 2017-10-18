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
        List<OrderStatusModel> GetOrderStatuses(string machineName);

        //    отделы
        [OperationContract]
        List<DepartmentModel> GetDepartments(string machineName);


        // ПОЛУЧИТЬ СПИСОК ЗАКАЗОВ
        // клиент передает службе свое имя (машины), список отображаемых статусов и цехов, тип отображения данных
        [OperationContract]
        List<OrderModel> GetOrders(string machineName, List<int> clientStatusIDs, List<int> clientDepIDs, OrderGroupEnum clientGroupBy);

        // *** ПОЛУЧИТЬ НАСТРОЙКИ ИЗ CONFIG-ФАЙЛА ХОСТА ***
        [OperationContract]
        Dictionary<string, object> GetHostAppSettings(string machineName);

        [OperationContract]
        void SetExpectedTakeValue(string machineName, int value);

    }  // class
}
