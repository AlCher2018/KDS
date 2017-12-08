using IntegraLib;
using KDSWPFClient.ServiceReference1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace KDSWPFClient.View
{
    public static class BrushHelper
    {
        // словарь кистей (фон и шрифт)
        // для статусов заказов/блюд в качестве ключа используется стр.знач.перечисления OrderStatusEnum
        // в качестве значения используется класс BrushesPair, в котором содержится пара кистей - для фона и текста
        // символ ~ в начале наименования указывает на то, что данный объкт не надо отображать в легенде
        private static Dictionary<string, BrushesPair> _appBrushes;

        // создать в статическом конструкторе
        static BrushHelper()
        {
            _appBrushes = new Dictionary<string, BrushesPair>();
        }

        public static void FillAppBrushes()
        {
            addBrushesPair(OrderStatusEnum.None.ToString(), "~Состояние неопределено", null, null, Brushes.Black, Brushes.WhiteSmoke);

            addBrushesPair(OrderStatusEnum.WaitingCook.ToString(), "ОЖИДАНИЕ начала готовки: Ручной запуск начала приготовления.", "", "waitingCook", Brushes.Black, Brushes.Plum);

            addBrushesPair("estimateCook", "ПЛАНОВОЕ ВРЕМЯ приготовления: блюдо находится в ожидании ручного запуска начала приготовления", "00:00:00", "estimateCook", Brushes.Navy, Brushes.Plum);

            addBrushesPair("estimateStart", "ОТЛОЖЕННЫЙ СТАРТ: автоматический старт начала приготовления после окончания отсчета.", "00:00:00", "estimateStart", Brushes.Yellow, Brushes.DeepSkyBlue);

            addBrushesPair(OrderStatusEnum.Cooking.ToString(), "Блюдо находится В ПРОЦЕССЕ приготовления: таймер показывает оставшееся время приготовления", "00:00:00", "statusCooking", Brushes.Yellow, Brushes.Green);

            addBrushesPair(OrderStatusEnum.Cooking.ToString() + "minus", "Блюдо находится В ПРОЦЕССЕ приготовления: таймер показывает количество просроченного времени", "-00:00:00", "statusCookingOver", Brushes.Red, Brushes.DarkGreen);

            addBrushesPair(OrderStatusEnum.Ready.ToString(), "Отображается таймер обратного отсчета планового времени выноса блюда", "00:00:00", "statusReady", Brushes.Black, Brushes.Orange);

            addBrushesPair(OrderStatusEnum.ReadyConfirmed.ToString() + OrderStatusEnum.Ready.ToString(), "Отображается таймер автоматического перехода в ПодтвГотово", "00:00:00", "readyConfirmedReady", Brushes.Green, Brushes.Orange);

            addBrushesPair(OrderStatusEnum.Ready.ToString() + "minus", "Отображается таймер просроченного времени выноса блюда", "-00:00:00", "statusReadyOver", Brushes.Red, Brushes.Orange);

            addBrushesPair(OrderStatusEnum.ReadyConfirmed.ToString(), "Подтверждение готовности: отображается таймер обратного отсчета планового времени выноса блюда", "00:00:00", "readyConfirmed", Brushes.Black, Brushes.Gold);

            addBrushesPair(OrderStatusEnum.ReadyConfirmed.ToString() + "minus", "Подтверждение готовности: отображается таймер просроченного времени выноса блюда", "-00:00:00", "readyConfirmedOver", Brushes.Red, Brushes.Gold);

            addBrushesPair(OrderStatusEnum.Cancelled.ToString(), "Блюдо/заказ ОТМЕНЕНО", null, "statusCancelled", Brushes.Black, Brushes.Salmon);

            addBrushesPair(OrderStatusEnum.Took.ToString(), "~Блюдо/заказ ВЫДАНО", null, "statusTook", Brushes.White, Brushes.Blue);

            addBrushesPair(OrderStatusEnum.Commit.ToString(), "~Блюдо/заказ ЗАФИКСИРОВАНО", null, "statusCommit", Brushes.Yellow, Brushes.DarkBlue);
            
            addBrushesPair(OrderStatusEnum.CancelConfirmed.ToString(), "~Отмена ПОДТВЕРЖДЕНА", null, "statusCancelConfirmed", Brushes.Yellow, Brushes.DarkBlue);

            addBrushesPair("orderHeaderTimer", "~Таймер в заголовке заказа", null, "orderHeaderTimer", Brushes.Black, Brushes.YellowGreen);

            addBrushesPair("dishLineBase", "~Строка блюда", null, "dishLineBase", Brushes.Black, Brushes.White);
            addBrushesPair("ingrLineBase", "~Строка ингредиента", null, "ingrLineBase", Brushes.DarkViolet, Brushes.White);

            addBrushesPair("delimiterBreakPage", "~Разделитель разрыва заказа на странице", null, "pageBreak", Brushes.White, Brushes.Blue);
        }

        private static void addBrushesPair(string dictionaryKey, string brushPairName, string legendText, string cfgElementName, Brush foregroundBrush, Brush backgroundBrush)
        {
            BrushesPair bp;
            bp = new BrushesPair() { Name = brushPairName, LegendText = legendText };
            bp.SetBrushes(cfgElementName, foregroundBrush, backgroundBrush);

            _appBrushes.Add(dictionaryKey, bp);
        }

        // использовать для внешних обращений
        public static Dictionary<string, BrushesPair> AppBrushes { get { return _appBrushes; } }

    }  // class BrushHelper


    public class BrushesPair
    {
        public string Name { get; set; }

        public string LegendText { get; set; }  // текст, отображаемый в легенде

        // основная пара
        public Brush Background { get; set; }

        public Brush Foreground { get; set; }

        public void SetBrushes(string cfgElementName, Brush defaultForeBrush, Brush defaultBackBrush)
        {
            if (string.IsNullOrEmpty(cfgElementName))
            {
                Foreground = defaultForeBrush; Background = defaultBackBrush;
            }
            // иначе берем из config-файла, значение: [строка цвета шрифта];[строка цвета фона]
            else
            {
                string cfgValue = CfgFileHelper.GetAppSetting(cfgElementName);
                if (string.IsNullOrEmpty(cfgValue))
                {
                    Foreground = defaultForeBrush; Background = defaultBackBrush;
                }
                else
                {
                    string[] cfgVals = cfgValue.Split('|');

                    Foreground = DrawHelper.GetBrushByName(cfgVals[0]);

                    if (cfgVals.Length > 1)
                        Background = DrawHelper.GetBrushByName(cfgVals[1]);
                    else
                        Background = defaultBackBrush;
                }
            }
        }

    }  // class BrushesPair

}
