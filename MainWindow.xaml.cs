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
        //这里说下方案，在主页弄两个frame，一个MainNv是用来作为用户主要的交互导航，另一个MainMini则是用来作为用户的进度条等等播放控制
        public Pages.MainNvPage PageNv_Load= new Pages.MainNvPage();
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            App.settings_ = File.ReadAllText(App.setting_path);
            App.setting_all_list = App.settings_.Split("${@}").ToList();
            List<string> setting_homelist = App.settings_.Split("${@}").ToList();
            setting_homelist_count = setting_homelist.Count;
            //读取和设置屏幕 
            List<string> tall2 = setting_homelist[0].Split(",").ToList();                                                            
            Double happ = Double.Parse(tall2[0]);
            Height = happ;
            Double wapp = Double.Parse(tall2[1]);
            Width = wapp;
            Double lapp = Double.Parse(tall2[2]);
            Left = lapp;
            Double tapp = Double.Parse(tall2[3]);
            Top = tapp;
            //设置主题          
            List<string> theme = App.settings_.Split("${@}").ToList();                                                                        
            if (setting_homelist[1] == "dark")
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            }
            else if (setting_homelist[1] == "light")
            {
                   ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            }
            //设置语言
            if (setting_homelist[2] == "CN") //中
            {
                App.InterfaceLanguage_All = File.ReadAllText(App.Run_Time_Path + "\\Language_S\\CN.txt");
                App.IL_all_list = App.InterfaceLanguage_All.Split("${@}").ToList();
            }
            else if (setting_homelist[2] == "EN")//英文
            {
                App.InterfaceLanguage_All = File.ReadAllText(App.Run_Time_Path + "\\Language_S\\EN.txt");
                App.IL_all_list = App.InterfaceLanguage_All.Split("${@}").ToList();
            }
            if (setting_homelist[2] == "JP")//日语
            {
                App.InterfaceLanguage_All = File.ReadAllText(App.Run_Time_Path + "\\Language_S\\JP.txt");
                App.IL_all_list = App.InterfaceLanguage_All.Split("${@}").ToList();
            }
            if (setting_homelist[2] == "FR")//法语
            {
                App.InterfaceLanguage_All = File.ReadAllText(App.Run_Time_Path + "\\Language_S\\FR.txt");
                App.IL_all_list = App.InterfaceLanguage_All.Split("${@}").ToList();
            }
            Frame_Main_MainNv.Navigate(PageNv_Load);

            //异步读取自定义的列表文件，前面的以后再改了
            Load_IL();
        }
        private List<string> MLO_ = new List<string>();//临时的
        private async void Load_IL()
        {
            await Task.Run(() =>
            {
                string filePath = App.Cover_of_the_List_Path; //请确保文件路径正确
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    string[] parts = content.Split(new string[] { "${@}" }, StringSplitOptions.None);
                    foreach (string part in parts)
                    {
                        MLO_.Add(part);
                    }
                }
                else
                {
                    MessageBox.Show("文件不存在\n\nFile not found\n\nファイルが見つかりません\n\nFichier non trouvé", "!", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
            MLO_.Clear();
        }
        private void _Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 弹窗提示是否确定要退出
            MessageBoxResult f1 = MessageBox.Show(App.IL_all_list[0], App.IL_all_list[0], MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (f1 == MessageBoxResult.OK)
            {
                //文本文件中的第一行内容获取，并且被设置为app的屏幕值
                //保存软件的屏幕位置和大小后退出
                List<string> setting_homelist = File.ReadAllText(App.setting_path).Split("${@}").ToList();
                string less = File.ReadAllText(App.setting_path);
                string App_hwlt = Height + "," + Width + "," + Left + "," + Top;
                setting_homelist[0] = App_hwlt; setting_homelist[8] = "8";
                string Data = string.Join("${@}", setting_homelist);
                StreamWriter sm1 = new StreamWriter(App.setting_path);
                sm1.Write(Data);
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