﻿using KDSService.AppModel;
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
        Dictionary<int, DepartmentGroup> GetDepartmentGroups();
        //    отделы
        [OperationContract]
        Dictionary<int, Department> GetDepartments();

        // получить список заказов
        //[OperationContract]
        //OrdersModel GetOrdersModel();


        [OperationContract]
        void ChangeStatus(OrderCommand command);
    }

}