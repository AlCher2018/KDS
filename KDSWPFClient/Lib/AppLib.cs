using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.ViewModel;
using KDSWPFClient.View;
using IntegraLib;
using IntegraWPFLib;

namespace KDSWPFClient.Lib
{

    public static class AppLib
    {
        // общий логгер
        public static NLog.Logger _appLogger;

        static AppLib()
        {
        }

        public static void InitAppLogger()
        {
            // логгер приложения
            _appLogger = NLog.LogManager.GetLogger("appLogger");
        }

        #region app logger
        // отладочные сообщения
        // стандартные действия службы
        public static void WriteLogTraceMessage(string msg)
        {
            if ((bool)WpfHelper.GetAppGlobalValue("IsWriteTraceMessages", false) && !msg.IsNull()) _appLogger.Trace(msg);
        }
        public static void WriteLogTraceMessage(string format, params object[] args)
        {
            if ((bool)WpfHelper.GetAppGlobalValue("IsWriteTraceMessages", false)) _appLogger.Trace(format, args);
        }

        // подробная информация о преобразованиях списка заказов, полученных клиентом от службы
        public static void WriteLogOrderDetails(string msg)
        {
            if ((bool)WpfHelper.GetAppGlobalValue("IsWriteTraceMessages", false) 
                && (bool)WpfHelper.GetAppGlobalValue("TraceOrdersDetails", false))
                _appLogger.Trace(msg);
        }
        public static void WriteLogOrderDetails(string format, params object[] paramArray)
        {
            if ((bool)WpfHelper.GetAppGlobalValue("IsWriteTraceMessages", false)
                && (bool)WpfHelper.GetAppGlobalValue("TraceOrdersDetails", false))
            {
                string msg = string.Format(format, paramArray);
                _appLogger.Trace(msg);
            }
        }

        // сообщения о действиях клиента
        public static void WriteLogClientAction(string msg)
        {
            if ((bool)WpfHelper.GetAppGlobalValue("IsLogClientAction", false))
                _appLogger.Trace("cltAct|" + msg);
        }
        public static void WriteLogClientAction(string format, params object[] paramArray)
        {
            if ((bool)WpfHelper.GetAppGlobalValue("IsLogClientAction", false))
                _appLogger.Trace("cltAct|" + string.Format(format, paramArray));
        }

        public static void WriteLogInfoMessage(string msg)
        {
            if (_appLogger.IsInfoEnabled && !msg.IsNull()) _appLogger.Info(msg);
        }
        public static void WriteLogInfoMessage(string format, params object[] args)
        {
            if (_appLogger.IsInfoEnabled) _appLogger.Info(format, args);
        }

        public static void WriteLogErrorMessage(string msg)
        {
            if (_appLogger.IsErrorEnabled && !msg.IsNull()) _appLogger.Error(msg);
        }
        public static void WriteLogErrorMessage(string format, params object[] args)
        {
            if (_appLogger.IsErrorEnabled) _appLogger.Error(format, args);
        }

        #endregion

        //  ДЛЯ КОНКРЕТНОГО ПРИЛОЖЕНИЯ

        // преобразовать TimeSpan в строку
        public static string GetAppStringTS(TimeSpan tsTimerValue)
        {
            string retVal = "";

            if (tsTimerValue != TimeSpan.Zero)
            {
                retVal = (tsTimerValue.Days > 0d) ? tsTimerValue.ToString(@"d\.hh\:mm\:ss") : tsTimerValue.ToString(@"hh\:mm\:ss");
                // отрицательное время
                if (tsTimerValue.Ticks < 0) retVal = "-" + retVal;
            }

            return retVal;
        }
        // преобразовать строку в TimeSpan
        internal static TimeSpan GetTSFromString(string tsString)
        {
            TimeSpan ts = TimeSpan.Zero;
            TimeSpan.TryParse(tsString, out ts);
            return ts;
        }


