using ClientOrderQueue.Lib;
using IntegraLib;
using IntegraWPFLib;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace ClientOrderQueue.View
{
    /// <summary>
    /// Interaction logic for SplashScreen.xaml
    /// </summary>
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();

            this.lblMessage.SetBinding(Label.ContentProperty,
                new Binding()
                {
                    Source = SplashScreenLib.MessageListener.Instance,
                    Path = new PropertyPath("Message")
                });

            // background image
            string fileFullName = getSplashBackImageFile();
            if (fileFullName != null)
            {
                splashBackImage.Source = ImageHelper.GetBitmapImage(fileFullName);
                IntegraWPFLib.DispatcherHelper.DoEvents();
            }
        }

        private string getSplashBackImageFile()
        {
            string hor = CfgFileHelper.GetAppSetting("SplashBackImageHorizontal");
            string ver = CfgFileHelper.GetAppSetting("SplashBackImageVertical");
            string fileName = (WpfHelper.IsAppVerticalLayout ? ver : hor);
            if (fileName == null) return null;
            if (System.IO.File.Exists(fileName) == false) return null;

            if (fileName.Contains(@"/")) fileName = fileName.Replace(@"/", "\\");

            return AppEnvironment.GetFullFileName("", fileName);
        }


    } // class
}
