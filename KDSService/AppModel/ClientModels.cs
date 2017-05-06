using KDSService.AppModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.AppModel
{
    // классы, которые будут предоставляться клиентам для отображения информации

    // класс ORDERS для клиентов, обертка для массива
    [DataContract]
    public class OrdersCltModel
    {
        [DataMember]
        public string ErrorMsg { get; set; }

        [DataMember]
        public OrderCltModel[] Orders { get; set; }
    }

    [DataContract]
    public class OrderCltModel
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public int Status { get; set; }

        [DataMember]
        public string UID { get; set; }

        [DataMember]
        public int OrderNumber { get; set; }

        [DataMember]
        public string HallName { get; set; }

        [DataMember]
        public string TableName { get; set; }

        [DataMember]
        public string Waiter { get; set; }

        [DataMember]
        public OrderDishCltModel[] Dishes { get; set; }

    }

    [DataContract]
    public class OrderDishCltModel
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Name { get; set; }
    }

}
