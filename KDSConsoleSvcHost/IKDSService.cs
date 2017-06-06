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
        // - являются ли ингредиенты независимыми?
        [OperationContract]
        bool GetIsIngredientsIndependent();

        // - планируемое время выноса блюда (ExpectedTake)
        [OperationContract]
        int GetExpectedTakeValue();
        [OperationContract]
        void SetExpectedTakeValue(int value);

        // - надо ли подтверждать состояние ГОТОВ (состояние ReadyConfirmed)
        [OperationContract]
        bool GetUseReadyConfirmedState();

    }  // class
}
