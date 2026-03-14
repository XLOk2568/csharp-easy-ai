using NVTemperatureControl;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Runtime.InteropServices.JavaScript.JSType;
using File=System.IO.File;
using Path=System.IO.Path;

namespace XLOKProject
{
    /// <summary>
    /// Add_aifuckgame.xaml 的交互逻辑
    /// </summary>
    public partial class Add_aifuckgame : Window
    {
        public static List<string> list_Back_Temp = new List<string>(); //临时存储
        public Add_aifuckgame()
        {
            InitializeComponent();
        }
        private string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApplyNecessaryConfiguration", "ScreenPosition(Add_fuckgame).txt");
        private string temptext = "404 line0 MainWindow loaded";
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "ScreenPosition(MainWindow)_300_300_300_300");
            }
            string content = File.ReadAllText(filePath);
            string[] parts = content
                .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var w))
                Width = w;
            if (double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var h))
                Height = h;
            if (double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var t))
                Top = t;
            if (double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var l))
                Left = l;
            temptext = parts[0];
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            string[] temp_parts = File.ReadAllText(filePath)
                .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            temptext = temp_parts[0];
            string[] saveParts = new string[]
            {
                temptext,
                this.Width.ToString(CultureInfo.InvariantCulture),
                this.Height.ToString(CultureInfo.InvariantCulture),
                this.Top.ToString(CultureInfo.InvariantCulture),
                this.Left.ToString(CultureInfo.InvariantCulture)
            };
            string output = string.Join("_", saveParts);
            File.WriteAllText(filePath, output);
            HomePage2.PCHW = 0;
        }
        // 选择识别区域 第五个主元素 第1开始
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string[] saveParts = new string[]
            {
                "识别范围",
                this.Width.ToString(CultureInfo.InvariantCulture),
                this.Height.ToString(CultureInfo.InvariantCulture),
                this.Top.ToString(CultureInfo.InvariantCulture),
                this.Left.ToString(CultureInfo.InvariantCulture)
            };
            string output = string.Join("_", saveParts);
            list_Back_Temp[5]= output;
        }
        // 位于第一个
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            int value = int.Parse(fpsSet.Text);
            if (value < 1 || value > 1000)
            {
                MessageBox.Show("请输入1-1000之间的整数", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            list_Back_Temp[1] = $"Fps_{value}";
        }
        // 位于第0个
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            float saveR = float.Parse(setR.Text);
           float saveG= float.Parse(setG.Text);
            float saveB = float.Parse(setB.Text);
            if (saveR < 0 || saveR > 1 || saveG < 0 || saveG > 1 || saveB < 0 || saveB > 1)
            {
                MessageBox.Show("请输入0-1之间的数值", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            list_Back_Temp[0] = $"权重_{saveR}_{saveG}_{saveB}";
        }
    }
}
