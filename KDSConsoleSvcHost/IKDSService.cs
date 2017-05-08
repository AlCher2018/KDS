using KDSService.AppModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace KDSService
{
    // внешний инрефейс службы

    [ServiceContract]
    public interface IKDSService
    {
        // получить словари
        //    группы отделов
        [OperationContract]
        Dictionary<int, DepartmentGroupModel> GetDepartmentGroups();
        //    отделы
        [OperationContract]
        Dictionary<int, DepartmentModel> GetDepartments();

        // получить список заказов
        [OperationContract]
        List<OrderModel> GetOrders();


        [OperationContract]
        void ChangeStatus(OrderCommand command);
    }

}
