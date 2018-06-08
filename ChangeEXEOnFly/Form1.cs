using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChangeEXEOnFly
{
    public partial class Form1 : Form
    {
        private string _filePathFrom;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _filePathFrom = "D:\\form1\\";
            RestartApplication();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            _filePathFrom = "D:\\form2\\";
            RestartApplication();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            _filePathFrom = "D:\\form3\\";
            RestartApplication();
        }

        // "ftp://integra-its\ftp@82.207.112.88/IT Department/!Soft_dev/_utilities/waitCopy.exe" 
        public void RestartApplication(string args = null)
        {
            System.Diagnostics.Process curProcess = System.Diagnostics.Process.GetCurrentProcess();

            // запустить процесс копирования
            string fileNames = AppDomain.CurrentDomain.FriendlyName + ";test File.txt;test File.docx";
            string dstPath = AppDomain.CurrentDomain.BaseDirectory;

            if (_filePathFrom.EndsWith("\\")) _filePathFrom = _filePathFrom.Remove(_filePathFrom.Length - 1, 1);
            if (dstPath.EndsWith("\\")) dstPath = dstPath.Remove(dstPath.Length - 1, 1);

            System.Diagnostics.ProcessStartInfo pInfo = new System.Diagnostics.ProcessStartInfo();
            pInfo.FileName = "d:\\waitCopy.exe";
            pInfo.Arguments = string.Format($"-sp \"{_filePathFrom}\" -f \"{fileNames}\" -dp \"{dstPath}\" -l \"d:\\waitCopy.log\" -r");
            System.Diagnostics.Process.Start(pInfo);

            curProcess.Kill();
        }

    }  // class
}
