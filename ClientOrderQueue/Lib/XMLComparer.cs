using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace ClientOrderQueue.Lib
{

    /// <summary>
    /// Класс для сравнения двух XML-документов
    /// Результат сравнения - набор объектов XMLCompareChangeItem.
    /// Каждый элемент XMLCompareChangeItem содержат описание одного изменения в документе-назначении (_xDocSrc)
    /// по сравнению с документом-источником (_xDocDest)
    /// </summary>
    public class XMLComparer
    {
        // сравниваемые документы
        private XDocument _xDocSrc, _xDocDest;

        private List<XMLCompareChangeItem> _changes;
        public List<XMLCompareChangeItem> Changes { get { return _changes; } }

        private string _errMsg;
        public string ErrorMessage { get { return _errMsg; } }

        public XMLComparer(XDocument xDocSrc, XDocument xDocDest)
        {
            _xDocSrc = xDocSrc;
            _xDocDest = xDocDest;

            _changes = new List<XMLCompareChangeItem>();
        }

        public bool Compare()
        {
            if (_errMsg != null) _errMsg = null;
            _changes.Clear();
            if ((_xDocSrc == null) || (_xDocDest == null)) return false;

            compareXElements(_xDocSrc.Root, _xDocDest.Root);

            return (_errMsg == null);
        }

        private void compareXElements(XElement xESrc, XElement xEDst)
        {
            // проверка атрибутов
            compareAttributes(xESrc, xEDst);

            // и вложенных элементов
            XElement curEl;
            // удаление элементов из Dest
            List<string> delElements = xEDst.Elements().Select(e => e.Name.LocalName).Except(xESrc.Elements().Select(e => e.Name.LocalName)).ToList();
            foreach (string item in delElements)
            {
                XMLCompareChangeItem newItem = new XMLCompareChangeItem();
                newItem.SetNamesFromXElement(xEDst.Element(item));
                newItem.Result = XMLCompareResultEnum.Remove;
                _changes.Add(newItem);
            }

            // цикл по элементам в Source
            // проверяются только уникальные элементы
            foreach (XElement xSrcElement in xESrc.Elements())
            {
                int cntEls = xEDst.Elements(xSrcElement.Name).Count();

                // добавить в Dest новый элемент
                if (cntEls == 0)
                {
                    XMLCompareChangeItem newItem = new XMLCompareChangeItem();
                    newItem.SetNamesFromXElement(xSrcElement);
                    newItem.Result = XMLCompareResultEnum.AddNew;
                    _changes.Add(newItem);
                }

                // проверяются только уникальные элементы
                else if (cntEls == 1)
                {
                    curEl = xEDst.Element(xSrcElement.Name);
                    compareXElements(xSrcElement, curEl);
                }
            }
        }

        // проверка атрибутов
        private void compareAttributes(XElement xESrc, XElement xEDst)
        {
            // - удаление
            List<string> delElements = xEDst.Attributes().Select(e => e.Name.LocalName).Except(xESrc.Attributes().Select(e => e.Name.LocalName)).ToList();
            foreach (string item in delElements)
            {
                XMLCompareChangeItem newItem = new XMLCompareChangeItem();
                newItem.SetNamesFromXElement(xEDst);
                newItem.AttrName = item;
                newItem.Result = XMLCompareResultEnum.Remove;
                _changes.Add(newItem);
            }

            XAttribute curAtr;
            // - цикл по атрибутам в Source
            foreach (XAttribute xAttrSrc in xESrc.Attributes())
            {
                // есть атрибут
                if (xEDst.Attributes(xAttrSrc.Name).Count() > 0)
                {
                    curAtr = xEDst.Attribute(xAttrSrc.Name);
                    if (xAttrSrc.Value != curAtr.Value)
                    {
                        XMLCompareChangeItem newItem = new XMLCompareChangeItem();
                        newItem.SetNamesFromXElement(xESrc);
                        newItem.AttrName = curAtr.Name.LocalName;
                        newItem.Value = xAttrSrc.Value;
                        newItem.Result = XMLCompareResultEnum.ChangeValue;
                        _changes.Add(newItem);
                    }
                }
                // добавить атрибут
                else
                {
                    XMLCompareChangeItem newItem = new XMLCompareChangeItem();
                    newItem.SetNamesFromXElement(xESrc);
                    newItem.AttrName = xAttrSrc.Name.LocalName;
                    newItem.Result = XMLCompareResultEnum.AddNew;
                    _changes.Add(newItem);
                }
            }
        }

    }  // class

    public class XMLCompareChangeItem
    {
        private List<string> _names;

        public List<string> Names { get { return _names; } }

        public string AttrName { get; set; }
        public XMLCompareResultEnum Result { get; set; }
        public string Value { get; set; }

        public XMLCompareChangeItem()
        {
            _names = new List<string>();
        }

        public override string ToString()
        {
            StringBuilder bld = new StringBuilder();

            bld.Append($"element=\"{string.Join("/", _names)}\"");

            if (string.IsNullOrEmpty(this.AttrName) == false) bld.Append($" attr=\"{this.AttrName}\"");

            bld.Append($" action=\"{this.Result.ToString()}\"");

            if (this.Result == XMLCompareResultEnum.ChangeValue) bld.Append($" value=\"{this.Value}\"");

            return bld.ToString();
        }

        internal void SetNamesFromXElement(XElement curEl)
        {
            _names.Clear();

            _names.Add(curEl.Name.LocalName);
            while (curEl.Parent != null)
            {
                curEl = curEl.Parent;
                _names.Insert(0, curEl.Name.LocalName);
            }
        }
    }

    public enum XMLCompareResultEnum
    {
        AddNew, Remove, ChangeValue
    }

}
