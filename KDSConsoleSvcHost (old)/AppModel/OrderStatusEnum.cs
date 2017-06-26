using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.AppModel
{
    // состояния блюда/заказа
    public enum OrderStatusEnum
    {
        None = -1,
        WaitingCook = 0,        // Заказ/блюдо находится в ожидании начала готовки
        Cooking = 1,            // Заказ/блюдо находится в процессе приготовления
        Ready = 2,              // Заказ/блюдо готов
        Took = 3,               // Заказ/блюдо забран официантом (выдан, вынесен, подан клиенту)
        Cancelled = 4,          // Заказ/блюдо отменено
        Commit = 5,             // заказ зафиксирован (закрыт от возвратов и отмен после оплаты)
        CancelConfirmed = 6,    // отмена подтверждена
        Transferred = 7,        // Заказ/блюдо перемещено на другой стол
        ReadyConfirmed = 8,     // предварительно готов: устанавливает повар, подтверждает шеф-повар
        YesterdayNotTook = 9    // вчерашний заказ/блюдо не был выдан
    }

}
