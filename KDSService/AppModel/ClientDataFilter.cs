using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.AppModel
{
    /// <summary>
    /// Класс для передачи информации от КДС-клиента службе для получения выборки заказов, определяемой данными условиями.
    /// </summary>
    public class ClientDataFilter
    {
        // фильтр по всем заказам
        public List<int> StatusesList { get; set; }         // список статусов
        public List<int> DepIDsList { get; set; }           // список Ид отделов
        public OrderGroupEnum GroupBy { get; set; }         // условие группировки заказов

        // ограничение количества элементов
        public int EndpointOrderID { get; set; }            // Ид конечного отдела
        public int EndpointOrderItemID { get; set; }        // Ид конечного элемента коллекции блюд
        public LeafDirectionEnum LeafDirection { get; set; }    // направление движения по коллекции заказов
        
        // приблизительное количество элементов заказов
        // данный параметр зависит от графических настроек клиента: количество столбцов, коэффициент масштабировния шрифтов
        public int ApproxMaxDishesCountOnPage { get; set; }

        // группировка по блюдам и суммирование количества
        public bool IsDishGroupAndSumQuatity { get; set; }

        public override string ToString()
        {
            string s1 = (this.StatusesList == null) ? "" : string.Join(",", this.StatusesList);
            string s2 = (this.DepIDsList == null) ? "" : string.Join(",", this.DepIDsList);

            string retVal = string.Format("StatusesList={0}; DepIDsList={1}; GroupBy={2}; EndpointOrderID={3}; EndpointOrderItemID={4}; LeafDirection={5}", s1, s2, this.GroupBy.ToString(), this.EndpointOrderID, this.EndpointOrderItemID, this.LeafDirection.ToString());

            return retVal;
        }
    }
}
