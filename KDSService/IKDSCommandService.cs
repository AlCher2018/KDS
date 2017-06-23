using System.Collections.Generic;
using System.ServiceModel;
using KDSService.AppModel;



namespace KDSService
{
    [ServiceContract]
    public interface IKDSCommandService
    {
        // изменение статуса заказа
        [OperationContract]
        void ChangeOrderStatus(int orderId, OrderStatusEnum orderStatus);


        // изменение статуса блюда
        [OperationContract]
        void ChangeOrderDishStatus(int orderId, int orderDishId, OrderStatusEnum orderDishStatus);
    }
}
