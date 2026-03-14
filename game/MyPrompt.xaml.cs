using NVTemperatureControl;
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
using System.Windows.Shapes;
using Path=System.IO.Path;

namespace XLOKProject
{
    /// <summary>
    /// MyPrompt.xaml 的交互逻辑
    /// </summary>
    public partial class MyPrompt : Window
    {
        public MyPrompt()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            buttonListUI.Children.Clear();
            // 设置窗口位置在主窗口的中心
            MainWindow mainWin = (MainWindow)Application.Current.MainWindow;
            var info = mainWin.InfoMainWindow();
            int MyPromptLeft =info.Left+info.Width/2-(int)Width/2;
            int MyPromptTop = info.Top + info.Height / 2 - (int)Height / 2;
            Top= MyPromptTop;
            Left= MyPromptLeft;
            // 设置文本和按钮内容啥的
            Title = MainWindow._title;
            messageText.Text = MainWindow._messageText;
            int buttonNumber = 0;
            List<string> buttonTextList = new List<string>();
            if (MainWindow._buttonTextList != null)
            {
                buttonNumber = MainWindow._buttonTextList.Count;
                buttonTextList = MainWindow._buttonTextList;
            }
            for (int i = 0; i < buttonNumber; i++)
            {
                Button btn = new Button
                {
                    Content = $"{buttonTextList[i]}",   // 按钮文字
                    Tag = i,                // 存储数值在 Tag 属性里
                    Margin = new Thickness(8), // 设置按钮之间的间距
                };
                btn.Click += Btn_Click_s;
                buttonListUI.Children.Add(btn); // 将按钮添加到 StackPanel 中
            }
        }
        private void Btn_Click_s(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                MainWindow._back=(int)btn.Tag;
                Close();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            string file_fontData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApplyNecessaryConfiguration", "font(MsgWindow).txt");

        }
    }
}