        /// <summary>
        /// соединение двух ОТСОРТИРОВАННЫХ массивов
        ///  target - что обновляем (цель/получатель обновления), напр. List<OrderDishViewModel>
        ///  source - чем обновляем (источник/поставщик обновления), напр. List<OrderDishModel> 
        /// </summary>
        /// <typeparam name="T1">Получатель данных, напр. view-объект</typeparam>
        /// <typeparam name="T2">Источник данных, напр. service-объект</typeparam>
        /// <param name="targetList">Список объктов-получателей</param>
        /// <param name="sourceList">Список объектов-источников</param>
        /// <returns></returns>
        internal static bool JoinSortedLists<T1, T2>(List<T1> targetList, List<T2> sourceList) 
            where T1:IJoinSortedCollection<T2>, new() where T2: IContainIDField
        {
            bool retVal = false;
            int index = 0;
            T1 trgObj;
            // в цикле по объектам источника просматриваем целевой список в ТАКОМ ЖЕ ПОРЯДКЕ, сравнивая Ид
            foreach (T2 srcObj in sourceList)
            {
                // в источнике больше элементов, поэтому добавляем в цель
                if (index == targetList.Count)
                {
                    trgObj = new T1();
                    trgObj.FillDataFromServiceObject(srcObj, index + 1);
                    targetList.Add(trgObj);
                    retVal = true;
                }
                // если одинаковые идентификаторы, то просто обновляем целевой объект из источника
                else if (targetList[index].Id == srcObj.Id)
                {
                    trgObj = targetList[index];
                    trgObj.UpdateFromSvc(srcObj);
                    if ((trgObj is IContainInnerCollection) && ((trgObj as IContainInnerCollection).IsInnerListUpdated) && !retVal) retVal = true;
                }
                else
                {
                    // попытаться найти блюдо с таким Ид и переставить его в нужную позицию
                    trgObj = targetList.FirstOrDefault(d => d.Id == srcObj.Id);
                    if (trgObj == null)  // не найдено - ВСТАВЛЯЕМ в нужную позицию
                    {
                        trgObj = new T1();
                        trgObj.FillDataFromServiceObject(srcObj, index + 1);
                        targetList.Insert(index, trgObj);
                        retVal = true;
                    }
                    else  // переставляем и обновляем из источника
                    {
                        targetList.Remove(trgObj);
                        targetList.Insert(index, trgObj);
                        trgObj.UpdateFromSvc(srcObj);
                        if ((trgObj is IContainInnerCollection) && ((trgObj as IContainInnerCollection).IsInnerListUpdated) && !retVal) retVal = true;
                    }
                }
                index++;
            }

            // удалить блюда, которые не пришли от службы
            while (targetList.Count >= (index + 1))
            {
                targetList.RemoveAt(targetList.Count - 1);
                if (!retVal) retVal = true;
            }

            return retVal;
        }  // method

        // узнать, в каком состоянии находятся ВСЕ БЛЮДА заказа
        public static OrderStatusEnum GetStatusAllDishes(List<OrderDishViewModel> dishes)
        {
            OrderStatusEnum retVal = OrderStatusEnum.None;
            if ((dishes == null) || (dishes.Count == 0)) return retVal;

            int iLen = Enum.GetValues(typeof(OrderStatusEnum)).Length;
            int dishCount = dishes.Count;

            int[] statArray = new int[iLen];

            int iStatus;
            foreach (OrderDishViewModel modelDish in dishes)
            {
                iStatus = modelDish.DishStatusId;
                statArray[iStatus]++;
            }

            for (int i = 0; i < iLen; i++)
            {
                if (statArray[i] == dishCount) { retVal = (OrderStatusEnum)i;break; }
            }

            return retVal;
        }

        // узнать, в каком состоянии находятся ВСЕ БЛЮДА заказа отображаемых на данном КДСе цехов
        public static StatusEnum GetStatusAllDishesOwnDeps(List<OrderDishViewModel> dishes)
        {
            if ((dishes == null) || (dishes.Count == 0)) return StatusEnum.None;

            int statId = -1;
            AppDataProvider dataProvider = (AppDataProvider)WpfHelper.GetAppGlobalValue("AppDataProvider");
            foreach (OrderDishViewModel modelDish in dishes)
            {
                if (dataProvider.Departments[modelDish.DepartmentId].IsViewOnKDS)
                {
                    if (statId == -1) statId = modelDish.DishStatusId;
                    else if (statId != modelDish.DishStatusId) return StatusEnum.None;
                }
            }

            return (StatusEnum)statId;
        }

        // принадлежит ли переданный Ид цеха разрешенным цехам на этом КДСе
        internal static bool IsDepViewOnKDS(int depId, AppDataProvider dataProvider = null)
        {
            if (dataProvider == null) dataProvider = (AppDataProvider)WpfHelper.GetAppGlobalValue("AppDataProvider");

            return dataProvider.Departments[depId].IsViewOnKDS;
        }

    }  // class
}
