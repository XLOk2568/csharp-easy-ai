using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using XLOKProject;

namespace NVTemperatureControl
{
    /// <summary>
    /// HomePage2.xaml 的交互逻辑
    /// </summary>
    public partial class HomePage2 : Page
    {
        public static int PCHW = 0;
        public HomePage2()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            tesrt.Text = "Hello World!";
            MainWindow._title = "退出";
            MainWindow._messageText = "是否保存当前窗口位置？";
            MainWindow._buttonTextList = new List<string> { "保存", "不保存" };
            MyPrompt msgWin = new MyPrompt();
            msgWin.ShowDialog();
        }
    }
}
