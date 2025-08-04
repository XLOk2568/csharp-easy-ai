using iNKORE.UI.WPF.Modern.Controls;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsAPICodePack.Dialogs;
using WPFtransformer.Pages.child;
using FileIO= System.IO.File;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using Path = System.IO.Path;

namespace NavigationViewExample.Pages
{

    public partial class GuaGua : Page
    {
        public GuaGua()
        {
            InitializeComponent();
            _proc = HookCallback;//鼠标检测                             
        }
        private string pathKeep2 = ""; // 用于存储选择的保存路径
        private List<int> listShiShiiBuHuo=new List<int>();//     转成RGB矩阵的 中间 数据
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            string path = "";
            string pathKeep = "";
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                path = dialog.FileName;
                var dialog2 = new CommonOpenFileDialog //以下是选择保存的路径
                {
                    IsFolderPicker = true
                };
                if (dialog2.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    pathKeep = dialog2.FileName;
                    MessageBoxResult f1 = MessageBox.Show("确认:\n" + $"要处理的文件路径为{path}" + "\n" + $"要保存的文件路径为{pathKeep}", "Are you sure?", MessageBoxButton.OKCancel, MessageBoxImage.Information);
                    if (f1 == MessageBoxResult.OK)
                    {
                        pathKeep2 = pathKeep; // 保存选择的路径
                        ring.Visibility = Visibility.Visible;
                        await ImageFeatures(path, pathKeep);
                        MessageBox.Show($"CSV 文件已保存到: {pathKeep}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        ring.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ring.Visibility = Visibility.Collapsed;
            informr.Text = "请注意：\n1. 该功能需要安装 ILGPU 库和相应的 GPU 驱动。\n2. 请确保选择的文件夹中包含有效的图像文件（PNG、JPG、BMP）。\n3. 输出文件将保存为文本格式，每个通道的数据将分别存储在不同的文件中。\n4. 处理完成后，您可以在指定的输出文件夹中找到生成的文本文件。";
            OffOnLabel.Content = "是否显示处理\n的画面预览";
            UserCLabel.Content = "选择自己的特征文件夹\n(非必要请保持默认)";
            string filePathImage = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Appdata", "ImageOnOrOff.txt");
            if (!System.IO.File.Exists(filePathImage))
            {
                FileIO.WriteAllText(filePathImage, "On");
                OffOn.Source = null;
                OffOn.Source = new BitmapImage(new Uri("/PNG/On.png", UriKind.Relative));
            }
            else
            {
                string content = "On";
                content = FileIO.ReadAllText(filePathImage);
                if (content == "On")
                {
                    Sriof = 1;
                    OffOn.Source = null;
                    OffOn.Source = new BitmapImage(new Uri("/PNG/On.png", UriKind.Relative));
                }
                else if (content == "Off")
                {
                    Sriof = 0;
                    OffOn.Source = null;
                    OffOn.Source = new BitmapImage(new Uri("/PNG/Off.png", UriKind.Relative));
                }
                else { MessageBox.Show($"文件:\n{filePathImage}\n的内容格式不正确，已经取消了你的操作"); }
            }
            FeaturesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Appdata", "FeaturesFolder.txt");//  这是存储着有 总 特征文件夹 信息 的，字符串 的文件  //这里先拿来存储看看  文件是否存在  ，之后再写入具体的文件夹路径
            if (!FileIO.Exists(FeaturesFolder))
            {
                FileIO.WriteAllText(FeaturesFolder, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FeaturesFolder"));
                FeaturesFolder = FileIO.ReadAllText(FeaturesFolder);
            }
            else
            {
                FeaturesFolder = FileIO.ReadAllText(FeaturesFolder);
            }
            if (!System.IO.Directory.Exists(FeaturesFolder))
            {
                MessageBox.Show("特征路径不存在!!!", "!", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        [DllImport("ScreenCaptureWpfEasy.dll", CallingConvention = CallingConvention.Cdecl)]// 原有 P/Invoke 声明
        private static extern bool CaptureFrame(int x, int y, int width, int height, out IntPtr buffer, out int outWidth, out int outHeight);
        [DllImport("ScreenCaptureWpfEasy.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FreeBuffer(IntPtr buffer);
        private int CatchLeft = 100;
        private int CatchTop = 300;
        private int CatchWidth = 900;
        private int CatchHeight = 600;
        private bool _running =false; // 控制捕获循环的标志
        private  void Button_Click2(object sender, RoutedEventArgs e)
        {
            if (PCHW == 0)
            {
                PCHW = 1;//下面是初始化 捕获屏幕代码
                _running=true;
                Task.Run(CaptureLoopAsync);
                _hookId = SetHook(_proc);
                StartButton.Content = "Stop";
            }
            else
            {
                _running = false; // 停止捕获循环
                UnhookWindowsHookEx(_hookId);
                StartButton.Content = "Start";
                PCHW = 0;
            }
        }
        private async Task CaptureLoopAsync()
        {
            while (_running == true)
            {
                if (!CaptureFrame(CatchLeft, CatchTop, CatchWidth, CatchHeight, out IntPtr bufPtr, out int width, out int height))
                {
                    MessageBox.Show("Capture failed");
                    return;
                }
                int stride = width * 2;
                int bufSize = height * stride;
                var RGBMatrix = new int[height,width];
                byte[] rawData = new byte[bufSize];
                Marshal.Copy(bufPtr, rawData, 0, bufSize);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * stride + x * 2;
                        ushort pixel565 = BitConverter.ToUInt16(rawData, index);
                        int r = (pixel565 >> 11) & 0x1F;
                        int g = (pixel565 >> 5) & 0x3F;
                        int b = pixel565 & 0x1F;
                        r = (r << 3) | (r >> 2);
                        g = (g << 2) | (g >> 4);
                        b = (b << 3) | (b >> 2);
                        RGBMatrix[y, x] = (r << 16) | (g << 8) | b;
                    }
                }  //获取实时 RGB矩阵
                if (Sriof == 1)
                {
                    var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr565, null, bufPtr, bufSize, stride);
                    bmp.Freeze();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SrI.Source = bmp;
                    });
                }
                FreeBuffer(bufPtr);
                await Task.Delay(100);//循环间隔
            }
        }

        private IntPtr _hookId = IntPtr.Zero;// 钩子句柄，回调
        private readonly LowLevelMouseProc _proc;
        private IntPtr SetHook(LowLevelMouseProc proc)// 2安装全局低级鼠标钩子
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            string moduleName = curModule?.ModuleName ?? string.Empty;
            return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(moduleName), 0);
        }
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)//3钩子回调：捕获 WM_RBUTTONDOWN 则触发 Running 逻辑
        {
            const int WM_RBUTTONDOWN = 0x0204;
            if (nCode >= 0 && wParam == (IntPtr)WM_RBUTTONDOWN)
            {
                MessageBox.Show("右键点击事件触发！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
        //PInvoke定义
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        private const int WH_MOUSE_LL = 14;
        private string CharacteristicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Feature");//特征存储路径
        //调用自己的C++库 捕获屏幕的
        private static async Task<byte[]> ReadTxtToByteArray(string path)        // 修正 ReadTxtToByteArray，跳过空字符串
        {
            var lines = await FileIO.ReadAllLinesAsync(path);
            var list = new List<byte>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                foreach (var p in parts)
                    if (!string.IsNullOrWhiteSpace(p) && byte.TryParse(p, out var v)) list.Add(v);
            }
            return list.ToArray();
        }
        private static int GetWidthFromTxt(string path)        // 获取txt宽度（即每行元素数）
        {
            using var sr = new StreamReader(path);
            string? line = sr.ReadLine();
            if (line == null) return 0;
            return line.Split(',').Length;
        }

        public static int PCHW = 0;// 打开选择识别的窗口，这个值0表示可以，1表示已经打开(防止重复开启)
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (PCHW == 0)
            {
                PCHW = 1;
                var gauguachwWin = new GuaGuaCHW();
                gauguachwWin.Show();
            }
        }
        //  单独显示 捕获区域 预览的开关
        private int Sriof = 0;
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if (PCHW == 0)
            {
                string filePathImage = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Appdata", "ImageOnOrOff.txt");
                string content = "On";
                if (!FileIO.Exists(filePathImage))
                {
                    FileIO.WriteAllText(filePathImage, "On");Sriof = 1;
                    OffOn.Source = null;
                    OffOn.Source = new BitmapImage(new Uri("/PNG/On.png", UriKind.Relative));
                }
                else
                {
                    content = FileIO.ReadAllText(filePathImage);
                    if (content == "On")
                    {
                        FileIO.WriteAllText(filePathImage, "Off");Sriof = 0;
                        OffOn.Source = null;
                        OffOn.Source = new BitmapImage(new Uri("/PNG/Off.png", UriKind.Relative));
                    }
                    else if (content == "Off")
                    {
                        FileIO.WriteAllText(filePathImage, "On");Sriof = 1;
                        OffOn.Source = null;
                        OffOn.Source = new BitmapImage(new Uri("/PNG/On.png", UriKind.Relative));
                    }
                    else { MessageBox.Show($"文件:\n{filePathImage}\n的内容格式不正确，已经取消了你的操作"); }
                }
            }
        }
        //  CLMatch.dll  networkhalfsize 
        [DllImport("CLMatch.dll", EntryPoint = "networkhalfsize", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NetworkHalfSize(int[] bigImg, int bigH, int bigW,int[] tplImg, int tplH, int tplW,[Out] float[] scoreBuf,[Out] int[] infoBuf);
        // ②  WPF 按钮事件：仅演示 networkhalfsize 的一次调用
        private async void RunHalfSlide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                /* ------------------------------------------
                 * 1) 造两张“假”图：待检测图 1000×8000、模板 300×200
                 *    （实际项目中请替换为真实像素）
                 * ------------------------------------------ */
                int bigH = 1000, bigW = 8000;    // 待检测大图
                int tplH = 300, tplW = 200;    // 特征模板
                int[] bigImg = new int[bigH * bigW];   // 灰度 int32
                int[] tplImg = new int[tplH * tplW];
                /* ------------------------------------------
                 * 2) 计算缓冲区大小
                 *    步幅 = 模板尺寸 / 2   → rows × cols
                 * ------------------------------------------ */
                int strideY = tplH / 2;          // 300 / 2 = 150
                int strideX = tplW / 2;          // 200 / 2 = 100
                int rows = (bigH - tplH) / strideY + 1; // (1000-300)/150 + 1 = 5
                int cols = (bigW - tplW) / strideX + 1; // (8000-200)/100 + 1 = 79
                int total = rows * cols;                // 5 × 79 = 395
                float[] scoreBuf = new float[total];    // 每个滑窗一个分数
                int[] infoBuf = new int[total * 2];  // 行、列各 1 个 int
                int ret = await Task.Run(() =>
                NetworkHalfSize(bigImg, bigH, bigW,tplImg, tplH, tplW,scoreBuf, infoBuf));
                if (ret >= 0)
                {
                    MessageBox.Show($"networkhalfsize OK，窗口总数 = {total}，Top-1 Score = {scoreBuf[0]:F3}");
                }
                else
                {
                    MessageBox.Show($"networkhalfsize Failed，err = {ret}", "Error");
                }
            }
            catch (DllNotFoundException ex)
            {
                MessageBox.Show(ex.Message, "DLL Missing");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Unhandled");
            }
        }
        private string FeaturesFolder = null!;
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            var folderDialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };
            if (folderDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var files = Directory.GetFiles(folderDialog.FileName, "*.txt");
                //pathRGBOLD = string.Join(";", files);        //  450和457 同样使用  pathRGBOLD
                string FFTPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Appdata", "FeaturesFolder.txt");
                FileIO.WriteAllText(FFTPath, folderDialog.FileName);
                FeaturesFolder = FileIO.ReadAllText(FFTPath);
            }
        }
        // 使用自己的opencl库 CLMath
        private const string DllName = "CLMath.dll";
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double CL_Add(double[] arr, int len, int deviceIndex);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double CL_Sub(double[] arr, int len, int deviceIndex);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double CL_Mul(double[] arr, int len, int deviceIndex);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double CL_Div(double[] arr, int len, int deviceIndex);
        // 按钮 “Run CL Operations” 异步执行 1+2+3, 2*4, 3-4, 4/2
        private async void BtnCompute_Click(object sender, RoutedEventArgs e)
        {
            const int device = 0;
            var addTask = Task.Run(() => CL_Add(new[] { 1.0, 2.0, 3.0 }, 3, device));
            var mulTask = Task.Run(() => CL_Mul(new[] { 2.0, 4.0 }, 2, device));
            var subTask = Task.Run(() => CL_Sub(new[] { 3.0, 4.0 }, 2, device));
            var divTask = Task.Run(() => CL_Div(new[] { 4.0, 2.0 }, 2, device));
            await Task.WhenAll(addTask, mulTask, subTask, divTask);
            MessageBox.Show($"1 + 2 + 3 = {addTask.Result}", "CL Add");
            MessageBox.Show($"2 × 4     = {mulTask.Result}", "CL Mul");
            MessageBox.Show($"3 − 4     = {subTask.Result}", "CL Sub");
            MessageBox.Show($"4 ÷ 2     = {divTask.Result}", "CL Div");
        }
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetDeviceNamesCount();
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetDeviceNames", CharSet = CharSet.Ansi)]
        private static extern void GetDeviceNames(int index, StringBuilder buf, int bufSize);

        [DllImport("CLMath.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisposeOpenCL();
        private async void BtnQuery_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() =>
            {
                int count = GetDeviceNamesCount();
                var sb = new StringBuilder();
                var buf = new StringBuilder(256);

                for (int i = 0; i < count; i++)
                {
                    buf.Clear();
                    GetDeviceNames(i, buf, buf.Capacity);
                    sb.AppendLine($"Device {i}: {buf}");
                }
                // 切回 UI 线程弹窗
                Dispatcher.Invoke(() =>
                MessageBox.Show(sb.ToString(), "Available OpenCL Devices"));
            });
            DisposeOpenCL(); // 释放 OpenCL 资源
        }
        private async void Button_Click_5(object sender, RoutedEventArgs e)
        {
            const int device = 0;
            var addTask = Task.Run(() => CL_Add(new[] { 1.0, 2.0, 3.0, 6663, 0.2 }, 5, device));
            var mulTask = Task.Run(() => CL_Mul(new[] { 2.0, 4.0 }, 2, device));
            var subTask = Task.Run(() => CL_Sub(new[] { 3.0, 4.0 }, 2, device));
            var divTask = Task.Run(() => CL_Div(new[] { 4.0, 2.0 }, 2, device));
            await Task.WhenAll(mulTask);
            MessageBox.Show($"1 + 2 + 3 = {mulTask.Result}", "CL Add");
        }
        // 示例的代码 c# 实现滑动窗口模板匹配
        public static void SlideOnce(int[,] newP,int[,] oldP,int times,List<float> scoreList,List<(int left, int top, int w, int h)> scoreInfoList)
        {
            //--- 0) 尺寸常量 -------------------------------------------------------
            int bigH = newP.GetLength(0);  //这个是  检测图  的尺寸
            int bigW = newP.GetLength(1);
            int winH = oldP.GetLength(0);   //这个是  模板  的尺寸
            int winW = oldP.GetLength(1);
            //--- 1) 由 times 推 stride --------------------------------------------//times控制滑动次数
            int rows = (int)Math.Ceiling(Math.Sqrt(times));
            int cols = (int)Math.Ceiling((double)times / rows);
            int strideY = (rows <= 1)? bigH - winH : (bigH - winH) / (rows - 1);
            int strideX = (cols <= 1)? bigW - winW: (bigW - winW) / (cols - 1);
            //--- 2) 预计算 ---------------------------------------------------------
            int winN = winH * winW;//  模板的像素数量
            int maxPix = 255;
            int maxSAD = maxPix * winN;
            //--- 3) 核心单 for -----------------------------------------------------
            int total = rows * cols;
            for (int t = 0; t < total; t++)
            {
                // 3-A) 左上角
                int rowIdx = t / cols;
                int colIdx = t - rowIdx * cols;
                int y0 = rowIdx * strideY;
                int x0 = colIdx * strideX;
                if (y0 + winH > bigH || x0 + winW > bigW)
                {
                    continue;    // 超界窗口丢弃
                }
                // 3-B) 计算 SAD
                int sad = 0;
                int k = 0;
                for (; k < winN; k++)
                {
                    int u = k / winW;
                    int v = k - u * winW;
                    int y = y0 + u;
                    int x = x0 + v;
                    int a = newP[y, x];
                    int b = oldP[u, v];
                    int diff;
                    if (a >= b)
                    {
                        diff = a - b;
                    }
                    else
                    {
                        diff = b - a;
                    }
                    sad += diff;
                }
                // 3-C) 归一化分数
                float score = 1.0f - ((float)sad / maxSAD);
                scoreList.Add(score);
                scoreInfoList.Add((x0, y0, winW, winH));
            }
        }
        // 提取图片的 RGB通道
        public  static async Task  ImageFeatures(string pathDaiShiBie, string pathBaoCunShiBie)
        {
            if (!Directory.Exists(pathBaoCunShiBie))
            {
                Directory.CreateDirectory(pathBaoCunShiBie);
            }
            string[] exts = { ".png", ".jpg", ".jpeg", ".bmp" };
            string[] files = Directory.GetFiles(pathDaiShiBie).Where(f => exts.Contains(Path.GetExtension(f).ToLower())).ToArray();
            await Task.WhenAll(files.Select(file => Task.Run(() =>
            {
                using Bitmap bmp = (Bitmap)System.Drawing.Image.FromFile(file);
                int h = bmp.Height;
                int w = bmp.Width;
                int total = h * w;                 // H×W
                StringBuilder sb = new StringBuilder(total * 8); // 预估容量
                for (int idx = 0; idx < total; idx++)
                {
                    int row = idx / w;
                    int col = idx - row * w;
                    System.Drawing.Color c = bmp.GetPixel(col, row); // System.Drawing.Color
                    int r = c.A == 0 ? 0 : c.R;
                    int g = c.A == 0 ? 0 : c.G;
                    int b = c.A == 0 ? 0 : c.B;
                    sb.Append(r); sb.Append(',');
                    sb.Append(g); sb.Append(',');
                    sb.Append(b);
                    if (col == w - 1)
                    {
                        sb.AppendLine();            // 换行
                    }
                    else
                    {
                        sb.Append(',');             // 逗号
                    }
                }
                string name = Path.GetFileNameWithoutExtension(file);
                string outFile = Path.Combine(pathBaoCunShiBie, name + "Feature.txt");
                File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);            
            })));
        }
        //滑动窗口代码
        [DllImport("CLMatch.dll", EntryPoint = "SlideOnce",CallingConvention = CallingConvention.Cdecl)]
        private static extern int SlideOnceauto(int[] bigImg, int bigH, int bigW, int[] tplImg, int tplH, int tplW, [Out] float[] scoreBuf, [Out] int[] infoBuf);
        [DllImport("CLMatch.dll", EntryPoint = "SlideOnce", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SlideOnce(int[] bigImg, int bigH, int bigW,int[] tplImg, int tplH, int tplW,int times,[Out] float[] scoreBuf,[Out] int[] infoBuf);
        private int CNNnetWork = 0;
        private async void RunSlide_Click_auto(object sender, RoutedEventArgs e)
        {
            if (CNNnetWork == 0)
            {
                CNNnetWork = 1; // 打开滑动窗口识别
                try
                {
                    var FFTList = await Task.Run(() =>
                    Directory.EnumerateFiles(FeaturesFolder, "*.txt", SearchOption.AllDirectories).ToList());
                    for (int i = 0; i < FFTList.Count - 1; i++)
                    {
                        List<int> FFTValues = File.ReadAllText(FFTList[i]).Split(',')
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Select(int.Parse).ToList();
                        int ret = await Task.Run(() => SlideOnceauto(CatchWidth * CatchHeight, bigH, bigW, tplImg, tplH, tplW, scoreBuf, infoBuf));
                        if (ret == 0)
                        {
                            MessageBox.Show($"SlideOnce OK. Top-1 Score = {scoreBuf[0]:F3}");
                        }
                        else
                        {
                            CNNnetWork = 0; // 关闭滑动窗口识别
                            MessageBox.Show($"SlideOnce Failed, err = {ret}", "Error");
                        }
                    }
                }
                catch (DllNotFoundException ex)
                {
                    CNNnetWork = 0; // 关闭滑动窗口识别
                    MessageBox.Show(ex.Message, "DLL Missing");
                }
                catch (Exception ex)
                {
                    CNNnetWork = 0; // 关闭滑动窗口识别
                    MessageBox.Show(ex.ToString(), "Unhandled");
                }
            }
        }
        private async void RunSlide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int times = 1; // 滑动次数
                // 1) 造两张假的图片 —— 实际项目里用真正像素填充
                int bigH = 512, bigW = 512;
                int tplH = 64, tplW = 64;
                int[] bigImg = new int[bigH * bigW];   // 0 填充  待检测图
                int[] tplImg = new int[tplH * tplW];   // 0 填充     特征模板
                // 2) 计算输出缓冲区大小
                //    假设返回每个滑窗一个 score，同时 info 四个 int（例：x,y,w,h）
                float[] scoreBuf = new float[times];
                int[] infoBuf = new int[times * 4];
                int ret = await Task.Run(() =>SlideOnce(bigImg, bigH, bigW,tplImg, tplH, tplW,times: 1,scoreBuf,infoBuf));
                // 4) 检查返回值 & 用结果
                if (ret == 0)
                {
                    MessageBox.Show($"SlideOnce OK. Top-1 Score = {scoreBuf[0]:F3}");
                }
                else
                { //
                  MessageBox.Show($"SlideOnce Failed, err = {ret}", "Error");
                }
            }
            catch (DllNotFoundException ex)
            {
                MessageBox.Show(ex.Message, "DLL Missing");                   
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Unhandled");
            }
        }
    }
}