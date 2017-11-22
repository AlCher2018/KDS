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
        List<OrderStatusModel> GetOrderStatuses(string machineName);

        //    отделы
        [OperationContract]
        List<DepartmentModel> GetDepartments(string machineName);


        // ПОЛУЧИТЬ СПИСОК ЗАКАЗОВ
        // клиент передает службе свое имя (машины), список отображаемых статусов и цехов, тип группировки заказов
        // начальные Ид заказа и блюда, направление движения и приблизительное кол-во элементов
        // служба возвращает коллекцию заказов, отобранную с заданными фильтрами,
        // с признаками наличия "хвостов" спереди и сзади для отображения на клиенте кнопок прокрутки
        [OperationContract]
        ServiceResponce GetOrders(string machineName, ClientDataFilter clientFilter);

        // *** ПОЛУЧИТЬ НАСТРОЙКИ ИЗ CONFIG-ФАЙЛА ХОСТА ***
        [OperationContract]
        Dictionary<string, object> GetHostAppSettings(string machineName);

        [OperationContract]
        void SetExpectedTakeValue(string machineName, int value);

    }  // class
}
