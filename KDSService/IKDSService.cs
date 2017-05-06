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
        DepartmentGroup[] GetDepartmentGroups();
        Department[] GetDepartments();

        // получить список заказов
        [OperationContract]
        OrdersCltModel GetOrdersCltModel();


        [OperationContract]
        void ChangeStatus(OrderCommand command);
    }


    [DataContract]
    public class CltOrders
    {
        [DataMember]
        public List<OrdersCltModel> Orders { get; set; }

        public CltOrders(List<OrdersCltModel> orders)
        {
            Orders = orders;
        }
    }


}
