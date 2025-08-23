using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace NavigationViewExample.Pages
{
    /// <summary>
    /// Interaction logic for AppsPage.xaml
    /// </summary>
    public partial class Set : Page
    {
        public Set()
        {
            InitializeComponent();
        }
        [DllImport("ScreenCaptureWpfEasy.dll", CallingConvention = CallingConvention.Cdecl)]// 原有 P/Invoke 声明
        private static extern bool CaptureFrame(int x, int y, int width, int height, out IntPtr buffer, out int outWidth, out int outHeight);
        [DllImport("ScreenCaptureWpfEasy.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FreeBuffer(IntPtr buffer);
        private bool _running= true;
        int CatchLeft = 0;  
        int CatchTop = 0;
        int CatchWidth = 1920; // 捕获区域宽度
        int CatchHeight = 1080; // 捕获区域高度
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _running = true;
            Task.Run(() => CaptureLoopAsync());
        }
        private async Task CaptureLoopAsync()
        {
            var txtList = FeatureFileHelper.GetFeatureTxtList();
            foreach (var file in txtList)
            {
                Console.WriteLine(file);
            }
        }
        [DllImport("CLMath.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NetWorkUsingV2(  
            int[] tplList, int tplCount, int tplWidth,
            int[] bigList, int bigCount, int bigWidth,
            int[] deviceList, int deviceCount,
            int[] oldRegion, // x,y,w,h
            out int outX, out int outY, out int outW, out int outH, out float outScore);
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string tpltxt = string.Empty;
                string bigtxt = string.Empty;
                string TempFristLine = string.Empty;
                // 选择 tpl.txt
                var openFileDialog = new OpenFileDialog
                {
                    Title = "选择模板文件 tpl.txt",
                    Filter = "文本文件 (*.txt)|*.txt",
                    Multiselect = false
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    tpltxt = openFileDialog.FileName;
                }
                openFileDialog.Title = "选择搜索图文件 big.txt";
                if (openFileDialog.ShowDialog() == true)
                {
                    bigtxt = openFileDialog.FileName;
                }
                List<string> ListTempLineFrist;
                int tplWidth;
                List<int> tpl;
                using (var reader = new StreamReader(tpltxt))
                {
                    TempFristLine = File.ReadAllLines(tpltxt)[0];
                    MessageBox.Show("第一行内容：" + TempFristLine,"模板图");
                    ListTempLineFrist = TempFristLine.Split(',').ToList();            
                    tplWidth = ListTempLineFrist.Count;
                    MessageBox.Show("模板宽度：" + tplWidth,"模板图");
                }
                // 整个文件
                tpl = File.ReadAllText(tpltxt)
                          .Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => int.Parse(s))
                          .ToList();
                // 4. 读取 big.txt
                int bigWidth;
                List<int> big;
                using (var reader = new StreamReader(bigtxt))
                {
                    TempFristLine = File.ReadAllLines(bigtxt)[0];                    
                    MessageBox.Show("第一行内容：" + TempFristLine,"搜索图");
                    ListTempLineFrist = TempFristLine.Split(',').ToList();
                    bigWidth = ListTempLineFrist.Count;
                    MessageBox.Show("模板宽度：" + bigWidth,"搜索图");
                }
                big = File.ReadAllText(bigtxt)
                          .Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => int.Parse(s))
                          .ToList();
                MessageBox.Show($"模板宽度: {tplWidth}, 搜索图宽度: {bigWidth}\n模板像素数: {tpl.Count}, 搜索图像素数: {big.Count}");       
                int[] deviceList = new int[] { 0 };
                // 调用
                var tplArr = tpl.ToArray();
                var bigArr = big.ToArray();
                int deviceCount = deviceList.Length;
                int ok = NetWorkUsingV2(
                    tplArr, tplArr.Length, tplWidth,
                    bigArr, bigArr.Length, bigWidth,
                    deviceList,deviceCount,
                    new int[] { 0, 0, 0, 0 },
                    out int x, out int y, out int w, out int h, out float score);
                if (ok == 0)
                {
                    MessageBox.Show("计算失败：请检查参数、设备索引或 DLL 是否可用。", "deform_slide_v2_k", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string msg = $"最佳匹配位置：\n" +
                             $"- 左上: ({x}, {y})\n" +
                             $"- 尺寸: {w} x {h}\n" +
                             $"- 得分: {score:F4} (0~1)";
                MessageBox.Show(msg, "deform_slide_v2_k 结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (DllNotFoundException)
            {
                MessageBox.Show("未找到 DLL，请确认 YourDllName.dll 在可搜索路径。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (EntryPointNotFoundException)
            {
                MessageBox.Show("未找到导出函数 deform_slide_v2_k，请确认导出名与调用约定。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("运行异常：\n" + ex.Message, "异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
