using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;


namespace ClientOrderQueue.Lib
{
    public class FTPHelper
    {
        public string Login { get; set; }
        public string PWD { get; set; }
        public bool IsAlive { get; set; }
        public bool IsPassive { get; set; }

        private string _errorMsg;
        public string LastErrorMessage { get { return _errorMsg; } }


        public FTPHelper()
        {
            this.IsAlive = false;
            this.IsPassive = true;
        }

        public FTPFolder GetFTPFolder(string path, bool isRecurse = false)
        {
            if (_errorMsg != null) _errorMsg = null;
            FTPFolder retVal = null;

            // fields checking
            if (string.IsNullOrEmpty(path)) return retVal;

            try
            {
                if (path.Last() != '/') path += "/";
                FtpWebRequest requestDir = (FtpWebRequest)WebRequest.Create(path);
                requestDir.KeepAlive = this.IsAlive;
                requestDir.UsePassive = this.IsPassive;
                requestDir.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                requestDir.Credentials = new NetworkCredential(this.Login, this.PWD);

                FtpWebResponse responseDir = (FtpWebResponse)requestDir.GetResponse();
                using (StreamReader readerDir = new StreamReader(responseDir.GetResponseStream()))
                {
                    retVal = new FTPFolder() { ItemType = FSItemTypeEnum.Folder, FullName = path };

                    string line = readerDir.ReadLine();
                    FTPItem item;
                    while (line != null)
                    {
                        item = FTPItem.Parse(line);
                        if (item != null)
                        {
                            item.FullName = path + item.Name;
                            if ((item.ItemType == FSItemTypeEnum.Folder) && isRecurse)
                            {
                                string savedName = item.Name;
                                item = GetFTPFolder(path + item.Name + "/", true);
                                item.Name = savedName;
                            }
                            retVal.Items.Add(item);
                        }

                        line = readerDir.ReadLine();
                    }
                }
                responseDir.Close();
            }
            catch (Exception ex)
            {
                _errorMsg = ex.Message;
            }

            return retVal;
        }

        public FTPFile GetFTPFile(string ftpPath, string ftpFileName)
        {
            if (_errorMsg != null) _errorMsg = null;
            FTPFile retVal = null;

            // fields checking
            if (string.IsNullOrEmpty(ftpPath) || string.IsNullOrEmpty(ftpFileName)) return retVal;

            if (!ftpPath.EndsWith("/")) ftpPath += "/";
            try
            {
                FtpWebRequest requestDir = (FtpWebRequest)WebRequest.Create(ftpPath);
                requestDir.KeepAlive = this.IsAlive;
                requestDir.UsePassive = this.IsPassive;
                requestDir.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                requestDir.Credentials = new NetworkCredential(this.Login, this.PWD);

                FtpWebResponse responseDir = (FtpWebResponse)requestDir.GetResponse();
                using (StreamReader readerDir = new StreamReader(responseDir.GetResponseStream()))
                {
                    string line = readerDir.ReadLine();
                    FTPItem item;
                    while (line != null)
                    {
                        item = FTPItem.Parse(line);
                        if ((item != null) && (item.Name == ftpFileName) && (item is FTPFile))
                        {
                            FTPFile ftpFile = (FTPFile)item;
                            retVal = new FTPFile()
                            {
                                ItemType = FSItemTypeEnum.File,
                                FullName = ftpPath + ftpFileName,
                                Name = ftpFileName,
                                DateTime = ftpFile.DateTime,
                                Size = ftpFile.Size
                            };
                            break;
                        }
                        line = readerDir.ReadLine();
                    }
                }
                responseDir.Close();
            }
            catch (Exception ex)
            {
                _errorMsg = ex.Message;
            }

            return retVal;
        }

        public bool DownloadFile(string ftpPath, string fileName, string localDestPath)
        {
            if (ftpPath.EndsWith("/") == false) ftpPath += "/";
            if (localDestPath.EndsWith("\\") == false) localDestPath += "\\";
            string sourceFile = ftpPath + fileName;
            string destFile = localDestPath + fileName;

            bool retVal = DownloadFile(sourceFile, destFile);
            return retVal;
        }

