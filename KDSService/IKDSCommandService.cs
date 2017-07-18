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
        void LockOrder(int orderId);
        [OperationContract]
        void DelockOrder(int orderId);

        // заблокировать/разблокировать блюдо от изменения по таймеру
        [OperationContract]
        void LockDish(int dishId);
        [OperationContract]
        void DelockDish(int dishId);


        // изменение статуса заказа
        [OperationContract]
        void ChangeOrderStatus(int orderId, OrderStatusEnum orderStatus);


        // изменение статуса блюда
        [OperationContract]
        void ChangeOrderDishStatus(int orderId, int orderDishId, OrderStatusEnum orderDishStatus);
    }
}
