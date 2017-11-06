﻿using IntegraLib;
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
            _appBrushes = new Dictionary<string, BrushesPair>()
            {
                {OrderStatusEnum.None.ToString(),
                    new View.BrushesPair() {Name="~Состояние неопределено", Background = Brushes.WhiteSmoke, Foreground=Brushes.Black} },

                { OrderStatusEnum.WaitingCook.ToString(),
                    new View.BrushesPair() {Name="ОЖИДАНИЕ начала готовки: Ручной запуск начала приготовления.", Background=Brushes.Plum, Foreground = Brushes.Black} },

                { "estimateCook",
                    new View.BrushesPair() {Name="ПЛАНОВОЕ ВРЕМЯ приготовления: блюдо находится в ожидании ручного запуска начала приготовления", LegendText = "00:00:00", 
                        Background = CfgFileHelper.GetBrushFromCfgFile("estimateCookBack", "Plum"),
                        Foreground = CfgFileHelper.GetBrushFromCfgFile("estimateCookFore", "Navy")} },

                { "estimateStart",
                    new View.BrushesPair() { Name = "ОТЛОЖЕННЫЙ СТАРТ: автоматический старт начала приготовления после окончания отсчета.", LegendText = "00:00:00",
                        Background = CfgFileHelper.GetBrushFromCfgFile("estimateStartBack", "DeepSkyBlue"),
                        Foreground = CfgFileHelper.GetBrushFromCfgFile("estimateStartFore", "Yellow") } },

                { OrderStatusEnum.Cooking.ToString(),
                    new View.BrushesPair() {Name="Блюдо находится В ПРОЦЕССЕ приготовления: таймер показывает оставшееся время приготовления", LegendText="00:00:00",
                        Background = CfgFileHelper.GetBrushFromCfgFile("statusCookingBack", "Green"),
                        Foreground = CfgFileHelper.GetBrushFromCfgFile("statusCookingFore", "Yellow") } },

                { OrderStatusEnum.Cooking.ToString()+"minus",
                    new View.BrushesPair() {Name="Блюдо находится В ПРОЦЕССЕ приготовления: таймер показывает количество просроченного времени",  LegendText="-00:00:00",
                        Background = CfgFileHelper.GetBrushFromCfgFile("statusCookingOverBack", "DarkGreen"),
                        Foreground = CfgFileHelper.GetBrushFromCfgFile("statusCookingOverFore", "Red") } },

                { OrderStatusEnum.Ready.ToString(),
                    new View.BrushesPair() {Name="Отображается таймер обратного отсчета планового времени выноса блюда", LegendText="00:00:00",
                        Background = CfgFileHelper.GetBrushFromCfgFile("statusReadyBack", "Orange"),
                        Foreground = CfgFileHelper.GetBrushFromCfgFile("statusReadyFore", "Black") } },

                { OrderStatusEnum.Ready.ToString()+"minus",
                    new View.BrushesPair() {Name="Отображается таймер просроченного времени выноса блюда", LegendText="-00:00:00",
                        Background = CfgFileHelper.GetBrushFromCfgFile("statusReadyOverBack", "Orange"),
                        Foreground = CfgFileHelper.GetBrushFromCfgFile("statusReadyOverFore", "Red") } },

                { OrderStatusEnum.ReadyConfirmed.ToString(),
                    new View.BrushesPair() {Name="Подтверждение готовности: отображается таймер обратного отсчета планового времени выноса блюда", Background=Brushes.Gold, Foreground = Brushes.Black, LegendText="00:00:00" } },

                { OrderStatusEnum.ReadyConfirmed.ToString()+"minus",
                    new View.BrushesPair() {Name="Подтверждение готовности: отображается таймер просроченного времени выноса блюда", Background=Brushes.Gold, Foreground = Brushes.Red, LegendText="-00:00:00" } },

                { OrderStatusEnum.Cancelled.ToString(),
                    new View.BrushesPair() {Name="Блюдо/заказ ОТМЕНЕНО", Background=Brushes.Salmon, Foreground = Brushes.Black} },

                { OrderStatusEnum.Took.ToString(),
                    new View.BrushesPair() {Name="~Блюдо/заказ ВЫДАНО", Background=Brushes.Blue, Foreground = Brushes.White } },

                { OrderStatusEnum.Commit.ToString(),
                    new View.BrushesPair() {Name="~Блюдо/заказ ЗАФИКСИРОВАНО", Background=Brushes.DarkBlue, Foreground = Brushes.Yellow } },

                { OrderStatusEnum.CancelConfirmed.ToString(),
                    new View.BrushesPair() {Name="~Отмена ПОДТВЕРЖДЕНА", Background=Brushes.DarkBlue, Foreground = Brushes.Yellow } },

                { "orderHeaderTimer",
                    new View.BrushesPair() {Name="~Таймер в заголовке заказа", Background=Brushes.YellowGreen, Foreground = Brushes.Black} },

                { "dishLineBase",
                    new View.BrushesPair() {Name="~Строка блюда", Background=Brushes.White, Foreground = Brushes.Black} },

                { "ingrLineBase",
                    new View.BrushesPair() {Name="~Строка ингредиента", Background=Brushes.White, Foreground = Brushes.DarkViolet } }
            };
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

    }  // class BrushesPair

}