        public bool DownloadFile(string ftpFullFileName, string localFullFileName)
        {
            bool retVal = false;
            _errorMsg = null;

            if (string.IsNullOrEmpty(ftpFullFileName) || string.IsNullOrEmpty(localFullFileName)) return false;

            string[] tmpPair = parseFTPFileFullName(ftpFullFileName);
            if (tmpPair == null)
            {
                _errorMsg = "Error during parse full name to path and name: " + ftpFullFileName;
                return false;
            }

            string ftpPath = tmpPair[0];
            string ftpFileName = tmpPair[1];
            FTPFile ftpFile = GetFTPFile(ftpPath, ftpFileName);
            if (ftpFile == null)
            {
                return false;
            }

            try
            {
                FtpWebRequest requestDir = (FtpWebRequest)WebRequest.Create(ftpFullFileName);
                requestDir.KeepAlive = false;
                requestDir.UsePassive = true;
                requestDir.Method = WebRequestMethods.Ftp.DownloadFile;
                requestDir.Credentials = new NetworkCredential(this.Login, this.PWD);

                FtpWebResponse responseDir = (FtpWebResponse)requestDir.GetResponse();
                Stream responseStream = responseDir.GetResponseStream();
                StreamReader stream = new StreamReader(responseStream);

                using (FileStream writer = new FileStream(localFullFileName, FileMode.Create))
                {
                    long length = responseDir.ContentLength;
                    int bufferSize = 2048;
                    int readCount;
                    byte[] buffer = new byte[2048];

                    readCount = responseStream.Read(buffer, 0, bufferSize);
                    while (readCount > 0)
                    {
                        writer.Write(buffer, 0, readCount);
                        readCount = responseStream.Read(buffer, 0, bufferSize);
                    }
                }

                stream.Dispose();
                responseStream.Dispose();
                responseDir.Dispose();

                // set created date
                FileInfo fInfo = new FileInfo(localFullFileName);
                fInfo.CreationTime = ftpFile.DateTime;
                fInfo.LastWriteTime = ftpFile.DateTime;

                retVal = true;
            }
            catch (Exception ex)
            {
                _errorMsg = ex.Message;
            }

            return retVal;
        }

        private string[] parseFTPFileFullName(string ftpFileFullName)
        {
            if (string.IsNullOrEmpty(ftpFileFullName))
            {
                return null;
            }
            else
            {
                int iDec = ftpFileFullName.LastIndexOf('/');
                if (iDec > -1)
                {
                    string path = ftpFileFullName.Substring(0, iDec + 1);
                    string name = ftpFileFullName.Substring(iDec + 1);
                    return new string[] { path, name};
                }
                else
                    return null;
            }
        }


    }  // class

    public class FTPItem
    {
        public FSItemTypeEnum ItemType { get; set; }

        public string Name { get; set; }

        public string FullName { get; set; }

        public DateTime DateTime { get; set; }

        public static FTPItem Parse(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;

            string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return null;

            FTPItem retVal;
            if (parts[2] == "<DIR>")
            {
                retVal = new FTPFolder();
            }
            else
            {
                retVal = new FTPFile();
                ((FTPFile)retVal).Size = Convert.ToUInt32(parts[2]);
            }

            DateTime dt;
            string s = parts[0] + " " + parts[1];
            if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out dt) == false) return null;
            retVal.DateTime = dt;
            retVal.Name = string.Join(" ", parts.Skip(3));

            return retVal;
        }
    }

    public class FTPFolder : FTPItem
    {
        private List<FTPItem> _items;
        public List<FTPItem> Items { get { return _items; } }

        public FTPFolder()
        {
            base.ItemType = FSItemTypeEnum.Folder;
            _items = new List<FTPItem>();
        }
    }

    public class FTPFile: FTPItem
    {
        private string _ext = null;
        public string Ext
        {
            get
            {
                if (_ext == null) setFileExt();
                return _ext;
            }
        }
        public bool IsConfigFile
        {
            get
            {
                string fileExt = this.Ext;
                if (string.IsNullOrEmpty(fileExt))
                    return false;
                else
                    return (fileExt == "config");
            }
        }

        public uint Size { get; set; }
        public FTPFile()
        {
            base.ItemType = FSItemTypeEnum.File;
        }

        private void setFileExt()
        {
            if (string.IsNullOrEmpty(this.Name))
            {
                _ext = null;
            }
            else
            {
                int iDec = this.Name.LastIndexOf('.');
                if (iDec > -1) _ext = this.Name.Substring(iDec+1).ToLower();
            }
        }

    }  // class

}
