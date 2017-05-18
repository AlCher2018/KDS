using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSWPFClient.Model
{
    public enum ServiceOrderQueueEnum
    {
        None,               // никакого запроса
        AllOrders,          // все заказы
        ByDishStatus,       // по состоянию блюда
        ByDepartment,       // по отделу/напр.печати
        ByDepartmentGroup   // по группе отделов
    }
}
