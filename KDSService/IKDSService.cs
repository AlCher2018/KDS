using KDSService.AppModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace KDSService
{
    // внешний инрефейс службы

    [ServiceContract]
    public interface IKDSService
    {
        [OperationContract]
        // получить список заказов
        List<Order> GetOrders();

        [OperationContract]
        void ChangeOrderStatus(OrderCommand command);
    }
}
