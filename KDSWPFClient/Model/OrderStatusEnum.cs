using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSWPFClient
{
    // состояния блюда/заказа
    public enum OrderStatusEnum
    {
        None = -1,
        WaitingCook = 0,        // блюдо/заказ находится в ожидании начала готовки
        Cooking = 1,            // блюдо/заказ находится в процессе приготовления
        Ready = 2,              // блюдо/заказ готов
        Took = 3,               // блюдо/заказ забран официантом (выдан, вынесен, подан клиенту)
        Cancelled = 4,          // блюдо/заказ отменено
        Commit = 5,             // заказ зафиксирован (закрыт от возвратов и отмен после оплаты)
        CancelConfirmed = 6     // отмена подтверждена
    }

}
