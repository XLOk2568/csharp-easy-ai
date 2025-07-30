using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Runtime.ConstrainedExecution;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Media.Protection.PlayReady;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace NavigationViewExample.Pages
{
    /// <summary>
    /// Interaction logic for AppsPage.xaml
    /// </summary>
    public partial class AboutPage : Page
    {
        public AboutPage()
        {
            InitializeComponent();
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            meag.Text = "本软件项目开源:\nhttps://github.com/XLOk2568/csharp-easy-ai\n当前软件版本:\n0.0.1.4\nCopyright  ©2024-2025  @XiaLiang\n当前版本支持的语言:中\n任何违法行为均与软件本作者无关!!!\n堆栈追踪：\n" + Environment.StackTrace;
            ring.Visibility = Visibility.Collapsed;
        }
        private static string upm = "正在获取更新信息，请稍后...";
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            ring.Visibility = Visibility.Visible;
            await up();
            List<string> upmlist = upm.Split(",").ToList();
            ring.Visibility = Visibility.Collapsed;
            string result = "";
            int current2 = int.Parse("12");
            int target2 = int.Parse($"{upmlist[1]}");
            if (current2 < target2)
            {
                result=$"发现新版本:{target2}";
                // 在某个事件或方法里调用
                string url = "https://github.com/XLOk2568/csharp-easy-ai";
                Process.Start(url);
            }
            else if(current2>target2)
            {
                result = $"当前版本大于所公布的最新版本,此版本可能是测试版:{target2}";
            }
            else
            {
                result = $"当前为最新版本:{target2}";
            }
            MessageBox.Show("信息:\n" + $"{result}\n{upmlist[3]}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        public static async Task<string> up()
        {
            using var client = new HttpClient();
            string url = "https://raw.githubusercontent.com/XLOk2568/csharp-easy-ai/main/Update.txt";
            client.DefaultRequestHeaders.UserAgent.ParseAdd("request");
            upm = await client.GetStringAsync(url);
            return upm;
        }
    }
}
