using System.Collections.Generic;
using System.ServiceModel;
using KDSService.AppModel;
using IntegraLib;

namespace KDSService
{
    [ServiceContract]
    public interface IKDSCommandService
    {
        // заблокировать/разблокировать заказ от изменения по таймеру
        [OperationContract]
        bool LockOrder(string machineName, int orderId);
        [OperationContract]
        bool DelockOrder(string machineName, int orderId);

        // заблокировать/разблокировать блюдо от изменения по таймеру
        [OperationContract]
        bool LockDish(string machineName, int dishId);
        [OperationContract]
        bool DelockDish(string machineName, int dishId);


        // изменение статуса заказа
        [OperationContract]
        bool ChangeOrderStatus(string machineName, int orderId, int orderStatusId);

        // изменение статуса блюда
        [OperationContract]
        bool ChangeOrderDishStatus(string machineName, int orderId, int dishId, int dishStatusId);

        // создание файла-уведомления для ЗАКАЗА
        [OperationContract]
        bool CreateNoticeFileForOrder(string machineName, int orderId, string dishIdsStr);

        // создание файла-уведомления для БЛЮДА
        [OperationContract]
        bool CreateNoticeFileForDish(string machineName, int orderId, int orderDishId);

    }
}
