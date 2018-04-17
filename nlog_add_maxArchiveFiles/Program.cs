using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;

namespace nlog_add_maxArchiveFiles
{
    class Program
    {
        private static ConsoleColor _foreColorDefault;

        static void Main(string[] args)
        {
            string appName = AppDomain.CurrentDomain.FriendlyName;
            _foreColorDefault = Console.ForegroundColor;

            // описание
            Console.WriteLine("Утилита, добавляющая атрибут maxArchiveFiles=\"n\" в элемент <target> конфигурационного файла, в котором есть настройки логгера NLog.\nДанный атрибут устанавливает максимальное количество архивных файлов.\nДля почасовых архивов за сутки наберется 24 файла.\nПо умолчанию, утилита устанавливает maxArchiveFiles=\"120\", т.е. на 5 суток.\nДля установки другого значения, запустите утилиту с параметром -set n.\n\nПримеры:\n\t"
                + appName + "\t- запуск по умолчанию (n = 120)\n\t" + appName + " -set 50\t- максимальное количество архивных файлов равно 50");
            Console.WriteLine("--------------------------------------------------------------------------------");

            string strDir = AppDomain.CurrentDomain.BaseDirectory;
            Console.Write("Текущая папка: "); writeColorText(ConsoleColor.Cyan, strDir);

            // параметры запуска
            string maxArchiveFiles = "120";
            if (args != null)
            {
                bool b1 = false;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].Equals("-set", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i < (args.Length - 1)) { i++; maxArchiveFiles = args[i]; b1 = true; }
                    }
                }

                // если именованного аргумента нет и первый аргумент - это число, то берем это число, как maxArchiveFiles
                if (b1 == false)
                {
                    int arg1;
                    if (int.TryParse(args[0], out arg1) == true) maxArchiveFiles = args[0];
                }
            }
            Console.Write("\nУстановить maxArchiveFiles=\"{0}\"", maxArchiveFiles);

            Console.Write("\nпоиск config-файлов...\n");

            // get *.config files
            DirectoryInfo curDir = new DirectoryInfo(strDir);
            FileInfo[] filesInfo = curDir.GetFiles("*.config");
            if (filesInfo.Length == 0)
            {
                writeColorText(ConsoleColor.Red, "\n\nВ текущей папке config-файлы НЕ НАЙДЕНЫ!", strDir);
                Console.Write("\n\n\nPress any key..."); Console.ReadKey();
                Environment.Exit(1);
            }

            foreach (FileInfo fInfo in filesInfo)
            {
                writeColorText(ConsoleColor.Yellow, "\n\n{0} : ", fInfo.Name);
                XDocument xDoc = XDocument.Load(fInfo.FullName);

                XElement xEl = xDoc.Descendants().FirstOrDefault(xe => xe.Name.LocalName.Equals("nlog", StringComparison.OrdinalIgnoreCase));
                if (xEl == null)
                    Console.Write("элемент nlog НЕ найден");
                else
                {
                    Console.Write("элемент nlog НАЙДЕН");
                    // get target elements
                    IEnumerable<XElement> nlogElements = xEl.Descendants();
                    foreach (XElement target in nlogElements)
                    {
                        if (target.Name.LocalName.Equals("target", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.Write("\n - target name=\"{0}\"", target.Attribute("name").Value);
                            // check archives enable
                            XAttribute xa = target.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("archiveEvery", StringComparison.OrdinalIgnoreCase));
                            if (xa == null)
                                Console.Write(" - архивные файлы НЕ создаются");
                            else
                            {
                                Console.Write(" - архивные файлы СОЗДАЮТСЯ");
                                setMaxArchiveFiles(target, maxArchiveFiles);
                                // сохранить измененный config-файл
                                try
                                {
                                    xDoc.Save(fInfo.FullName);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.ToString());
                                }
                            }
                        }
                    }
                }
            }

            Console.Write("\n\n\nPress any key..."); Console.ReadKey();
        }

        private static void setMaxArchiveFiles(XElement target, string maxArchiveFiles)
        {
            // проверить наличие атрибута maxArchiveFiles 
            XAttribute xa = target.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("maxArchiveFiles", StringComparison.OrdinalIgnoreCase));
            
            // добавить
            if (xa == null)
            {
                target.Add(new XAttribute("maxArchiveFiles", maxArchiveFiles));
                writeColorText(ConsoleColor.Green, "\n   атрибут ДОБАВЛЕН: maxArchiveFiles=\"{0}\"", maxArchiveFiles);
            }
            else
            {
                if (xa.Value == maxArchiveFiles)
                {
                    writeColorText(ConsoleColor.Green, "\n   атрибут НЕ ИЗМЕНЕН: maxArchiveFiles=\"{0}\"", maxArchiveFiles);
                }
                else
                {
                    xa.Value = maxArchiveFiles;
                    writeColorText(ConsoleColor.Green, "\n   атрибут ИЗМЕНЕН: maxArchiveFiles=\"{0}\"", maxArchiveFiles);
                }
            }
        }

        private static void writeColorText(ConsoleColor textColor, string text)
        {
            Console.ForegroundColor = textColor;
            Console.Write(text);
            Console.ForegroundColor = _foreColorDefault;
        }
        private static void writeColorText(ConsoleColor textColor, string format, params object[] args)
        {
            Console.ForegroundColor = textColor;
            Console.Write(format, args);
            Console.ForegroundColor = _foreColorDefault;
        }


    }  // class
}
