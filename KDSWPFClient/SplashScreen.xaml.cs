using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;


namespace KDSWPFClient
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
        }

    }  // class
}
