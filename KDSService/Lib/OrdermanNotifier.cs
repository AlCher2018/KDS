using IntegraLib;
using KDSService.AppModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.Lib
{
    /// <summary>
    /// Класс-уведомитель OdermanServer-а об изменении статуса заказа
    /// Когда заказ ГОТОВ, то класс создает файл в указанной папке (NoticeOrdermanFolder в конфиге) текстовый файл.
    /// Папку периодически читает OrdermanServer.exe и, если находит там файлы, то их читает, парсит и рассылает
    /// уведомления о готовности заказа на Orderman-терминалы.
    /// </summary>

/* Пример файла:
Order UID: 9fca982b-7913-11e7-988d-a45d36c3cc51
Order Id: 264
Order Number: 18
Order Status: 2 (Ready)
Waiter Name: 1104 Kyiv Arena REST
*/
    class OrdermanNotifier
    {
        public static bool IsEnable { get { return AppProperties.GetBoolProperty("NoticeOrdermanFeature"); } }

        private OrderModel _order;

        public OrdermanNotifier(OrderModel order)
        {
            _order = order;
        }

        public bool CreateNoticeFileForOrder()
        {
            OrderStatusEnum status = (OrderStatusEnum)_order.OrderStatusId;
            string logMsg = $"Создание файла-уведомления для заказа № {_order.Number} (id {_order.Id}, status {status.ToString()})...";
            writeLogMsg(logMsg);

            // check notice folder
            string sResult = null;
            string folder = (string)AppProperties.GetProperty("NoticeOrdermanFolder");
            bool bResult = IntegraLib.AppEnvironment.CheckFolder(folder, out sResult);
            if (bResult == false)
            {
                writeLogMsg(" - " + sResult);
                return false;
            }
            folder = sResult;

            bool retVal = false;
            string fileName = null, fileText = null;
            OrderStatusEnum toFileStatus = (OrderStatusEnum)_order.OrderStatusId;
            if (toFileStatus == OrderStatusEnum.ReadyConfirmed) toFileStatus = OrderStatusEnum.Ready;
            fileText = string.Format("Order UID: {1}{0}Order Id: {2}{0}Order Number: {3}{0}Order Status: {4} ({5}){0}Write Time: {7}{0}Waiter Name: {6}{0}Table Name: {8}",
                Environment.NewLine, _order.Uid, _order.Id, _order.Number,
                ((int)toFileStatus).ToString(), toFileStatus.ToString(),
                _order.Waiter, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                _order.TableName);

            // для терминального состояния Готов ЗАКАЗА
            if (_order.IsReadyStatusFinal() == true)
            {
                fileName = $"{folder}ordNumber_{_order.Number}" + ".txt";
            }

            // иначе вывести в файл-уведомление БЛЮДА в состоянии Готово
            else
            {
                List<OrderDishModel> readyDishes = _order.GetDishesReadyStatFin();
                int cnt = readyDishes.Count;
                if (cnt > 0)
                {
                    IntegraLib.StringHelper.SBufClear();
                    foreach (OrderDishModel dish in readyDishes)
                    {
                        logMsg = $"   - блюдо '{dish.Name}' id {dish.Id}, status {((OrderStatusEnum)dish.DishStatusId).ToString()}";
                        writeLogMsg(logMsg);
                        IntegraLib.StringHelper.SBufAppendText(Environment.NewLine + getDishStrForNoticeFile(dish));
                    }
                    fileText += IntegraLib.StringHelper.SBufGetString();
                    fileName = $"{folder}ordNumber_{_order.Number} ({cnt.ToString()} dishes)" + ".txt";
                }
                else
                {
                    writeLogMsg(" - нет блюд в состоянии ГОТОВ");
                }
            }

            if ((fileName != null) && (fileText != null))
            {
                try
                {
                    System.IO.File.WriteAllText(fileName, fileText);

                    writeLogMsg($" - файл '{fileName}' создан успешно");
                    retVal = true;
                }
                catch (Exception ex)
                {
                    writeLogMsg(" - Error: " + ex.Message);
                }
            }

            return retVal;
        }

        // создание файла уведомления о состоянии БЛЮДА
        // для заказа создается файл [номер заказа].txt, для блюда - [номер заказа]([id блюда]).txt
        // для блюда, не ингредиента, только, если сам заказ еще не готов и NoticeOrdermanDishNotice = 1 (создавать файл-уведомления для блюда)
        public bool CreateNoticeFileForDishes(string orderDishIds)
        {
            string logMsg = $"Создание файла состояния для блюд/id {orderDishIds} заказа № {_order.Number} (id {_order.Id})...";
            writeLogMsg(logMsg);

            if (orderDishIds.IsNull())
            {
                writeLogMsg(" - перечень id блюд - пустой!");
                return false;
            }

            // check notice folder
            string sResult = null;
            string folder = (string)AppProperties.GetProperty("NoticeOrdermanFolder");
            bool bResult = IntegraLib.AppEnvironment.CheckFolder(folder, out sResult);
            if (bResult == false)
            {
                writeLogMsg(" - " + sResult);
                return false;
            }
            folder = sResult;

            bool retVal = false;
            string fileName = null, fileText = null;
            OrderStatusEnum toFileStatus = (OrderStatusEnum)_order.OrderStatusId;
            if (toFileStatus == OrderStatusEnum.ReadyConfirmed) toFileStatus = OrderStatusEnum.Ready;
            fileText = string.Format("Order UID: {1}{0}Order Id: {2}{0}Order Number: {3}{0}Order Status: {4} ({5}){0}Waiter Name: {6}",
                Environment.NewLine, _order.Uid, _order.Id, _order.Number,
                ((int)toFileStatus).ToString(), toFileStatus.ToString(),
                _order.Waiter);

            int[] ids = orderDishIds.Split(';').Select(s => s.ToInt()).ToArray();
            if (ids.Length > 0)
            {
                IntegraLib.StringHelper.SBufClear();
                OrderDishModel dish;
                foreach (int dishId in ids)
                {
                    if (_order.Dishes.ContainsKey(dishId))
                    {
                        dish = _order.Dishes[dishId];
                        IntegraLib.StringHelper.SBufAppendText(Environment.NewLine + getDishStrForNoticeFile(dish));
                    }
                }
                fileText += IntegraLib.StringHelper.SBufGetString();

                fileName = $"{folder}ordNumber_{_order.Number} (dishes {orderDishIds})" + ".txt";
                try
                {
                    System.IO.File.WriteAllText(fileName, fileText);

                    writeLogMsg($" - файл '{fileName}' создан успешно");
                    retVal = true;
                }
                catch (Exception ex)
                {
                    writeLogMsg(" - Error: " + ex.Message);
                }
            }

            return retVal;
        }

        private void writeLogMsg(string logMsg)
        {
            AppLib.WriteLogTraceMessage("OmanNtfr|" + logMsg);
        }

        private string getDishStrForNoticeFile(OrderDishModel dishModel)
        {
            OrderStatusEnum toFileStatus = (OrderStatusEnum)dishModel.DishStatusId;
            if (toFileStatus == OrderStatusEnum.ReadyConfirmed) toFileStatus = OrderStatusEnum.Ready;

            return string.Format("Dish: {1};{2}{0}Dish Status: {3} ({4})",
                Environment.NewLine,
                dishModel.Id, dishModel.Name,
                (int)toFileStatus, toFileStatus.ToString());
        }

        /*
    // создание файла для Одермана - только для БЛЮДА, НЕ для ингредиента
    if (result && dishModel.ParentUID.IsNull())
    {
        // только для терминального окончания готовки и включенных уведомлений (получаем от сервиса)
        if (finReady && WpfHelper.GetAppGlobalBool("NoticeOrdermanFeature"))
        {
            dataProvider.CreateNoticeFileForDish(orderModel.Id, dishModel.Id);
        }
    }

    // создание файла-уведомления для Одермана, если это терминальный статус Готово и 
    // статус всех блюд был успешно изменен и включено создание файлов-уведомлений
    if (finReady && WpfHelper.GetAppGlobalBool("NoticeOrdermanFeature") 
        && (dishIds.Count > 0))
    {
        // Id блюд с измененными статусами
        string dishIdsToSvc = string.Join(";", dishIds.Select<int, string>(item => item.ToString()));
        dataProvider.CreateNoticeFileForOrder(orderModel.Id, dishIdsToSvc);
    }
         
         */

    }
}  // class
