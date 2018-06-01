using IntegraLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ClientOrderQueue.Lib
{
    /// <summary>
    /// Класс автообновления приложения.
    /// В конструктор передаюься: путь к фолдеру настроек, адрес хранилища, логин и пароль к хранилищу
    /// </summary>
    public class AppAutoUpdater
    {
        #region fields
        private bool _enable;
        public bool Enable { get { return _enable; } }

        private string _registryPath;
        public string RegistryPath { get { return _registryPath; } }

        private string _storagePath;
        public string StoragePath { get { return _storagePath; } }

        private string _storageLogin;
        public string StorageLogin { get { return _storageLogin; } }

        private string _storagePWD;
        public string StoragePWD { get { return _storagePWD; } }

        private string _errorMsg;
        public string LastError { get { return _errorMsg; } }

        private string _updatePath;
        public string UpdatePath { get { return _updatePath; } }

        // причины обновления
        private List<AppUpdateReason> _updateReasons;
        public List<AppUpdateReason> UpdateReasons { get { return _updateReasons; } }

        // действия по обновлению
        private List<AppUpdateItem> _updateItems;
        public List<AppUpdateItem> UpdateItems { get { return _updateItems; } }

        private FTPFolder _updFTPFolder;
        private string _appFolder;

        #endregion


        public AppAutoUpdater(string registryPath, string storagePath, string storageLogin, string storagePWD)
        {
            _registryPath = registryPath;
            _storagePath = storagePath; _storageLogin = storageLogin; _storagePWD = storagePWD;
            _updateItems = new List<AppUpdateItem>();
            _updateReasons = new List<AppUpdateReason>();

            _appFolder = AppEnvironment.GetAppDirectory();

            _updatePath = AppEnvironment.GetAppDirectory() + "updFiles";
            checkUpdPath();

            checkRegSettings();
        }

        private bool checkUpdPath()
        {
            bool retVal = false;
            try
            {
                if (System.IO.Directory.Exists(_updatePath) == false)
                {
                    System.IO.Directory.CreateDirectory(_updatePath);
                }
                if (_updatePath.EndsWith("\\") == false) _updatePath += "\\";
                retVal = true;
            }
            catch (Exception ex)
            {
                _errorMsg = ex.Message;
            }
            return retVal;
        }

        // очистить папку обновляемых файлов
        private void clearUpdPath()
        {
            if (checkUpdPath())
            {
                try
                {
                    string[] dirs = System.IO.Directory.GetDirectories(_updatePath);
                    foreach (string dirName in dirs)
                    {
                        System.IO.Directory.Delete(dirName, true);
                    }

                    string[] files = System.IO.Directory.GetFiles(_updatePath);
                    foreach (string filName in files)
                    {
                        System.IO.File.Delete(filName);
                    }
                }
                catch (Exception)
                {
                }
            }
        }


        private void checkRegSettings()
        {
            bool regResult;
            string regValue = RegistryHelper.GetCompanyValue(_registryPath, "Enable");
            if (regValue == null)  // add & enable
            {
                regResult = RegistryHelper.SetCompanyValue(_registryPath, "Enable", "1");
                if (regResult == false) { _enable = false; return; }
                
                // "ftp://integra-its\ftp@82.207.112.88/IT Department/!Soft_dev/KDS/ClientQueue/.." 
                RegistryHelper.SetCompanyValue(_registryPath, "Source", _storagePath);
                RegistryHelper.SetCompanyValue(_registryPath, "FTPLogin", _storageLogin);
                RegistryHelper.SetCompanyValue(_registryPath, "FTPPassword", _storagePWD);
                _enable = true;
            }
            else
            {
                _enable = regValue.ToBool();
            }
        }

        // проверка необходимости обновления: сравнение версий файлов (exe | dll) 
        public bool IsNeedUpdate()
        {
            if (_errorMsg != null) _errorMsg = null;
            _updateReasons.Clear();

            // получить папки версий
            FTPHelper ftpHelper = getFTPHelper();
            FTPFolder ftpFolder = ftpHelper.GetFTPFolder(_storagePath);
            if ((ftpFolder != null) && (ftpFolder.Items.Count > 0))
            {
                // папка с самой свежей версией (сравнение по дате)
                FTPItem newestFolder = null; DateTime dtMax = DateTime.MinValue;
                ftpFolder.Items.ForEach(f => 
                {
                    if (f.DateTime > dtMax) { newestFolder = f; dtMax = f.DateTime; }
                });

                // получить рекурсивно имена ВСЕХ файлов из папки последней версии
                if (_storagePath.RightString(1) != "/") _storagePath += "/";
                _updFTPFolder = ftpHelper.GetFTPFolder(_storagePath + newestFolder.Name + "/", true);

                // получить файлы exe/dll
                clearUpdPath(); // очистить папку файлов, загружаемых с ФТП
                string[] checkedExt = new string[] { "exe", "dll" };
                FileInfo[] appFiles = (new DirectoryInfo(_appFolder)).GetFiles().Where(f => checkedExt.Contains(f.Extension.Remove(0,1))).ToArray();
                FTPFile[] ftpFiles = _updFTPFolder.Items.Where(f => f.ItemType == FSItemTypeEnum.File).Select(f => (FTPFile)f).Where(f => checkedExt.Contains(f.Ext)).ToArray();
                foreach (FTPFile item in ftpFiles)
                {
                    FileInfo appFileInfo = appFiles.FirstOrDefault(f => f.Name==item.Name);
                    // файл отсутствует
                    if (appFileInfo == null)
                    {
                        _updateReasons.Add(new AppUpdateReason()
                        {
                            FileName = item.Name, FileAttribute = "file",
                            ValueExist = "not exist", ValueNew = "exist"
                        });
                    }

                    // сравнить размер, дату, версию файла
                    else
                    {
                        bool needUpd = false;
                        // дата создания
                        if (appFileInfo.CreationTime != item.DateTime)
                        {
                            _updateReasons.Add(new AppUpdateReason()
                            {
                                FileName = item.Name,
                                FileAttribute = "creationTime",
                                ValueExist = appFileInfo.CreationTime.ToString(),
                                ValueNew = item.DateTime.ToString()
                            });
                            needUpd = true;
                        }

                        // размер файла
                        if (appFileInfo.Length != item.Size)
                        {
                            _updateReasons.Add(new AppUpdateReason()
                            {
                                FileName = item.Name,
                                FileAttribute = "size",
                                ValueExist = appFileInfo.Length.ToString(),
                                ValueNew = item.Size.ToString()
                            });
                            needUpd = true;
                        }

                        // при необходимости, проверяем версию, загружая файл с ФТП
                        if (!needUpd)
                        {
                            string newUpdFileFullName = _updatePath + item.Name;
                            if (ftpHelper.DownloadFile(item.FullName, newUpdFileFullName))
                            {
                                string appFileVersion = AppEnvironment.GetFileVersion(appFileInfo.FullName);
                                string updFileVersion = AppEnvironment.GetFileVersion(newUpdFileFullName);
                                if (appFileVersion != updFileVersion)
                                {
                                    _updateReasons.Add(new AppUpdateReason()
                                    {
                                        FileName = item.Name,
                                        FileAttribute = "version",
                                        ValueExist = appFileVersion,
                                        ValueNew = updFileVersion
                                    });
                                }
                            }
                        }
                    }
                }  // for
                clearUpdPath();
            }
            else
            {
                _errorMsg = "Ошибка получения папки хранилища версий '" + _storagePath + "' : " + ftpHelper.LastErrorMessage; 
            }

            // создать действия обновления приложения
            bool retVal = ((_updateReasons.Count > 0) && (_errorMsg == null));
            if (retVal)
            {
                _errorMsg = null;
                // TODO debug here
                createUpdateActions();
                retVal = (_errorMsg == null);
            }

            return retVal;
        }

        private FTPHelper getFTPHelper()
        {
            return new FTPHelper()
            {
                Login = _storageLogin,
                PWD = _storagePWD,
                IsAlive = false,
                IsPassive = true
            };
        }

        // заполнить _updItems
        private void createUpdateActions()
        {
            _updateItems.Clear();
            AppUpdateItem appItem;
            string appDirectory = AppEnvironment.GetAppDirectory();
            foreach (FTPItem item in _updFTPFolder.Items)
            {
                // файлы в корне целевой папки могут быть добавлены, заменены или модифицированы (для *.config)
                // config-файлы по умолчанию не заменяются!!
                if (item.ItemType == FSItemTypeEnum.File)
                {
                    appItem = getUpdateItemForFile(item, appDirectory, true);
                    if (appItem == null)
                    {
                        if (_errorMsg != null) { _updateItems.Clear(); break; }
                    }
                    else
                    {
                        _updateItems.Add(appItem);
                    }
                }
                // папки могут только добавляться, файлы в них могут быть только добавлены или заменены
                // структура фолдеров начинается от _storagePath (FTP) и _updatePath (целевая папка)
                // для FTP есть _updFTPFolder, который содержит структуру папки-источника обновления
                else if (item.ItemType == FSItemTypeEnum.Folder)
                {
                    compareFolders(item, appDirectory);
                }
            }
        }

        private AppUpdateItem getUpdateItemForFile(FTPItem ftpItem, string destDirectory, bool checkConfig = false)
        {
            AppUpdateItem retVal = null;

            FTPFile ftpFile = (FTPFile)ftpItem;
            DirectoryInfo dirInfo = new DirectoryInfo(destDirectory);
            FileInfo fInfo = dirInfo.GetFiles().FirstOrDefault(f => f.Name == ftpFile.Name);
            if (fInfo == null) // новый файл
            {
                retVal = new AppUpdateItem()
                {
                    ItemType = FSItemTypeEnum.File,
                    Name = ftpFile.Name,
                    ActionType = AppUpdateActionTypeEnum.AddNew
                };
            }
            else if(isNeedFileUpdate(ftpFile, fInfo))
            {
                if (((FTPFile)ftpItem).IsConfigFile)
                {
                    if (checkConfig) retVal = getConfigModifyItems(ftpItem, fInfo);
                }
                else 
                {
                    retVal = new AppUpdateItem()
                    {
                        ItemType = FSItemTypeEnum.File,
                        Name = fInfo.Name,
                        ActionType = AppUpdateActionTypeEnum.Replace
                    };
                }

            }

            return retVal;
        }

        private void compareFolders(FTPItem ftpItem, string destPath)
        {
            // проверка наличия целевой папки в destPath
            string destFolder = destPath + ftpItem.Name + "\\";
            if (Directory.Exists(destFolder))
            {
                FTPFolder ftpFolder = (FTPFolder)ftpItem;
                DirectoryInfo destDirInfo = new DirectoryInfo(destFolder);
                string[] destFileNames = destDirInfo.GetFiles().Select(fInfo => fInfo.Name).ToArray();
                foreach (FTPItem item in ftpFolder.Items)
                {
                    if (item.ItemType == FSItemTypeEnum.File)
                    {
                        FTPFile ftpFile = (FTPFile)item;
                        string destFileFullName = destFolder + ftpFile.Name;

                        // добавить файл
                        if (!destFileNames.Contains(ftpFile.Name))
                        {
                            AppUpdateItem updItem = new AppUpdateItem()
                            {
                                ItemType = FSItemTypeEnum.File,
                                Name = destFileFullName,
                                ActionType = AppUpdateActionTypeEnum.AddNew
                            };
                            _updateItems.Add(updItem);
                        }
                        // иначе - обновить
                        else if ((!ftpFile.IsConfigFile) && (isNeedFileUpdate(ftpFile, destFileFullName)))
                        {
                            AppUpdateItem updItem = new AppUpdateItem()
                            {
                                ItemType = FSItemTypeEnum.File,
                                Name = destFileFullName,
                                ActionType = AppUpdateActionTypeEnum.Replace
                            };
                            _updateItems.Add(updItem);
                        }
                    }

                    // subfolder
                    else if (item.ItemType == FSItemTypeEnum.Folder)
                    {
                        compareFolders(item, destFolder);
                    }
                }
            }

            // папки нет - записать создание (при выполнении действия, создавать структуру папок рекурсивно)
            else
            {
                AppUpdateItem updItem = new AppUpdateItem()
                {
                    ItemType = FSItemTypeEnum.Folder,
                    Name = ftpItem.Name,
                    ActionType = AppUpdateActionTypeEnum.AddNew
                };
                _updateItems.Add(updItem);
            }
        }

        // проверка необходимости обновить файл
        private bool isNeedFileUpdate(FTPFile ftpFile, string destFileFullName)
        {
            bool retVal = false;
            try
            {
                FileInfo fInfo = new FileInfo(destFileFullName);
                retVal = isNeedFileUpdate(ftpFile, fInfo);
            }
            catch (Exception)
            {
            }
            return retVal;
        }

        private bool isNeedFileUpdate(FTPFile ftpFile, FileInfo fInfo)
        {
            bool retVal = (ftpFile.Size != fInfo.Length) || (ftpFile.DateTime != fInfo.CreationTime);

            // при одинаковом размере и дате создания, для exe/dll дополнительно проверить версию файла
            if ((retVal == false) && ((fInfo.Extension == "exe") || (fInfo.Extension == "dll")))
            {
                FTPHelper ftpHelper = getFTPHelper();
                string newUpdFileFullName = _updatePath + ftpFile.Name;
                if (ftpHelper.DownloadFile(ftpFile.FullName, newUpdFileFullName))
                {
                    // версия
                    string appFileVersion = AppEnvironment.GetFileVersion(fInfo.FullName);
                    string updFileVersion = AppEnvironment.GetFileVersion(newUpdFileFullName);
                    retVal = (appFileVersion != updFileVersion);
                }
            }

            return retVal;
        }

        private AppUpdateItem getConfigModifyItems(FTPItem ftpItem, FileInfo fInfo)
        {
            // скачать файл с ФТП
            string ftpDestFile = _updatePath + fInfo.Name;
            FTPHelper ftpHelper = getFTPHelper();
            if (ftpHelper.DownloadFile(ftpItem.FullName, ftpDestFile) == false)
            {
                _errorMsg = $"Ошибка загрузки файла \"{ftpItem.Name}\" : {ftpHelper.LastErrorMessage}";
                return null;
            }

            // получить XML-представления
            XDocument xDocSrc = null, xDocDst = null;
            try
            {
                xDocSrc = XDocument.Load(ftpDestFile);
                xDocDst = XDocument.Load(fInfo.FullName);
            }
            catch (Exception ex)
            {
                _errorMsg = ex.Message;
            }
            if ((xDocSrc == null) || (xDocDst == null)) return null;

            AppUpdateCfgFile retVal = null;
            XMLComparer xComparer = new XMLComparer(xDocSrc, xDocDst);
            if (xComparer.Compare())
            {
                List<XMLCompareChangeItem> result = xComparer.Changes;
                if (result.Count == 0)
                {
                    //Console.WriteLine("изменений нет");
                }
                else
                {
                    retVal = new AppUpdateCfgFile(fInfo.FullName);
                    retVal.ActionType = AppUpdateActionTypeEnum.Replace;
                    AppUpdateCfgFileItem curCfgFileItem;
                    foreach (XMLCompareChangeItem item in result)
                    {
                        //Console.WriteLine(item.ToString());
                        AppUpdateActionTypeEnum action = convertCfgAction(item.Result);
                        // только добавление или удаление элементов/атрибутов
                        if ((action == AppUpdateActionTypeEnum.AddNew) || (action == AppUpdateActionTypeEnum.Delete))
                        {
                            curCfgFileItem = new AppUpdateCfgFileItem();
                            curCfgFileItem.Action = action;
                            curCfgFileItem.XElementNames = item.Names.ToList();
                            curCfgFileItem.XAttributeName = item.AttrName;

                            retVal.UpdateItems.Add(curCfgFileItem);
                        }
                    }
                    if (retVal.UpdateItems.Count == 0) retVal = null;
                }
            }
            else
            {
                _errorMsg = xComparer.ErrorMessage;
            }

            return retVal;
        }

        // преобразование перечисления XMLCompareResultEnum в AppUpdateActionTypeEnum
        private AppUpdateActionTypeEnum convertCfgAction(XMLCompareResultEnum xmlComparerResult)
        {
            AppUpdateActionTypeEnum retVal = AppUpdateActionTypeEnum.None;
            switch (xmlComparerResult)
            {
                case XMLCompareResultEnum.AddNew:
                    retVal = AppUpdateActionTypeEnum.AddNew;
                    break;
                case XMLCompareResultEnum.Remove:
                    retVal = AppUpdateActionTypeEnum.Delete;
                    break;
                case XMLCompareResultEnum.ChangeValue:
                    retVal = AppUpdateActionTypeEnum.Modify;
                    break;
                default:
                    break;
            }

            return retVal;
        }

        public bool DoUpdate()
        {
            bool retVal = false;

            return retVal;
        }

        public string UpdateReasonString()
        {
            StringBuilder bld = new StringBuilder();
            if (_updateReasons.Count > 0)
            {
                _updateReasons.ForEach(r =>
                {
                    if (bld.Length > 0) bld.Append(Environment.NewLine);
                    bld.Append(r.ToString());
                });
            }

            return bld.ToString();
        }

    }  // class

    #region other classes

    public class AppUpdateItem
    {
        public string Name { get; set; }
        public FSItemTypeEnum ItemType { get; set; }
        public AppUpdateActionTypeEnum ActionType { get; set; }
    }

    // xml-файл, в котором ТОЛЬКО _добавляются_ или _удаляются_ элементы или атрибуты
    public class AppUpdateCfgFile: AppUpdateItem
    {
        private List<AppUpdateCfgFileItem> _items;      
        public List<AppUpdateCfgFileItem> UpdateItems
        {
            get { return _items; }
        }


        public AppUpdateCfgFile(string fileName)
        {
            this.ItemType = FSItemTypeEnum.File;
            this.Name = fileName;
            _items = new List<AppUpdateCfgFileItem>();
        }
    }

    public class AppUpdateCfgFileItem
    {
        public List<string> XElementNames { get; set; }
        public string XAttributeName { get; set; }
        public string Value { get; set; }
        public AppUpdateActionTypeEnum Action { get; set; }
    }

    public enum AppUpdateActionTypeEnum
    {
        None, AddNew, Delete, Modify, Replace
    }

    public class AppUpdateReason
    {
        public string FileName { get; set; }
        public string FileAttribute { get; set; }
        public string ValueExist { get; set; }
        public string ValueNew { get; set; }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(this.FileName)
                || string.IsNullOrEmpty(this.FileAttribute)
                || string.IsNullOrEmpty(this.ValueExist)
                || string.IsNullOrEmpty(this.ValueNew))
                return base.ToString();
            else
                return $"file=\"{this.FileName}\", attribute=\"{this.FileAttribute}\", value=\"{this.ValueExist}\", newValue=\"{this.ValueNew}\"";
        }
    }

    #endregion
}
