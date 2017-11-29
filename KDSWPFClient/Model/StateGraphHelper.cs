using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.View;
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
        public static void SetStateButtonBrushes(OrderStatusEnum eState, out Brush backgroundBrush, out Brush foregroundBrush)
        {
            Dictionary<string, BrushesPair> appBrushes = BrushHelper.AppBrushes;

            BrushesPair bp = appBrushes[eState.ToString()];
            backgroundBrush = bp.Background;
            foregroundBrush = bp.Foreground;
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
                    if (isOrder)
                        btnText2 = "Заказ полностью готов и может быть весь выдан на раздаче";
                    else
                        btnText2 = "Блюдо готово и может быть выдано на раздаче";
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

                case OrderStatusEnum.ReadyConfirmed:
                    btnText1 = "ПОДТВЕРДИТЬ ГОТОВНОСТЬ";
                    btnText2 = string.Format("Подтвердить готовность {0}", (isOrder ? "заказа" : "блюда"));
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
                    retVal = "ОЖИДАЕТ начала приготовления";
                    break;
                case OrderStatusEnum.Cooking:
                    retVal = "находится В ПРОЦЕССЕ приготовления";
                    break;
                case OrderStatusEnum.Ready:
                    retVal = (isOrder) ? "ГОТОВ к выдаче" : "ГОТОВО к выдаче";
                    break;
                case OrderStatusEnum.Took:
                    retVal = (isOrder) ? "ВЫДАН клиенту" : "ВЫДАНО клиенту";
                    break;
                case OrderStatusEnum.Cancelled:
                    retVal = (isOrder) ? "ОТМЕНЕН" : "ОТМЕНЕНО";
                    break;
                case OrderStatusEnum.Commit:
                    retVal = (isOrder) ? "ЗАФИКСИРОВАН" : "ЗАФИКСИРОВАНО";
                    break;
                case OrderStatusEnum.CancelConfirmed:
                    retVal = "ожидает подтверждения ОТМЕНЫ";
                    break;
                case OrderStatusEnum.ReadyConfirmed:
                    retVal = "ожидает подтверждения ГОТОВНОСТИ";
                    break;
                default:
                    break;
            }

            return retVal;
        }


        public static string GetStateTabName(OrderStatusEnum eState)
        {
            string retVal = null;

            switch (eState)
            {
                case OrderStatusEnum.None:
                    break;
                case OrderStatusEnum.WaitingCook:
                    retVal = "В ожидании";
                    break;
                case OrderStatusEnum.Cooking:
                    retVal = "В процессе";
                    break;
                case OrderStatusEnum.Ready:
                    retVal = "Готовые";
                    break;
                case OrderStatusEnum.Took:
                    retVal = "Выданные";
                    break;
                case OrderStatusEnum.Cancelled:
                    retVal = "Отмененные";
                    break;
                case OrderStatusEnum.Commit:
                    retVal = "Зафиксированные";
                    break;
                case OrderStatusEnum.CancelConfirmed:
                    retVal = "ПодтвОтмена";
                    break;
                case OrderStatusEnum.ReadyConfirmed:
                    retVal = "ПодтвГотов";
                    break;
                default:
                    break;
            }

            return retVal;
        }

        #endregion

    }  // class
}
