using IntegraLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace IntergaLib
{
    /// <summary>
    /// Класс автообновления приложения.
    /// В конструктор передаются: путь к фолдеру настроек, адрес хранилища, логин и пароль к хранилищу
    /// </summary>
    public class AppAutoUpdater: IDisposable
    {
        #region fields
        private bool _enable;
        public bool Enable { get { return _enable; } }

        private string _registryPath;
        public string RegistryPath { get { return _registryPath; } }
        // данные из реестра
        private string _storagePath, _storageLogin, _storagePWD;
        public string StoragePath { get { return _storagePath; } }
        private bool _autoCreateRegKeys;

        private string _errorMsg;
        public string LastError { get { return _errorMsg; } }

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
        // папка архивных файлов
        private string _archivePath;

        // log file
        public string LogFile { get; set; }

        public event EventHandler<string> UpdateActionBefore;
        public event EventHandler<string> UpdateActionAfter;

        #endregion


        public AppAutoUpdater(string appName, string destDirFullName = null, bool autoCreateRegKeys = false)
        {
            _updateItems = new List<AppUpdateItem>();

            _registryPath = appName + "/Update/";
            _autoCreateRegKeys = autoCreateRegKeys;
            getUpdateSettingsFromRegistry();

            initPathes(destDirFullName);
        }

        private void initPathes(string destDirFullName = null)
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

        // В реестре ветка обновления приложения должна содержать следующие ключи:
        // Enable - enable update, Sourse - full FTP path, FTPLogin, FTPPassword
        // _storagePath = "ftp://82.207.112.88/IT Department/!Soft_dev/KDS/ClientQueue/"
        // _storageLogin = "integra-its\\ftp"
        // _storagePWD = "Qwerty1234"
        private void getUpdateSettingsFromRegistry()
        {
            bool regResult;
            string regValue = RegistryHelper.GetCompanyValue(_registryPath, "Enable");
            if ((regValue == null) && _autoCreateRegKeys)  // add & enable
            {
                _storagePath = "ftp://82.207.112.88/IT Department/!Soft_dev/KDS/ClientQueue/";
                _storageLogin = "integra-its\\ftp";
                _storagePWD = "Qwerty1234";

                regResult = true;
                regResult &= RegistryHelper.SetCompanyValue(_registryPath, "Enable", "1");
                regResult &= RegistryHelper.SetCompanyValue(_registryPath, "Source", _storagePath);
                regResult &= RegistryHelper.SetCompanyValue(_registryPath, "FTPLogin", _storageLogin);
                regResult &= RegistryHelper.SetCompanyValue(_registryPath, "FTPPassword", _storagePWD);
                _enable = regResult;
            }
            else
            {
                _enable = regValue.ToBool();
                if (_enable)
                {
                    _storagePath = RegistryHelper.GetCompanyValue(_registryPath, "Source");
                    _storageLogin = RegistryHelper.GetCompanyValue(_registryPath, "FTPLogin");
                    _storagePWD = RegistryHelper.GetCompanyValue(_registryPath, "FTPPassword");
                }
            }
        }

        // проверка необходимости обновления, создать действия обновления приложения
        public bool IsNeedUpdate()
        {
            if (_errorMsg != null) _errorMsg = null;
            if (_enable == false) return false;

            // получить папки версий
            FTPHelper ftpHelper = getFTPHelper();
            FTPFolder ftpFolder = ftpHelper.GetFTPFolder(_storagePath, false);
            if ((ftpFolder != null) && (ftpFolder.Items.Count > 0))
            {
                // папка с самой свежей версией (сравнение по дате)
                FTPFolder newestFolder = getNewestFTPFolder(ftpFolder);
                if (newestFolder == null)
                {
                    _errorMsg = "В папке хранилища версий не обраружена папка для обновления";
                    return false;
                }
                UpdateActionBefore?.Invoke(this, "Папка с последней версией: " + newestFolder.Name);

                // получить рекурсивно имена ВСЕХ файлов из папки последней версии
                if (_storagePath.RightString(1) != "/") _storagePath += "/";
                _updFTPFolder = ftpHelper.GetFTPFolder(_storagePath + newestFolder.Name + "/", true);
                _updFTPFolder.Name = newestFolder.Name;

                #region проверка необходимости обновления
                clearTmpDir(); // очистить папку файлов, загружаемых с ФТП
                _updateItems.Clear();
                UpdateActionBefore?.Invoke(this, "Create update actions...");

                compareFolders(_updFTPFolder, _destDirFullName);

                if (_errorMsg != null)
                {
                    UpdateActionBefore?.Invoke(this, "Not created update actions due to error: " + _errorMsg);
                }
                else
                {
                    if (_updateItems.Count == 0)
                        UpdateActionBefore?.Invoke(this, "Not need update, no any actions");
                    else
                        UpdateActionBefore?.Invoke(this, "Update reasons: " + Environment.NewLine + "\t" + string.Join(Environment.NewLine + "\t", _updateItems));
                }
                clearTmpDir();
                #endregion
            }
            else
            {
                _errorMsg = "Ошибка получения папки хранилища версий '" + _storagePath + "' : " + ftpHelper.LastErrorMessage; 
            }

            bool retVal = ((_updateItems.Count > 0) && (_errorMsg == null));
            return retVal;
        }

        private FTPFolder getNewestFTPFolder(FTPFolder storageFolders)
        {
            DateTime dtMax = DateTime.MinValue;
            FTPFolder retVal = null;
            foreach (FTPFolder item in storageFolders.Items.Where(f => f is FTPFolder).Select(f => (FTPFolder)f))
            {
                if (item.DateTime > dtMax)
                {
                    retVal = item;
                    dtMax = item.DateTime;
                }
            }

            return retVal;
        }

        #region FTPHelper
        private FTPHelper getFTPHelper(bool isCreateHandlers = true)
        {
            FTPHelper retVal = new FTPHelper()
            {
                Login = _storageLogin,
                PWD = _storagePWD,
                IsAlive = false,
                IsPassive = true
            };
            
            if (isCreateHandlers)
            {
                retVal.DownloadFileBeforeHandler += FtpHelper_DownloadFileBeforeHandler;
                retVal.DownloadFileAfterHandler += FtpHelper_DownloadFileAfterHandler;
            }

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


        private void compareFolders(FTPFolder ftpPath, string localDestPath)
        {
            foreach (FTPItem ftpItem in ftpPath.Items)
            {
                UpdateActionBefore?.Invoke(this, "- check " + ftpItem.ItemType.ToString() + ": " + ftpItem.Name);

                // файлы в целевой папке могут быть добавлены, заменены или модифицированы (для *.config)
                // config-файлы по умолчанию не заменяются!!
                if (ftpItem.ItemType == FSItemTypeEnum.File)
                {
                    // проверка наличия файла
                    FTPFile ftpFile = (FTPFile)ftpItem;
                    string localFileFullName = localDestPath + ftpItem.Name;
                    if (File.Exists(localFileFullName) == false)
                    {
                        UpdateActionBefore?.Invoke(this, "  - add new file: " + localFileFullName);
                        createActionNewFile(ftpFile.FullName, localFileFullName, ftpItem.Name);
                    }
                    else
                    {
                        createActionsForUpdateFile(ftpFile, localDestPath);
                    }
                }

                // папки могут только добавляться, файлы в них могут быть только добавлены или заменены
                // структура фолдеров начинается от _storagePath (FTP) и _updatePath (целевая папка)
                // для FTP есть _updFTPFolder, который содержит структуру папки-источника обновления
                else if (ftpItem.ItemType == FSItemTypeEnum.Folder)
                {
                    // проверка наличия целевой папки в destPath
                    string destFolder = localDestPath + ftpItem.Name + "\\";
                    // папки нет - записать создание (при выполнении действия, создавать структуру папок рекурсивно)
                    if (Directory.Exists(destFolder) == false)
                    {
                        createActionNewFolder(ftpItem.FullName, destFolder);
                    }
                    else
                    {
                        compareFolders((FTPFolder)ftpItem, destFolder);
                    }
                }
                if (_errorMsg != null)
                {
                    UpdateActionBefore?.Invoke(this, "- check error: " + _errorMsg);
                }
            }
        }

        private void createActionNewFolder(string pathFrom, string pathTo)
        {
            _updateItems.Add(new AppUpdateItem()
            {
                ItemType = FSItemTypeEnum.Folder,
                ActionType = AppUpdateActionTypeEnum.AddNew,
                FullNameFrom = pathFrom,
                FullNameTo = pathTo,
                FileAttribute = "exists",
                ValueExist = "false",
                ValueNew = "true"
            });
        }

        private void createActionNewFile(string fullNameFrom, string fullNameTo, string fileName)
        {
            _updateItems.Add(new AppUpdateItem()
            {
                ItemType = FSItemTypeEnum.File,
                ActionType = AppUpdateActionTypeEnum.AddNew,
                FullNameFrom = fullNameFrom,
                FullNameTo = fullNameTo,
                FileName = fileName,
                FileAttribute = "exists",
                ValueExist = "false",
                ValueNew = "true"
            });
        }

        // надо ли заменить СУЩЕСТВУЮЩИЙ файл?
        private void createActionsForUpdateFile(FTPFile ftpFile, string localDestPath)
        {
            string localFileFullName = localDestPath + ftpFile.Name;
            FileInfo fInfo = new FileInfo(localFileFullName);
            if (!fInfo.Exists) return;

            AppUpdateItem updItem;
            bool needUpdate = false;

            // config-файлы проверяем по внутреннему содержанию, как xml-файлы
            if (ftpFile.IsConfigFile)
            {
                updItem = getConfigModifyItems(ftpFile, fInfo);
                if (updItem != null)
                {
                    needUpdate = true;
                    _updateItems.Add(updItem);
                    UpdateActionBefore?.Invoke(this, "  - need " + updItem.ToString());
                }
            }

            // прочие файлы проверяем по размеру, дате и версии
            else 
            {
                // дата создания
                if (ftpFile.DateTime > fInfo.LastWriteTime)
                {
                    updItem = new AppUpdateItem()
                    {
                        ItemType = FSItemTypeEnum.File,
                        ActionType = AppUpdateActionTypeEnum.Replace,
                        FullNameFrom = ftpFile.FullName,
                        FullNameTo = localFileFullName,
                        FileName = ftpFile.Name,
                        FileAttribute = "lastWriteTime",
                        ValueExist = fInfo.LastWriteTime.ToString(),
                        ValueNew = ftpFile.DateTime.ToString()
                    };
                    needUpdate = true;
                    _updateItems.Add(updItem);
                    UpdateActionBefore?.Invoke(this, "  - need " + updItem.ToString());
                }
                // размер файла
                if (ftpFile.Size != fInfo.Length)
                {
                    updItem = new AppUpdateItem()
                    {
                        ItemType = FSItemTypeEnum.File,
                        ActionType = AppUpdateActionTypeEnum.Replace,
                        FullNameFrom = ftpFile.FullName,
                        FullNameTo = localFileFullName,
                        FileName = ftpFile.Name,
                        FileAttribute = "size",
                        ValueExist = fInfo.Length.ToString(),
                        ValueNew = ftpFile.Size.ToString()
                    };
                    needUpdate = true;
                    _updateItems.Add(updItem);
                    UpdateActionBefore?.Invoke(this, "  - need " + updItem.ToString());
                }
                // при одинаковом размере и дате создания, для exe/dll дополнительно проверить версию файла
                if ((fInfo.Extension == ".exe") || (fInfo.Extension == ".dll"))
                {
                    UpdateActionBefore?.Invoke(this, "  - request to check file version " + ftpFile.Name + " ...");
                    string tmpFile = _tmpDir + ftpFile.Name;
                    if (downloadFile(ftpFile.FullName, tmpFile))
                    {
                        string appFileVersion = AppEnvironment.GetFileVersion(localFileFullName);
                        string updFileVersion = AppEnvironment.GetFileVersion(tmpFile);
                        UpdateActionBefore?.Invoke(this, $"    - current version: {appFileVersion}, storage version: {updFileVersion}");
                        if (appFileVersion != updFileVersion)
                        {
                            updItem = new AppUpdateItem()
                            {
                                ItemType = FSItemTypeEnum.File,
                                ActionType = AppUpdateActionTypeEnum.Replace,
                                FullNameFrom = ftpFile.FullName,
                                FullNameTo = localFileFullName,
                                FileName = ftpFile.Name,
                                FileAttribute = "version",
                                ValueExist = appFileVersion,
                                ValueNew = updFileVersion
                            };
                            needUpdate = true;
                            _updateItems.Add(updItem);
                            UpdateActionBefore?.Invoke(this, "  - need " + updItem.ToString());
                        }
                    }
                }

            }

            if (!needUpdate && (_errorMsg == null))
            {
                UpdateActionBefore?.Invoke(this, "  - NO need to update");
            }
        }


        private AppUpdateItem getConfigModifyItems(FTPItem ftpItem, FileInfo fInfo)
        {
            if (_enable == false) return null;

            // скачать файл с ФТП
            string ftpDestFile = _tmpDir + fInfo.Name;
            if (downloadFile(ftpItem.FullName, ftpDestFile) == false) return null;

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

            // если все add-элементы содержат только два элемента key и value, 
            // то это "правильный" config-файл, т.е. его можно преобразовать в xml для сравнения
            // где атрибут key становится именем элемента!
            if (ConfigXMLConverter.IsValidConfigFile(xDocSrc))
            {
                xDocSrc = ConfigXMLConverter.ConvertConfigToXML(xDocSrc);
                xDocDst = ConfigXMLConverter.ConvertConfigToXML(xDocDst);
            }

            AppUpdateCfgFile retVal = null;
            XMLComparer xComparer = new XMLComparer(xDocSrc, xDocDst);
            if (xComparer.Compare())
            {
                // все изменения в xml-файлах
                List<XMLCompareChangeItem> result = xComparer.Changes;
                if (result.Count == 0)
                {
                    //Console.WriteLine("изменений нет");
                }
                else
                {
                    retVal = new AppUpdateCfgFile(ftpItem.FullName, fInfo.FullName);
                    retVal.ActionType = AppUpdateActionTypeEnum.Modify;
                    retVal.FileName = ftpItem.Name;
                    retVal.FileAttribute = "content";
                    // выбрать только добавление или удаление элементов/атрибутов
                    foreach (XMLCompareChangeItem item 
                        in (from r in result
                            where (r.Result == XMLCompareResultEnum.AddNew) || (r.Result == XMLCompareResultEnum.Remove)
                            select r))
                    {
                        retVal.UpdateItems.Add(item);
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
            if (_enable == false) return false;
            if ((_updateItems == null) || (_updateItems.Count == 0)) return false;

            clearTmpDir();
            if (_errorMsg != null) _errorMsg = null;
            bool result;

            // в цикле по _updateItems выбрать exe/dll файлы, которые нужно обновить - они будут
            // обновлены в последнюю очередь специальной утилитой (waitCopy.exe), 
            // т.к. эти файлы могут быть заблокированы системой
            // В коллекции runFiles собираются только имена файлов
            List<string> runFiles = new List<string>();

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
                        UpdateActionBefore?.Invoke(this, $" - добавление папки '{sTo}' из '{sFrom}'...");
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
                                UpdateActionAfter?.Invoke(this, $" - папка '{sTo}' создана успешно");
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
                    // добавление файла
                    if (updItem.ActionType == AppUpdateActionTypeEnum.AddNew)
                    {
                        UpdateActionBefore?.Invoke(this, $" - добавление файла '{sTo}'...");
                        result = downloadFile(sFrom, sTo);
                        if (result == false)
                        {
                            UpdateActionAfter?.Invoke(this, $"Ошибка загрузки файла '{sFrom}' в папку '{sTo}': " + _errorMsg);
                            return false;
                        }
                        UpdateActionBefore?.Invoke(this, $" - файл '{sTo}' добавлен успешно");
                    }

                    // замена файла
                    else if (updItem.ActionType == AppUpdateActionTypeEnum.Replace)
                    {
                        #region replace file
                        UpdateActionBefore?.Invoke(this, $" - замена файла '{sTo}' файлом '{sFrom}'...");
                        // имя файла может содержать вложенные папки, структуру которых необходимо повторить
                        // во временной (_tmpDir) и архивной (_archive) папках
                        string fNameTo = sTo.Substring(_destDirFullName.Length);
                        // загрузить файл в _tmpDir
                        result = downloadFile(sFrom, _tmpDir + fNameTo);
                        if (result)
                        {
                            // исходный скопировать в архив
                            result = copyFileToArchive(_destDirFullName, fNameTo);
                            if (result)
                            {
                                // скопировать файл из _tmpDir в файл-назначение
                                result = copyFileTo(_tmpDir, fNameTo, _destDirFullName);
                                if (result)
                                {
                                    UpdateActionAfter?.Invoke(this, " - файл заменен успешно");
                                }
                                else
                                {
                                    // файлы, заблокированные системой сохранить в отдельный набор, 
                                    // чтобы обновить в конце процедуры обновления, т.к. эти файлы
                                    // будут обновлены при перезапуске приложения
                                    if (_errorMsg.StartsWith("IOException"))
                                    {
                                        runFiles.Add(fNameTo);
                                        UpdateActionAfter?.Invoke(this, $"Ошибка замены файла '{sTo}': " + _errorMsg);
                                    }
                                    else
                                    {
                                        UpdateActionAfter?.Invoke(this, $"Ошибка замены файла '{sTo}': " + _errorMsg);
                                        return false;
                                    }
                                }
                            }
                            else
                            {
                                UpdateActionAfter?.Invoke(this, $"Ошибка замены файла '{sTo}': " + _errorMsg);
                                return false;
                            }
                        }
                        #endregion
                    }

                    // обновление config-файла
                    else if ((updItem.ActionType == AppUpdateActionTypeEnum.Modify) && (updItem is AppUpdateCfgFile))
                    {
                        #region modify config-file
                        AppUpdateCfgFile updCfgFile = (updItem as AppUpdateCfgFile);
                        bool isConfig = false;
                        string updItemsString = string.Join(Environment.NewLine, updCfgFile.UpdateItems.Select(i => "\t" + i.ToString()).ToArray());
                        UpdateActionBefore?.Invoke(this, " - обновление config-файла '" + sTo + "':" + Environment.NewLine + updItemsString);
                        string fNameTo = sTo.Substring(_destDirFullName.Length);

                        // 1. загрузить файл в _tmpDir
                        result = downloadFile(sFrom, _tmpDir + fNameTo);
                        if (result == false)
                        {
                            UpdateActionAfter?.Invoke(this, $"Ошибка загрузки файла '{sFrom}' в папку '{_tmpDir}': " + _errorMsg);
                            return false;
                        }

                        // 2. исходный скопировать в архив
                        result = copyFileToArchive(_destDirFullName, fNameTo);
                        if (result == false)
                        {
                            UpdateActionAfter?.Invoke(this, $"Ошибка копирования файла '{fNameTo}' в архивную папку приложения: " + _errorMsg);
                            return false;
                        }

                        // 3. обновить файл-назначение файлом из _tmpDir
                        // 3.1. открыть файлы
                        XDocument xDocSrc = null, xDocDst = null;
                        _errorMsg = null;
                        try
                        {
                            xDocSrc = XDocument.Load(_tmpDir + fNameTo);
                            xDocDst = XDocument.Load(sTo);
                        }
                        catch (Exception ex)
                        {
                            _errorMsg = ex.Message;
                        }
                        if ((xDocSrc == null) || (xDocDst == null) || (_errorMsg != null))
                        {
                            _errorMsg = "Ошибка загрузки xml-файла: " + _errorMsg;
                            return false;
                        }
                        // 3.2. преобразовать, если надо, config в xml 
                        if (ConfigXMLConverter.IsValidConfigFile(xDocSrc))
                        {
                            isConfig = true;
                            xDocSrc = ConfigXMLConverter.ConvertConfigToXML(xDocSrc);
                            xDocDst = ConfigXMLConverter.ConvertConfigToXML(xDocDst);
                        }
                        // 3.3. обновить файлы в памяти
                        XMLComparer xComparer = new XMLComparer(xDocSrc, xDocDst);
                        result = xComparer.Update(updCfgFile.UpdateItems);
                        if (result == false)
                        {
                            _errorMsg = xComparer.ErrorMessage;
                            UpdateActionAfter?.Invoke(this, $"Ошибка обновления файла '{sTo}': " + _errorMsg);
                            return false;
                        }
                        // 3.4. преобразовать, если надо, обратно xml в config
                        if (isConfig && ConfigXMLConverter.CanConvertToConfig(xDocDst))
                        {
                            xDocDst = ConfigXMLConverter.ConvertXMLToConfig(xDocDst);
                        }

                        // 4. записать файл-назначение на диск
                        try
                        {
                            System.Xml.XmlWriterSettings xmlSettings = new System.Xml.XmlWriterSettings()
                            {
                                NamespaceHandling = System.Xml.NamespaceHandling.Default,
                                NewLineChars = Environment.NewLine,
                                NewLineHandling = System.Xml.NewLineHandling.Replace,
                                NewLineOnAttributes = false,
                                Indent = true, IndentChars = "   "
                            };
                            using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(sTo, xmlSettings))
                            {
                                xDocDst.WriteTo(writer);
                            }
                        }
                        catch (Exception ex)
                        {
                            _errorMsg = "Ошибка записи xml-файла '" + sTo + "' на диск: " + ex.ToString();
                            throw;
                        }

                        UpdateActionAfter?.Invoke(this, $" - файл обновлен успешно");
                        #endregion
                    }
                }
            }

            if (runFiles.Count > 0)
            {
                if (!updateRunFiles(runFiles)) return false;
            }

            clearTmpDir();
            return true;
        }

        // обновление run-time файлов: exe/dll
        private bool updateRunFiles(List<string> runFiles)
        {
            // загрузить утилиту копирования в папку временных файлов приложения _tmpDir
            string copyAppFullNameFrom = "ftp://82.207.112.88/IT Department/!Soft_dev/_utilities/waitCopy.exe";
            string copyAppName = copyAppFullNameFrom.Substring(copyAppFullNameFrom.LastIndexOf('/') + 1);
            string copyAppFullName = _tmpDir + copyAppName;
            bool result = downloadFile(copyAppFullNameFrom, copyAppFullName);
            if (!result) return false;

            // удалить из путей конечный слэш
            string sourceDir = removeEndSlash(_tmpDir);
            string sourceFiles = string.Join(";", runFiles);
            string destDir = removeEndSlash(_destDirFullName);

            // текущий процесс
            System.Diagnostics.Process curProcess = System.Diagnostics.Process.GetCurrentProcess();

            // запустить процесс копирования
            System.Diagnostics.ProcessStartInfo pInfo = new System.Diagnostics.ProcessStartInfo();
            pInfo.FileName = copyAppFullName;
            // 10 попыток копирования через 500 мсек
            pInfo.Arguments = string.Format($"-sp \"{sourceDir}\" -f \"{sourceFiles}\" -dp \"{destDir}\" -t 500 -c 10 -r");
            if (this.LogFile != null) pInfo.Arguments += " -l \"" + this.LogFile + "\"";
            System.Diagnostics.Process.Start(pInfo);

            curProcess.Kill();

            return true;
        }

        private string removeEndSlash(string pathName)
        {
            if (pathName.EndsWith("\\")) pathName = pathName.Remove(pathName.Length - 1, 1);
            return pathName;
        }


        private bool copyFileToArchive(string srcPath, string fileName)
        {
            if (_archivePath == null)
            {
                _archivePath = _destDirFullName + "archive\\";
                if (createDirectory(_archivePath) == false) return false;

                _archivePath += DateTime.Now.ToString("yyyy-MM-dd HHmmss") + "\\";
                if (createDirectory(_archivePath) == false) return false;
            }

            return copyFileTo(srcPath, fileName, _archivePath);
        }

        // копирвание файла из папки источника в папку назначение с учетом того, что 
        // имя файла может иметь структуру вложенных папок
        private bool copyFileTo(string srcPath, string fileName, string destPath)
        {
            if (!srcPath.EndsWith("\\")) srcPath += "\\";
            if (!destPath.EndsWith("\\")) destPath += "\\";

            string srcFullName = srcPath + fileName;
            string destFullName = destPath + fileName;

            if (File.Exists(srcFullName) == false)
            {
                _errorMsg = "Файл '" + srcFullName + "' НЕ существует!";
                return false;
            }
            if (checkFileDir(destFullName) == false) return false;

            try
            {
                File.Copy(srcFullName, destFullName, true);
            }
            catch (System.IO.IOException ex)
            {
                _errorMsg = "IOException: " + ex.Message;
                return false;
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

        private bool downloadFile(string sFrom, string sTo)
        {
            if (_enable == false) return false;

            UpdateActionBefore?.Invoke(this, $"Загружаю файл из '{sFrom}' в '{sTo}'");
            if (!checkFileDir(sTo)) return false;

            FTPHelper ftpHelper = getFTPHelper(false);
            bool result = ftpHelper.DownloadFile(sFrom, sTo);
            if (result)
            {
                UpdateActionAfter?.Invoke(this, $"Файл '{sTo}' загружен успешно");
            }
            else
            {
                UpdateActionAfter?.Invoke(this, $"Ошибка загрузки файла '{sTo}': " + ftpHelper.LastErrorMessage);
            }
            ftpHelper = null;
            return result;
        }

        // проверка существования папки назначения для полного имени файла
        // если папка не существует, то она создается
        private bool checkFileDir(string fileFullName)
        {
            FileInfo fi = new FileInfo(fileFullName);
            if (fi.Directory.Exists == false)
            {
                try
                {
                    fi.Directory.Create();
                }
                catch (Exception ex)
                {
                    _errorMsg = ex.Message;
                    return false;
                }
            }
            return true;
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

        // удалить временную папку
        private void deleteTmpDir()
        {
            if (_tmpDir != null)
            {
                try
                {
                    (new DirectoryInfo(_tmpDir)).Delete(true);
                }
                catch { }
            }
        }

        public void Dispose()
        {
            deleteTmpDir();
        }

    }  // class

    #region other classes

    public class AppUpdateItem
    {
        public FSItemTypeEnum ItemType { get; set; }

        public string FullNameFrom { get; set; }
        public string FullNameTo { get; set; }

        public string FileName { get; set; }
        public string FileAttribute { get; set; }
        public string ValueExist { get; set; }
        public string ValueNew { get; set; }


        public AppUpdateActionTypeEnum ActionType { get; set; }

        public override string ToString()
        {
            string retVal = $"{this.ActionType.ToString()} {this.ItemType} '{this.FullNameTo}'";

            if (!string.IsNullOrEmpty(this.FileAttribute))
                retVal += $", attribute=\"{this.FileAttribute}\"";
            if (!string.IsNullOrEmpty(this.ValueExist))
                retVal += $", old value=\"{this.ValueExist}\"";
            if (!string.IsNullOrEmpty(this.ValueNew))
                retVal += $", new value=\"{this.ValueNew}\"";

            return retVal;
        }
    }

    // xml-файл, в котором ТОЛЬКО _добавляются_ или _удаляются_ элементы или атрибуты
    public class AppUpdateCfgFile: AppUpdateItem
    {
        private List<XMLCompareChangeItem> _items;      
        public List<XMLCompareChangeItem> UpdateItems
        {
            get { return _items; }
        }


        public AppUpdateCfgFile(string fileNameFrom, string fileNameTo)
        {
            this.ItemType = FSItemTypeEnum.File;
            this.FullNameFrom = fileNameFrom;
            this.FullNameTo = fileNameTo;
            _items = new List<XMLCompareChangeItem>();
        }

        public override string ToString()
        {
            string retVal = base.ToString();

            string s1 = string.Join(Environment.NewLine + "\t", _items);
            if (!string.IsNullOrEmpty(s1)) retVal += Environment.NewLine + "\t" + s1;

            return retVal;
        }
    }

    public class AppUpdateCfgFileItem
    {
        public List<string> XElementNames { get; set; }
        public string XAttributeName { get; set; }
        public string Value { get; set; }
        public AppUpdateActionTypeEnum Action { get; set; }

        public override string ToString()
        {
            string retVal = $"element: {string.Join("/", XElementNames)}";
            if (string.IsNullOrEmpty(this.XAttributeName) == false)
            {
                retVal += $", attr name: {this.XAttributeName}, attr value: {this.Value}";
            }
            retVal += ", action: " + this.Action.ToString();
            return retVal;
        }
    }

    public enum AppUpdateActionTypeEnum
    {
        None, AddNew, Delete, Modify, Replace
    }

    #endregion
}
