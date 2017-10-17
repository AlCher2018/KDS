using System.Collections.Generic;
using System.ServiceModel;
using KDSService.AppModel;



namespace KDSService
{
    [ServiceContract]
    public interface IKDSCommandService
    {
        // заблокировать/разблокировать заказ от изменения по таймеру
        [OperationContract]
        void LockOrder(string machineName, int orderId);
        [OperationContract]
        void DelockOrder(string machineName, int orderId);

        // заблокировать/разблокировать блюдо от изменения по таймеру
        [OperationContract]
        void LockDish(string machineName, int dishId);
        [OperationContract]
        void DelockDish(string machineName, int dishId);


        // изменение статуса заказа
        [OperationContract]
        void ChangeOrderStatus(string machineName, int orderId, OrderStatusEnum orderStatus);


        // изменение статуса блюда
        [OperationContract]
        void ChangeOrderDishStatus(string machineName, int orderId, int orderDishId, OrderStatusEnum orderDishStatus);
    }
}
