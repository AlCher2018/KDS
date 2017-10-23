using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.AppModel
{
    // информация о кдс-клиенте
    public class ClientInfo
    {
        public string Name { get; set; }

        // флаг чтения клиентом заказов из внутренней коллекции
        public bool GetOrdersFlag { get; set; }

        // флаг инициирования клиентом изменения данных в БД
        public bool SetDataFlag { get; set; }
    }
}
