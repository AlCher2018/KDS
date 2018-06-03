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

        // причины обновления
        private List<AppUpdateReason> _updateReasons;
        public List<AppUpdateReason> UpdateReasons { get { return _updateReasons; } }

        // действия по обновлению
        private List<AppUpdateItem> _updateItems;
        public List<AppUpdateItem> UpdateItems { get { return _updateItems; } }

        // папка-источник
        private FTPFolder _updFTPFolder;
        public string UpdateFTPFolder { get { return (_updFTPFolder == null ? "" : _updFTPFolder.Name); } }

        // папка-назначение
        private string _destDirFullName;
        public string DestinationPath { get { return _destDirFullName; } }
        // папка временного нахождения скачиваемых файлов
        private string _tmpDir;

        public event EventHandler<string> UpdateActionBefore;
        public event EventHandler<string> UpdateActionAfter;

        #endregion


        public AppAutoUpdater(string registryPath, string storagePath, string storageLogin, string storagePWD, string destDirFullName)
        {
            _registryPath = registryPath;
            _storagePath = storagePath; _storageLogin = storageLogin; _storagePWD = storagePWD;
            _updateItems = new List<AppUpdateItem>();
            _updateReasons = new List<AppUpdateReason>();

            initPathes(destDirFullName);

            checkRegSettings();
        }

        private void initPathes(string destDirFullName)
        {
            _destDirFullName = (destDirFullName == null) ? AppEnvironment.GetAppDirectory() : destDirFullName;
            if (_destDirFullName != null)
            {
                try
                {
                    if (!Directory.Exists(_destDirFullName)) Directory.CreateDirectory(_destDirFullName);
                }
                catch (Exception ex)
                {
                    _errorMsg = ex.Message;
                    _destDirFullName = null;
                    return;
                }
                if (_destDirFullName.EndsWith("\\") == false) _destDirFullName += "\\";

                _tmpDir = _destDirFullName + "tmp\\";
                try
                {
                    if (Directory.Exists(_tmpDir))
                        clearTmpDir();
                    else
                        Directory.CreateDirectory(_tmpDir);
                }
                catch (Exception ex)
                {
                    _errorMsg = ex.Message;
                    _tmpDir = null;
                }

            }
        }

        // очистить папку обновляемых файлов
        private void clearTmpDir()
        {
            try
            {
                string[] dirs = Directory.GetDirectories(_tmpDir);
                foreach (string dirName in dirs)
                {
                    System.IO.Directory.Delete(dirName, true);
                }

                string[] files = System.IO.Directory.GetFiles(_tmpDir);
                foreach (string filName in files)
                {
                    System.IO.File.Delete(filName);
                }
            }
            catch (Exception)
            {
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
                _updFTPFolder.Name = newestFolder.Name;

                // получить файлы exe/dll
                clearTmpDir(); // очистить папку файлов, загружаемых с ФТП
                string[] checkedExt = new string[] { "exe", "dll" };
                FileInfo[] appFiles = (new DirectoryInfo(_destDirFullName)).GetFiles().Where(f => checkedExt.Contains(f.Extension.Remove(0,1))).ToArray();
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
                        if (item.DateTime > appFileInfo.LastWriteTime)
                        {
                            _updateReasons.Add(new AppUpdateReason()
                            {
                                FileName = item.Name,
                                FileAttribute = "lastWriteTime",
                                ValueExist = appFileInfo.LastWriteTime.ToString(),
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
                            string newUpdFileFullName = _destDirFullName + item.Name;
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
                clearTmpDir();
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
                createUpdateActions();
                retVal = (_errorMsg == null);
            }

            return retVal;
        }

        #region FTPHelper
        private FTPHelper getFTPHelper()
        {
            FTPHelper retVal = new FTPHelper()
            {
                Login = _storageLogin,
                PWD = _storagePWD,
                IsAlive = false,
                IsPassive = true
            };
            retVal.DownloadFileBeforeHandler += FtpHelper_DownloadFileBeforeHandler;
            retVal.DownloadFileAfterHandler += FtpHelper_DownloadFileAfterHandler;

            return retVal;
        }
        private void FtpHelper_DownloadFileBeforeHandler(object sender, string e)
        {
            UpdateActionBefore?.Invoke(this, "Downloading file: " + e);
        }
        private void FtpHelper_DownloadFileAfterHandler(object sender, string e)
        {
            UpdateActionAfter?.Invoke(this, "Download file result: " + e);
        }

        #endregion

        // заполнить _updItems
        private void createUpdateActions()
        {
            _updateItems.Clear();
            AppUpdateItem appItem;
            foreach (FTPItem item in _updFTPFolder.Items)
            {
                // файлы в корне целевой папки могут быть добавлены, заменены или модифицированы (для *.config)
                // config-файлы по умолчанию не заменяются!!
                if (item.ItemType == FSItemTypeEnum.File)
                {
                    appItem = getUpdateItemForFile(item, true);
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
                    compareFolders(item, _destDirFullName);
                }
            }
        }

        private AppUpdateItem getUpdateItemForFile(FTPItem ftpItem, bool checkConfig = false)
        {
            AppUpdateItem retVal = null;

            FTPFile ftpFile = (FTPFile)ftpItem;
            DirectoryInfo dirInfo = new DirectoryInfo(_destDirFullName);
            FileInfo fInfo = dirInfo.GetFiles().FirstOrDefault(f => f.Name == ftpFile.Name);
            if (fInfo == null) // новый файл
            {
                retVal = new AppUpdateItem()
                {
                    ItemType = FSItemTypeEnum.File,
                    ActionType = AppUpdateActionTypeEnum.AddNew,
                    FullNameFrom = ftpFile.FullName,
                    FullNameTo = _destDirFullName + ftpFile.Name
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
                        ActionType = AppUpdateActionTypeEnum.Replace,
                        FullNameFrom = ftpFile.FullName,
                        FullNameTo = fInfo.FullName,
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
                                ActionType = AppUpdateActionTypeEnum.AddNew,
                                FullNameFrom = ftpFile.FullName,
                                FullNameTo = destFileFullName
                            };
                            _updateItems.Add(updItem);
                        }
                        // иначе - обновить
                        else if ((!ftpFile.IsConfigFile) && (isNeedFileUpdate(ftpFile, destFileFullName)))
                        {
                            AppUpdateItem updItem = new AppUpdateItem()
                            {
                                ItemType = FSItemTypeEnum.File,
                                ActionType = AppUpdateActionTypeEnum.Replace,
                                FullNameFrom = ftpFile.FullName,
                                FullNameTo = destFileFullName
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
                    ActionType = AppUpdateActionTypeEnum.AddNew,
                    FullNameFrom = ftpItem.FullName,
                    FullNameTo = destFolder
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
            // разный размер файла и дата создания файла из хранилища БОЛЬШЕ даты создания из целевой папки
            bool retVal = (ftpFile.Size != fInfo.Length) || (ftpFile.DateTime > fInfo.LastWriteTime);

            // при одинаковом размере и дате создания, для exe/dll дополнительно проверить версию файла
            if ((retVal == false) && ((fInfo.Extension == "exe") || (fInfo.Extension == "dll")))
            {
                FTPHelper ftpHelper = getFTPHelper();
                string checkedFileFullName = _tmpDir + ftpFile.Name;
                if (ftpHelper.DownloadFile(ftpFile.FullName, checkedFileFullName))
                {
                    // версия
                    string appFileVersion = AppEnvironment.GetFileVersion(fInfo.FullName);
                    string updFileVersion = AppEnvironment.GetFileVersion(checkedFileFullName);
                    retVal = (appFileVersion != updFileVersion);
                }
            }

            return retVal;
        }

        private AppUpdateItem getConfigModifyItems(FTPItem ftpItem, FileInfo fInfo)
        {
            // скачать файл с ФТП
            string ftpDestFile = _tmpDir + fInfo.Name;
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

                // все элементы - add -> возможный config-файл
                if (xDocSrc.Root.Elements().All(e => e.Name.LocalName == "add"))
                {
                    // если все add-элементы содержат только два элемента key и value, 
                    // то это "правильный" config-файл, т.е. его можно преобразовать в xml для сравнения
                    // где атрибут key становится именем элемента!
                    if (isCorrectConfigFile(xDocSrc))
                    {
                        xDocSrc = convertConfigToXML(xDocSrc);
                        xDocDst = convertConfigToXML(xDocDst);
                    }
                    // "неправильный" config-файл пропускаем
                    else
                    {
                        return null;
                    }
                }
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
                    retVal = new AppUpdateCfgFile(ftpItem.FullName, fInfo.FullName);
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
            if (_errorMsg != null) _errorMsg = null;
            if ((_updateItems == null) || (_updateItems.Count == 0)) return false;
            bool result;
            clearTmpDir();

            foreach (AppUpdateItem updItem in _updateItems)
            {
                string sFrom = updItem.FullNameFrom;
                string sTo = updItem.FullNameTo;

                if (updItem.ItemType == FSItemTypeEnum.Folder)
                {
                    // папки можно только добавлять! вместе со всеми вложенными подпапками и файлами!
                    #region download folder
                    if (updItem.ActionType == AppUpdateActionTypeEnum.AddNew)
                    {
                        UpdateActionBefore?.Invoke(this, $"Загружаю папку '{sFrom}' в '{sTo}'");
                        FTPFolder ftpFolderFrom = getFTPFolderFrom(_updFTPFolder, updItem.FullNameFrom);
                        if (ftpFolderFrom == null)
                        {
                            UpdateActionBefore?.Invoke(this, $"Ошибка получения папки с FTP: " + _errorMsg);
                        }
                        else
                        {
                            result = createDirectory(ftpFolderFrom, updItem.FullNameTo);
                            if (result)
                            {
                                UpdateActionAfter?.Invoke(this, $"Папка '{sTo}' создана успешно");
                            }
                            else
                            {
                                UpdateActionAfter?.Invoke(this, $"Ошибка получения папки с FTP: " + _errorMsg);
                                return false;
                            }
                        }
                    }
                    #endregion
                }
                else if (updItem.ItemType == FSItemTypeEnum.File)
                {
                    #region download file
                    // добавление файла
                    if (updItem.ActionType == AppUpdateActionTypeEnum.AddNew)
                    {
                        downoadFile(sFrom ,sTo);
                    }

                    // замена файла
                    else if (updItem.ActionType == AppUpdateActionTypeEnum.Replace)
                    {
                        UpdateActionBefore?.Invoke(this, $"Обновление файла '{sTo}'...");
                        // загрузить файл в _tmpDir
                        FileInfo fInfo = new FileInfo(sTo);
                        result = downoadFile(sFrom, _tmpDir + fInfo.Name);
                        if (result)
                        {
                            // исходный скопировать в архив
                            result = copyFileToArchive(fInfo);
                            if (result)
                            {
                                FileInfo tmpFInfo = new FileInfo(_tmpDir + fInfo.Name);
                                // скопировать файл из _tmpDir в файл-назначение
                                result = copyFileTo(tmpFInfo, fInfo.DirectoryName);
                                if (result)
                                {
                                    UpdateActionAfter?.Invoke(this, $"Файл обновлен успешно: " + sTo);
                                }
                                else
                                {
                                    UpdateActionAfter?.Invoke(this, $"Ошибка обновления файла '{sTo}': " + _errorMsg);
                                    return false;
                                }
                            }
                            else
                            {
                                UpdateActionAfter?.Invoke(this, $"Ошибка обновления файла '{sTo}': " + _errorMsg);
                                return false;
                            }
                        }
                    }

                    // обновление config-файла
                    else if ((updItem.ActionType == AppUpdateActionTypeEnum.Modify) && (updItem is AppUpdateCfgFile))
                    {
                        // TODO update config-file
                    }
                    #endregion
                }
            }

            clearTmpDir();
            return true;
        }

        private bool copyFileToArchive(FileInfo fInfo)
        {
            string archivePath = _destDirFullName + "archive\\";
            if (createDirectory(archivePath) == false) return false;

            archivePath += DateTime.Now.ToString("yyyy-MM-dd") + "\\";
            if (createDirectory(archivePath) == false) return false;

            return copyFileTo(fInfo, archivePath);
        }

        private bool copyFileTo(FileInfo fInfo, string destPath)
        {
            try
            {
                if (!destPath.EndsWith("\\")) destPath += "\\";
                fInfo.CopyTo(destPath + fInfo.Name, true);
            }
            catch (Exception ex)
            {
                _errorMsg = ex.ToString();
                return false;
            }
            return true;
        }

        private bool createDirectory(string dirFullPath)
        {
            if (!Directory.Exists(dirFullPath))
            {
                try
                {
                    Directory.CreateDirectory(dirFullPath);
                }
                catch (Exception ex)
                {
                    _errorMsg = ex.ToString();
                    return false;
                }
            }
            return true;
        }

        private bool downoadFile(string sFrom, string sTo)
        {
            UpdateActionBefore?.Invoke(this, $"Загружаю файл из '{sFrom}' в '{sTo}'");
            FTPHelper ftpHelper = getFTPHelper();
            bool result = ftpHelper.DownloadFile(sFrom, sTo);
            if (result)
            {
                UpdateActionAfter?.Invoke(this, $"Файл '{sTo}' создан успешно");
            }
            else
            {
                UpdateActionAfter?.Invoke(this, $"Ошибка загрузки файла '{sTo}': " + ftpHelper.LastErrorMessage);
            }
            ftpHelper = null;
            return result;
        }

        private bool createDirectory(FTPFolder ftpFolderFrom, string destDirectory)
        {
            DirectoryInfo dirInfo = null;
            try
            {
                // создать папку назначения
                if (System.IO.Directory.Exists(destDirectory) == false)
                {
                    dirInfo = Directory.CreateDirectory(destDirectory);
                }
                // или очистить ее
                else
                {
                    dirInfo = new DirectoryInfo(destDirectory);
                    foreach (FileInfo item in dirInfo.EnumerateFiles()) item.Delete();
                    foreach (DirectoryInfo item in dirInfo.EnumerateDirectories()) item.Delete(true);
                }
            }
            catch (Exception ex)
            {
                _errorMsg = ex.ToString();
                return false;
            }

            // файлы загрузить с ФТП
            bool result;
            if (ftpFolderFrom.Items.Any(f => f.ItemType== FSItemTypeEnum.File))
            {
                FTPHelper ftpHelper = getFTPHelper();
                foreach (FTPItem item in ftpFolderFrom.Items.Where(f => f.ItemType == FSItemTypeEnum.File))
                {
                    result = ftpHelper.DownloadFile(item.FullName, destDirectory + item.Name);
                    if (result == false) { _errorMsg = ftpHelper.LastErrorMessage; return false; }
                }
                ftpHelper = null;
            }
            // папки создать
            if (ftpFolderFrom.Items.Any(f => f.ItemType == FSItemTypeEnum.Folder))
            {
                foreach (FTPFolder item in ftpFolderFrom.Items.Where(f => f.ItemType == FSItemTypeEnum.Folder))
                {
                    result = createDirectory(item, destDirectory + item.Name + "\\");
                    if (result == false) { return false; }
                }
            }

            return true;
        }

        private FTPFolder getFTPFolderFrom(FTPFolder ftpFolder, string fullNameFrom)
        {
            string[] flds = fullNameFrom.Substring(ftpFolder.FullName.Length).Split('/');

            FTPFolder retVal = ftpFolder;
            for (int i = 0; (i < flds.Length) && (retVal != null); i++)
            {
                retVal = (FTPFolder)retVal.Items.FirstOrDefault(fld => (fld is FTPFolder) && (fld.Name == flds[i]));
            }

            if (retVal == null) _errorMsg = "Папка '" + fullNameFrom + "' не найдена!!";

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
                    bld.Append("\t" + r.ToString());
                });
            }

            return bld.ToString();
        }

        // проверка на config, т.е. файл, который состоит из элементов add, 
        // каждый из которых имеет строго по 2 атрибута: key и value 
        private bool isCorrectConfigFile(XDocument xDoc)
        {
            bool retVal = true;

            foreach (XElement item in xDoc.Root.Elements())
            {
                if (item.Name.LocalName != "add") { retVal = false;  break; }

                IEnumerable<XAttribute> attrs = item.Attributes();
                if (attrs.Count() != 2) { retVal = false;  break; }
                if ((attrs.ElementAt(0).Name.LocalName != "key") && (attrs.ElementAt(1).Name.LocalName != "value")) { retVal = false; break; }
            }

            return retVal;
        }

        private XDocument convertConfigToXML(XDocument xConfigDoc)
        {
            XElement[] keys = xConfigDoc.Root.Elements().Select(e =>
                new XElement(e.Attribute("key").Value, new XAttribute(e.Attribute("value")))
                ).ToArray();
            XDocument xDoc = new XDocument(xConfigDoc.Declaration, new XElement(xConfigDoc.Root.Name, xConfigDoc.Root.Attributes(), keys));
            return xDoc;
        }

    }  // class

    #region other classes

    public class AppUpdateItem
    {
        public string FullNameFrom { get; set; }
        public string FullNameTo { get; set; }
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


        public AppUpdateCfgFile(string fileNameFrom, string fileNameTo)
        {
            this.ItemType = FSItemTypeEnum.File;
            this.FullNameFrom = fileNameFrom;
            this.FullNameTo = fileNameTo;
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
