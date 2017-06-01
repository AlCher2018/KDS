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
        // получить словарь кистей (фон и шрифт) в appBrushes
        // для статусов заказов/блюд в качестве ключа используется стр.знач.перечисления OrderStatusEnum
        // в качестве значения используется класс BrushesPair, в котором содержится основная пара и словарь неосновных пар
        // символ ~ в начале наименования указывает на то, что данный объкт не надо отображать в легенде
        public static Dictionary<string, BrushesPair> GetAppBrushes()
        {
            Dictionary<string, BrushesPair> appBrushes = new Dictionary<string, BrushesPair>()
            {
                {OrderStatusEnum.None.ToString(),
                    new View.BrushesPair() {Name="~Состояние неопределено", Background = Brushes.WhiteSmoke, Foreground=Brushes.Black} },
                {OrderStatusEnum.WaitingCook.ToString(),
                    new View.BrushesPair() {Name="ОЖИДАНИЕ начала готовки", Background=Brushes.Plum, Foreground = Brushes.Black} },
                {OrderStatusEnum.Cooking.ToString(),
                    new View.BrushesPair() {Name="В ПРОЦЕССЕ приготовления", Background=Brushes.Green, Foreground = Brushes.White, LegendText="00:00:00" } },
                {OrderStatusEnum.Ready.ToString(),
                    new View.BrushesPair() {Name="Блюдо/заказ ГОТОВО, отображается время ожидания выдачи", Background=Brushes.Orange, Foreground = Brushes.Black, LegendText="00:00:00" } },
                {OrderStatusEnum.Cancelled.ToString(),
                    new View.BrushesPair() {Name="Блюдо/заказ ОТМЕНЕНО", Background=Brushes.Salmon, Foreground = Brushes.Black} },
                {OrderStatusEnum.Took.ToString(),
                    new View.BrushesPair() {Name="~Блюдо/заказ ВЫДАНО", Background=Brushes.Blue, Foreground = Brushes.White } },
                {OrderStatusEnum.Commit.ToString(),
                    new View.BrushesPair() {Name="~Блюдо/заказ ЗАФИКСИРОВАНО", Background=Brushes.DarkBlue, Foreground = Brushes.Yellow } },
                {OrderStatusEnum.CancelConfirmed.ToString(),
                    new View.BrushesPair() {Name="~Отмена ПОДТВЕРЖДЕНА", Background=Brushes.DarkBlue, Foreground = Brushes.Yellow } },
                {"orderHeaderTimer",
                    new View.BrushesPair() {Name="~Таймер в заголовке заказа", Background=Brushes.YellowGreen, Foreground = Brushes.Black} },
                {"dishLineBase",
                    new View.BrushesPair() {Name="~Строка блюда", Background=Brushes.White, Foreground = Brushes.Black} },
                {"ingrLineBase",
                    new View.BrushesPair() {Name="~Строка ингредиента", Background=Brushes.White, Foreground = Brushes.DarkViolet } }
            };
            // дополнительные цвета для некоторых состояний
            BrushesPair waitBrushes = appBrushes[OrderStatusEnum.WaitingCook.ToString()];
            waitBrushes.CreateEmptySubDict();
            // время до авт.начала приготовления
            waitBrushes.SubDictionary.Add("estimateStart",
                new BrushesPair() { Name = "ОЖИДАНИЕ начала готовки: указано время автоматического начала приготовления", Background = Brushes.Plum, Foreground = Brushes.Red, LegendText = "00:00:00" });
            // время готовки блюда
            waitBrushes.SubDictionary.Add("estimateCook", 
                new BrushesPair() { Name = "ОЖИДАНИЕ начала готовки: указано время приготовления блюда", Background = Brushes.Plum, Foreground = Brushes.Navy, LegendText = "00:00:00" });

            return appBrushes;
        }

    }  // class BrushHelper


    public class BrushesPair
    {
        public string Name { get; set; }

        public string LegendText { get; set; }  // текст, отображаемый в легенде

        // основная пара
        public Brush Background { get; set; }

        public Brush Foreground { get; set; }

        // словарь неосновных пар
        private Dictionary<string, BrushesPair> _subDict = null;
        public Dictionary<string, BrushesPair> SubDictionary { get { return _subDict; } }

        public void CreateEmptySubDict()
        {
            _subDict = new Dictionary<string, BrushesPair>();
        }

    }  // class BrushesPair


}
