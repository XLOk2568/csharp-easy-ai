using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
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
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace NavigationViewExample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //setting.txt
        //749,1266,258,123${@}dark${@}CN${@}firsttime,newtime${@}playtime,palyalist
        public int setting_homelist_count = 0;
        public MainWindow()
        {
            InitializeComponent();
        }
        public Pages.MainNvPage PageNv_Load= new Pages.MainNvPage();
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //先检测是否存在屏幕，位置的文件夹
            string folderName = "Appdata";
            string folderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folderName);
            string filePath = System.IO.Path.Combine(folderPath, "data.txt");
            string writeText = "//屏幕_121_222_333_444_";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);                // 获取当前程序运行目录
                File.WriteAllText(filePath, writeText);
                MessageBox.Show("Please restart the software.");
            }
            else if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, writeText);
                MessageBox.Show("Please restart the software.");
            }
            else
            {
                string listText = File.ReadAllText(filePath);
                List<string> setting_homelist = listText.Split("_").ToList();
                Double happ = Double.Parse(setting_homelist[1]);
                Height = happ;
                Double wapp = Double.Parse(setting_homelist[2]);
                Width = wapp;
                Double lapp = Double.Parse(setting_homelist[3]);
                Left = lapp;
                Double tapp = Double.Parse(setting_homelist[4]);
                Top = tapp;
            }
            Frame_Main_MainNv.Navigate(PageNv_Load);
            Frame_Main_MainNv.Navigate(PageNv_Load);
        }
        private void _Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 弹窗提示是否确定要退出
            MessageBoxResult f1 = MessageBox.Show("确定退出吗?", "?", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (f1 == MessageBoxResult.OK)
            {
                //文本文件中的第一行内容获取，并且被设置为app的屏幕值
                //保存软件的屏幕位置和大小后退出
                string folderName = "Appdata";
                string folderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folderName);
                string filePath = System.IO.Path.Combine(folderPath, "data.txt");
                string listText = File.ReadAllText(filePath);
                List<string> setting_homelist = listText.Split("_").ToList();
                string App_hwlt = "//屏幕_" + Height + "_" + Width + "_" + Left + "_" + Top;
                StreamWriter sm1 = new StreamWriter(filePath);
                sm1.Write(App_hwlt);
                sm1.Close();
                Process.GetCurrentProcess().Kill();
            }
            else
            {
                e.Cancel = true;
            }
        }
    }
}