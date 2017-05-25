using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace KDSWPFClient.Model
{
    public static class StateGraphHelper
    {

        #region Визуальные элементы состояний и переходов
        // кисти фона и текста
        public static void SetStateButtonBrushes(OrderStatusEnum eState, out SolidColorBrush backgroundBrush, out SolidColorBrush foregroundBrush)
        {
            ResourceDictionary resDict = App.Current.Resources;

            switch (eState)
            {
                case OrderStatusEnum.None:
                    backgroundBrush = new SolidColorBrush(Colors.White);
                    foregroundBrush = new SolidColorBrush(Colors.Black);
                    break;

                case OrderStatusEnum.WaitingCook:
                    backgroundBrush = (SolidColorBrush)resDict["orderHeaderBackBrushWaitingCook"];
                    foregroundBrush = (SolidColorBrush)resDict["orderHeaderForeBrushWaitingCook"];
                    break;

                case OrderStatusEnum.Cooking:
                    backgroundBrush = (SolidColorBrush)resDict["orderHeaderBackBrushCooking"];
                    foregroundBrush = (SolidColorBrush)resDict["orderHeaderForeBrushCooking"];
                    break;

                case OrderStatusEnum.Ready:
                    backgroundBrush = (SolidColorBrush)resDict["orderHeaderBackBrushReady"];
                    foregroundBrush = (SolidColorBrush)resDict["orderHeaderForeBrushReady"];
                    break;

                case OrderStatusEnum.Took:
                    backgroundBrush = (SolidColorBrush)resDict["orderHeaderBackBrushTook"];
                    foregroundBrush = (SolidColorBrush)resDict["orderHeaderForeBrushTook"];
                    break;

                case OrderStatusEnum.Cancelled:
                    backgroundBrush = (SolidColorBrush)resDict["orderHeaderBackBrushCancelled"];
                    foregroundBrush = (SolidColorBrush)resDict["orderHeaderForeBrushCancelled"];
                    break;

                case OrderStatusEnum.Commit:
                    backgroundBrush = new SolidColorBrush(Colors.DarkBlue);
                    foregroundBrush = new SolidColorBrush(Colors.Yellow); 
                    break;

                case OrderStatusEnum.CancelConfirmed:
                    backgroundBrush = new SolidColorBrush(Colors.DarkBlue);
                    foregroundBrush = new SolidColorBrush(Colors.Yellow);
                    break;

                default:
                    backgroundBrush = new SolidColorBrush(Colors.White);
                    foregroundBrush = new SolidColorBrush(Colors.Black);
                    break;
            }
        }  // method

        public static void SetStateButtonTexts(OrderStatusEnum eState, out string btnText1, out string btnText2, bool isOrder, bool isReturnCooking)
        {
            btnText1 = null; btnText2 = null;

            switch (eState)
            {
                case OrderStatusEnum.None:
                    break;

                case OrderStatusEnum.WaitingCook:
                    btnText1 = "ОЖИДАНИЕ";
                    break;

                case OrderStatusEnum.Cooking:
                    btnText1 = (isReturnCooking) ? "ВЕРНУТЬ" : "ГОТОВИТЬ";
                    btnText2 = (isReturnCooking) 
                        ? string.Format("Возврат {0} в очередь приготовления", (isOrder ? "заказа" : "блюда")) 
                        : string.Format("Начать приготовление {0}", (isOrder ? "заказа" : "блюда"));
                    break;

                case OrderStatusEnum.Ready:
                    btnText1 = "ГОТОВО";
                    btnText2 = string.Format("{0} готово и может быть выдано на раздаче", (isOrder ? "Заказ" : "Блюдо"));
                    break;

                case OrderStatusEnum.Took:
                    btnText1 = "ЗАБРАТЬ";
                    btnText2 = string.Format("Забрать {0} и отнести его Клиенту", (isOrder ? "заказ" : "блюдо"));
                    break;

                case OrderStatusEnum.Cancelled:
                    btnText1 = "ОТМЕНИТЬ";
                    btnText2 = string.Format("Отменить приготовление {0}", (isOrder ? "заказа" : "блюда"));
                    break;

                case OrderStatusEnum.Commit:
                    btnText1 = "ЗАФИКСИРОВАТЬ";
                    btnText2 = string.Format("Зафиксировать, т.е. запретить изменять статус {0}", (isOrder ? "заказа" : "блюда"));
                    break;

                case OrderStatusEnum.CancelConfirmed:
                    btnText1 = "ПОДТВЕРДИТЬ ОТМЕНУ";
                    btnText2 = string.Format("Подтвердить отмену приготовления {0}", (isOrder ? "заказа" : "блюда"));
                    break;
                default:
                    break;
            }
        }

        public static string GetStateDescription(OrderStatusEnum eState, bool isOrder = false)
        {
            string retVal = null;

            switch (eState)
            {
                case OrderStatusEnum.None:
                    break;
                case OrderStatusEnum.WaitingCook:
                    retVal = "ожидает начала приготовления";
                    break;
                case OrderStatusEnum.Cooking:
                    retVal = "находится в процессе приготовления";
                    break;
                case OrderStatusEnum.Ready:
                    retVal = (isOrder) ? "готов к выдаче" : "готово к выдаче";
                    break;
                case OrderStatusEnum.Took:
                    retVal = (isOrder) ? "выдан клиенту" : "выдано клиенту";
                    break;
                case OrderStatusEnum.Cancelled:
                    retVal = (isOrder) ? "отменен" : "отменено";
                    break;
                case OrderStatusEnum.Commit:
                    retVal = (isOrder) ? "зафиксирован" : "зафиксировано";
                    break;
                case OrderStatusEnum.CancelConfirmed:
                    retVal = "ожидает подтверждения отмены";
                    break;
                default:
                    break;
            }

            return retVal;
        }


        #endregion

    }  // class
}
