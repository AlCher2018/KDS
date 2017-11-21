using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.Lib
{
    public static class CalcObjectSizeHelper
    {
        /// <summary>
        /// Calculates the lenght in bytes of an object 
        /// and returns the size 
        /// </summary>
        /// <param name="TestObject"></param>
        /// <returns></returns>
        private static long GetObjectSize(object TestObject)
        {
            long retVal = 0;
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                // сериализация в двоичный форматтер
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                bf.Serialize(ms, TestObject);
                retVal = ms.Length;

                // сериализация в SOAP formatter
                // Модуль сериализации SOAP не поддерживает сериализацию стандартных типов: System.Collections.Generic.List`1[KDSService.AppModel.OrderModel].
                //System.Runtime.Serialization.Formatters.Soap.SoapFormatter sf = new System.Runtime.Serialization.Formatters.Soap.SoapFormatter();
                //sf.Serialize(ms, TestObject);
                //retVal = sf.ToString().Length;

                // сериализация в XML formatter
                // Ошибка при отражении типа OrderModel[]
                //System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(OrderModel[]));
                //OrderModel[] oArr = ((List<OrderModel>)TestObject).ToArray();
                //xs.Serialize(ms, oArr);
                //retVal = xs.ToString().Length;

                // JSON serialization
                //System.Runtime.Serialization.Json.DataContractJsonSerializer js = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(List<OrderModel>));
                //js.WriteObject(ms, TestObject);
                //string sBuf = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                //retVal = sBuf.Length;
            }
            return retVal;
        }

    }
}
