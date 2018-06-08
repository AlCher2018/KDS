using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace waitCopy
{
    /// <summary>
    /// Утилита, которая циклически пытается скопировать файл с задаваемым ожиданием между попытками копирования
    /// из одной папки (источник) в другую (назначение). Файл в папке назначения может быть занят каким-либо
    /// системным процессом. После освобождения файла в папке-назначении, производится копирование файла
    /// и, если указано, его запуск.
    /// Все параметры задаются аргументами командной строки:
    /// -sp(source path) - полное имя ПАПКИ-источника
    /// -f(files) - имена исходных файлов (из папки-источника), может быть несколько файлов, разделенных ;
    /// -dp(destination path) - полное имя ПАПКИ-назначения
    /// -t(delay) - задержка, в миллисекундах
    /// -c(count) - количество повторов копирования
    /// -l(log) - полное имя журнала (текстовый файл), в который записывается процесс копирования
    /// -d(debug) - режим отладки
    /// -r(run) - признак запуска приложения после удачного копирования, т.е. есть аргумент - запускаем destination, нет - не запускаем.Можно указать какой именно файл необходимо запускать, если files представлены списком.
    /// </summary>

    // тестовые аргументы:
    // -sp "D:\srcDir" -f "AppSettings.config;ClientOrderQueue.exe;ClientOrderQueue.pdb;Audio\1.wav;Images\bg 3hor 1920x1080 splash.png" -dp "D:\destPath" -l "D:\журнал приложения.txt" -d 

    class Program
    {
        private const int countDefault = 20;
        private const int delayDefault = 500;

        private static string _log;
        private static bool _isRun;

        static void Main(string[] args)
        {
            if (args.Length == 0) { showHelp(); Environment.Exit(1); }

            AppArgs.PutArgs(args);
            if (AppArgs.IsExists("?") || AppArgs.IsExists("h") || AppArgs.IsExists("help"))
            {
                showHelp(); Environment.Exit(1);
            }

            // лог
            _log = AppArgs.GetValue("l");
            if (_log != null)
            {
                if (!File.Exists(_log))
                {
                    try { FileStream stream = File.Create(_log); stream.Close(); stream.Dispose(); }
                    catch (Exception) { }
                }
            }
            #region отладочный вывод аргументов
            if (AppArgs.IsExists("d"))
            {
                string sOut = "cmd line: " + Environment.CommandLine;
                Console.WriteLine(sOut); writeLog(sOut);
                sOut = "args:\n-------";
                Console.WriteLine(sOut); writeLog(sOut);
                foreach (string item in args)
                {
                    sOut = "\t" + item;
                    Console.WriteLine(sOut); writeLog(sOut);
                }
            }
            #endregion

            string srcPath = AppArgs.GetValue("sp");
            string srcFilesString = AppArgs.GetValue("f");
            string dstPath = AppArgs.GetValue("dp");
            if (string.IsNullOrEmpty(srcPath) || string.IsNullOrEmpty(srcFilesString) || string.IsNullOrEmpty(dstPath))
            {
                Console.WriteLine("Неверные агрументы. Используйте /? для помощи.");
                Environment.Exit(2);
            }
            if (!srcPath.EndsWith("\\")) srcPath += "\\";
            if (!dstPath.EndsWith("\\")) dstPath += "\\";

            string arg = AppArgs.GetValue("c");
            int count = string.IsNullOrEmpty(arg) ? countDefault : Convert.ToInt32(arg);

            arg = AppArgs.GetValue("t");
            int delay = string.IsNullOrEmpty(arg) ? delayDefault : Convert.ToInt32(arg);

            _isRun = AppArgs.IsExists("r");

#region check source files and folders
            if (!Directory.Exists(srcPath))
            {
                writeLog($"NOT exists source path: " + srcPath);
                Console.WriteLine($"Папка-источник '{srcPath}' не существует.");
                Environment.Exit(2);
            }
            List<SourceFileItem> srcFiles = srcFilesString.Split(';').Select(f => new SourceFileItem(f)).ToList();
            List<SourceFileItem> delItems = new List<SourceFileItem>();
            foreach (SourceFileItem item in srcFiles)
            {
                if (!File.Exists(srcPath + item.Name))
                {
                    writeLog($"NOT exists source file: " + srcPath + item.Name);
                    delItems.Add(item);
                }
            }
            delItems.ForEach(item => srcFiles.Remove(item));
            if (srcFiles.Count == 0)
            {
                writeLog($"source file(s) NOT FOUND");
                Console.WriteLine($"Исходный файл(ы) не найдены.");
                Environment.Exit(2);
            }
            if (!Directory.Exists(dstPath))
            {
                // попытка создать папку
                try
                {
                    Directory.CreateDirectory(dstPath);
                }
                catch (Exception)
                {
                    writeLog($"NOT exists destination path: " + dstPath);
                    Console.WriteLine($"Папка-назначение '{dstPath}' не существует.");
                    Environment.Exit(2);
                }
            }
#endregion

            // цикл копирования
            bool allCopied = false, isContinue = true;
            for (int i = 1; (i <= count) && isContinue; i++)
            {
                writeLog("copy attempt " + i.ToString());
                foreach (SourceFileItem fileItem in srcFiles.Where(f => !f.Copied))
                {
                    writeLog("- copy file '" + fileItem.Name + "'...");
                    try
                    {
                        FileInfo fi = new FileInfo(srcPath + fileItem.Name);
                        FileInfo fiNew = new FileInfo(dstPath + fileItem.Name);
                        // если папка назначения не существует, то создать ее
                        if (fiNew.Directory.Exists == false)
                        {
                            try
                            {
                                writeLog("- create dest directory: " + fiNew.DirectoryName);
                                fiNew.Directory.Create();
                                writeLog("- create SUCCESS!");
                            }
                            catch (Exception ex)
                            {
                                writeLog("- create dir error: " + ex.Message);
                                isContinue = false;
                            }
                        }
                        fiNew = fi.CopyTo(dstPath + fileItem.Name, true);
                        if (fiNew != null)
                        {
                            writeLog("- copy SUCCESS!");
                            fileItem.Copied = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        writeLog("- copy error: " + ex.Message);
                        Thread.Sleep(delay);
                        break;
                    }
                }

                if ((allCopied = srcFiles.All(f => f.Copied))) break;
            }

            if (allCopied)
            {
                // запуск файла
                if (_isRun)
                {
                    string runFile = AppArgs.GetValue("r");
                    if (runFile == null)
                    {
                        writeLog("get start file from source files (first .exe)...");
                        SourceFileItem sfi = srcFiles.FirstOrDefault(f => f.Name.EndsWith(".exe"));
                        if (sfi != null)
                        {
                            runFile = sfi.Name;
                            writeLog("- found: " + runFile);
                        }
                        else
                        {
                            writeLog("start file NOT FOUND");
                        }
                    }
                    else
                    {
                        writeLog("start file from cmd line argument: '" + runFile + "'");
                    }

                    if (runFile != null) 
                    {
                        runFile = dstPath + runFile;
                        if (File.Exists(runFile))
                        {
                            try
                            {
                                writeLog("start file '" + runFile + "'...");
                                Process.Start(new ProcessStartInfo() { FileName = runFile });
                                writeLog("start SUCCESS");
                            }
                            catch (Exception ex)
                            {
                                writeLog("start error: " + ex.Message);
                            }
                        }
                        else
                        {
                            writeLog("NOT exists start file: '" + runFile + "'");
                        }
                    }
                }
            }
            else
            {
                writeLog("copy has NOT PERFORMED");
                Environment.ExitCode = 5;
            }
        }

        private static void writeLog(string message)
        {
            if (_log == null) return;

            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(_log, true, Encoding.Default);
                sw.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "|waitCopy| " + message);
                sw.Close();
            }
            catch (Exception ex)
            {
                StreamWriter error = new StreamWriter("waitCopy.errors.log", true);
                error.WriteLine(ex.Message);
                error.Close();
            }
        }


        private static void showHelp()
        {
            Console.WriteLine("Копирование файла с повтором при неудачном копировании.");
            Console.WriteLine("©Integra-ITS, www.integra-its.com.ua, 2018");
            Console.WriteLine("");
            Console.WriteLine("waitCopy -sp source_path -f source_files -dp destination_path [-t msec] [-c count] [-l log] [-d] [-r]");
            Console.WriteLine("");
            Console.WriteLine("   source_path - полный путь к папке с исходными файлами (папка-источник)");
            Console.WriteLine("   source_files - имена исходных файлов, может быть несколько, разделенных ; (точка с запятой)");
            Console.WriteLine("   destination_path - полное путь к папке, куда будут копироваться файлы (папка-назначение)");
            Console.WriteLine("   msec - задержка, в миллисекундах");
            Console.WriteLine("   count - количество повторов копирования");
            Console.WriteLine("   log - полное имя журнала (текстовый файл), в который записывается процесс копирования");
            Console.WriteLine("   -d - признак отладки: вывод в лог аргументов командной строки");
            Console.WriteLine("   -r - признак запуска файла после удачного копирования, если файл запуска не задан, то ищется первый exe-файл из числа успешно скопированных в папку-назначение");
            Console.WriteLine("   нет аргументов, -h, -help, /?, /h - эта помощь");
            Console.WriteLine("");
            Console.WriteLine("Значения по умолчанию для необязательных аргументов:");
            Console.WriteLine($"   msec={delayDefault}; count={countDefault}; log=null; -r=not exists");
            Console.WriteLine("Внимание! Если параметр source_path (destination_path) заключен в двойных кавычках, то в конце строки обратный слэш НЕ задавать!!!");
            Console.WriteLine("");
            Console.Write("Press any key..."); Console.ReadKey();
        }

        private class SourceFileItem
        {
            public string  Name { get; set; }
            public bool Copied { get; set; }
            public SourceFileItem(string fileName)
            {
                this.Name = fileName;
                this.Copied = false;
            }
        }

    }  // class Program
}
