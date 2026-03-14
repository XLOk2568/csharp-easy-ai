
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
using File=System.IO.File;
using Path=System.IO.Path;

namespace NVTemperatureControl
{
    /// <summary>
    /// AifuckGame.xaml 的交互逻辑
    /// </summary>
    public partial class AifuckGame : Page
    {
        public AifuckGame()
        {
            InitializeComponent();
        }
        // 存储配置信息的
        private List<string> partsTemp = new List<string>(); //总的
        List<string> partsChildrenTemp = new List<string>(); //配置的配置:)
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            lineupUI.Children.Clear();
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AiFuckGamelineUp.txt");
            string filePath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DefaultImageMatrices");

            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "权重_0.6_0.8_0.9@@Fps_30@@" + filePath2 + "@@水平竖直_10_20@@默认配置@@识别范围_100_100_100_100" );
            }

            string content = File.ReadAllText(filePath);
            partsTemp = new List<string>(
                content.Split(new string[] { "@lineup@" }, StringSplitOptions.None)
            );

            // 添加按钮
            int partsTempCount=partsTemp.Count;
            // 循环处理
            for (int i = 0; i < partsTempCount; i++)
            {
                // 获取每个配置的名字的前一步骤，分割字符串
                partsChildrenTemp = new List<string>(
                    partsTemp[i].Split(new string[] { "@@" }, StringSplitOptions.None)
                );
                //按钮
                Button btn = new Button
                {
                    Content = $"{partsChildrenTemp[4]}",   // 按钮文字
                    Tag = i,                // 存储数值在 Tag 属性里
                    Margin = new Thickness(0, 8, 0, 0),
                    Height=42,Width=240,
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };
                btn.Click += Btn_Click_s;

                //右键菜单
                ContextMenu menu = new ContextMenu();
                MenuItem item = new MenuItem { Header = $"删除配置:\n{partsChildrenTemp[4]}" };
                item.Click += (s, e) =>
                {
                    MessageBox.Show($"右键菜单点击，数值是 {btn.Tag}");
                    MessageBoxResult f1 = MessageBox.Show("确定删除该配置吗?\n删除后无法恢复", "ques", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                    if (f1 == MessageBoxResult.OK)
                    {
                        partsTemp.RemoveAt(i);
                        string contentTemp = string.Join("@lineup@", partsTemp);
                        File.WriteAllText(filePath, contentTemp);
                    }
                };
                menu.Items.Add(item);
                btn.ContextMenu = menu;

                lineupUI.Children.Add(btn);
            }
        }
        private void Btn_Click_s(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                int value = (int)btn.Tag; // 取出按钮的数值
                MessageBox.Show($"单机\n按钮{value}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

            string sb = "";
            MessageBox.Show(sb.ToString(), "CUDA 设备信息");
        }

        //添加配置
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (HomePage2.PCHW == 0)
            {
                HomePage2.PCHW = 1;
                var newWindow = new Add_aifuckgame();
                newWindow.Show();
            }
        }
    }
}
