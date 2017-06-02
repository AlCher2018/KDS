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

        // глобальный параметр ExpectedTake
        [OperationContract]
        int GetExpectedTakeValue();

        [OperationContract]
        void SetExpectedTakeValue(int value);

        [OperationContract]
        bool GetIsIngredientsIndependent();
    }
}
