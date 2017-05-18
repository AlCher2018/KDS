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
        List<OrderStatusModel> GetOrderStatusList();
        //    группы отделов
        [OperationContract]
        Dictionary<int, DepartmentGroupModel> GetDepartmentGroups();
        //    отделы
        [OperationContract]
        Dictionary<int, DepartmentModel> GetDepartments();


        // ПОЛУЧИТЬ СПИСОК ЗАКАЗОВ
        [OperationContract]
        List<OrderModel> GetOrders();     // все

        [OperationContract]
        // по состоянию блюд, ид отдела или ид группы отделов
        List<OrderModel> GetOrdersByConditions(OrderStatusEnum dishStatus, int departmentId, int departmentGroupId);

    }
}
