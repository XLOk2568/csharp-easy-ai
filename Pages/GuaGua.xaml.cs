using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsAPICodePack.Dialogs;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using Path = System.IO.Path;

namespace NavigationViewExample.Pages
{
    /// <summary>
    /// Interaction logic for HomePage.xaml
    /// </summary>
    public partial class GuaGua : Page
    {
        // 私有字段：3 帧 Bitmap 缓冲、当前索引、定时器、屏幕尺寸
        private const int BufferSize = 3;
        private readonly Bitmap[] _bitmaps = new Bitmap[BufferSize];
        private int _currentIndex;
        private readonly int _screenWidth;
        private readonly int _screenHeight;
        private DispatcherTimer? _timer;// 将 _timer 字段声明为可为 null
        public GuaGua()
        {
            InitializeComponent();
            _proc = HookCallback;//鼠标检测                       
            _screenWidth = (int)SystemParameters.PrimaryScreenWidth;// 获取主屏幕尺寸
            _screenHeight = (int)SystemParameters.PrimaryScreenHeight;
        }
        private string pathKeep2 = ""; // 用于存储选择的保存路径
        private async  void Button_Click(object sender, RoutedEventArgs e)
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
                    MessageBoxResult f1 = MessageBox.Show("确认:\n" + $"要处理的文件路径为{path}" + "\n" + $"要保存的文件路径为{pathKeep}", 
                        "Are you sure?", 
                        MessageBoxButton.OKCancel, 
                        MessageBoxImage.Information);
                    if (f1 == MessageBoxResult.OK)
                    {     
                        pathKeep2 = pathKeep; // 保存选择的路径
                        await ProcessImagesGpu(path,pathKeep);
                        MessageBox.Show($"CSV 文件已保存到: {pathKeep}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }
        // 替换所有 ILGPU.Index2 为 ILGPU.Index2D
        // 替换 ProcessImagesGpu 方法中的 Bitmap、Rectangle、ImageLockMode、PixelFormat、Index2 用法
        private async Task ProcessImagesGpu(string srcFolder, string dstFolder)
        {
            Directory.CreateDirectory(dstFolder);
            var files = Directory.GetFiles(srcFolder, "*.*", SearchOption.AllDirectories).Where(f => new[] { ".png", ".jpg", ".bmp" }.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).ToArray();
            using var context = Context.Create(builder => builder.Cuda());
            var device = context.GetPreferredDevice(preferCPU: false);
            using var accelerator = device.CreateAccelerator(context);// 修正为 Index2D
            var kernel = accelerator.LoadAutoGroupedStreamKernel<ILGPU.Index2D, ArrayView<byte>,ArrayView<byte>,ArrayView<byte>,ArrayView<byte>,int,int>(ExtractKernel);
            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    using var bmp = new Bitmap(file);
                    int width = bmp.Width;
                    int height = bmp.Height;
                    var rect = new System.Drawing.Rectangle(0, 0, width, height);
                    var data = bmp.LockBits(
                        rect,
                        System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    var pixelBytes = new byte[Math.Abs(data.Stride) * height];
                    Marshal.Copy(data.Scan0, pixelBytes, 0, pixelBytes.Length);
                    bmp.UnlockBits(data);
                    using var dPixels = accelerator.Allocate1D(pixelBytes);
                    using var dR = accelerator.Allocate1D<byte>(width * height);
                    using var dG = accelerator.Allocate1D<byte>(width * height);
                    using var dB = accelerator.Allocate1D<byte>(width * height);
                    kernel(
                        new ILGPU.Index2D(width, height), // 修正为 Index2D
                        dPixels.View,
                        dR.View,
                        dG.View,
                        dB.View,
                        width,
                        height
                    );
                    accelerator.Synchronize();
                    var rArr = dR.GetAsArray1D();
                    var gArr = dG.GetAsArray1D();
                    var bArr = dB.GetAsArray1D();
                    string name = Path.GetFileNameWithoutExtension(file);
                    var channels = new[]
                    {
                        ("R", rArr),
                        ("G", gArr),
                        ("B", bArr)
                    };
                    foreach (var (tag, arr) in channels)
                    {
                        string outPath = Path.Combine(dstFolder, $"{name}{tag}.txt");
                        using var sw = new StreamWriter(outPath);
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                sw.Write(arr[y * width + x]);
                                if (x < width - 1) sw.Write(',');
                            }
                            sw.WriteLine();
                        }
                    }
                }
            });
        }
        // 替换 ExtractKernel 方法签名中的 Index2
        public static void ExtractKernel(ILGPU.Index2D index,ArrayView<byte> pixelData,ArrayView<byte> rView,ArrayView<byte> gView,ArrayView<byte> bView,int width,int height)
        {
            int x = index.X, y = index.Y;
            if (x >= width || y >= height) return;
            int baseIdx = (y * width + x) * 4;  // 格式：B, G, R, A
            bView[y * width + x] = pixelData[baseIdx + 0];
            gView[y * width + x] = pixelData[baseIdx + 1];
            rView[y * width + x] = pixelData[baseIdx + 2];
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            informr.Text="请注意：\n" +
                "1. 该功能需要安装 ILGPU 库和相应的 GPU 驱动。\n" +
                "2. 请确保选择的文件夹中包含有效的图像文件（PNG、JPG、BMP）。\n" +
                "3. 输出文件将保存为文本格式，每个通道的数据将分别存储在不同的文件中。\n" +
                "4. 处理完成后，您可以在指定的输出文件夹中找到生成的文本文件。";
        }

        private void Button_Click2(object sender, RoutedEventArgs e)
        {
            if (!_isRunning)
            {
                _hookId = SetHook(_proc);
                _isRunning = true;
                StartButton.Content = "Stop";
            }
            else
            {
                UnhookWindowsHookEx(_hookId);
                _isRunning = false;
                StartButton.Content = "Start";
            }
        }
        private bool _isRunning = false;// 运行状态标志
        private IntPtr _hookId = IntPtr.Zero;// 钩子句柄，回调
        private readonly LowLevelMouseProc _proc;
        private IntPtr SetHook(LowLevelMouseProc proc)// 2安装全局低级鼠标钩子
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            string moduleName = curModule?.ModuleName ?? string.Empty;
            return SetWindowsHookEx(WH_MOUSE_LL, proc,GetModuleHandle(moduleName),0);
        }
        private long beat = 0;// 记录点击次数191
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)//3钩子回调：捕获 WM_RBUTTONDOWN 则触发 Running 逻辑
        {
            const int WM_RBUTTONDOWN = 0x0204;
            if (nCode >= 0 && wParam == (IntPtr)WM_RBUTTONDOWN)
            {
                beat++;//暂时自定义191
                StartLabel.Content= $"Running: {beat} times";
                ButtonA_Click(null, null);//调用截图方法
                //Application.Current.Dispatcher.Invoke(…);//切换到UI线程
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
                                                                                                           //屏幕截获
        private async void ButtonA_Click(object? sender, RoutedEventArgs? e)
        {
            if (_timer == null)
            {
                // 第一次点击：创建 4 帧缓冲
                for (int i = 0; i < BufferSize; i++)
                {
                    _bitmaps[i] = new Bitmap(
                        _screenWidth,
                        _screenHeight,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                }

                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                _timer.Tick += async (s, args) =>
                {
                    var bmp = _bitmaps[_currentIndex];

                    // 1. 截图到 Bitmap
                    await Task.Run(() =>
                    {
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(
                                0, 0,
                                0, 0,
                                new System.Drawing.Size(_screenWidth, _screenHeight),
                                CopyPixelOperation.SourceCopy);
                        }
                    });
                    // 2. 转成 WPF 可显示的 BitmapSource
                    IntPtr hBmp = bmp.GetHbitmap();
                    try
                    {
                        var src = Imaging.CreateBitmapSourceFromHBitmap(
                            hBmp,
                            IntPtr.Zero,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        ScreenImage.Source = src;
                    }
                    finally
                    {
                        DeleteObject(hBmp);
                    }
                    // 3. 循环复用下一个缓冲
                    _currentIndex = (_currentIndex + 1) % BufferSize;
                };
                _timer.Start();
                await Task.CompletedTask; // 添加 await，避免 CS1998 警告
            }
            else
            {
                _timer.Stop();
                _timer = null;
                // 停止后，显示最后一帧
                var lastBmp = _bitmaps[(_currentIndex + BufferSize - 1) % BufferSize];
                if (lastBmp != null)
                {
                    IntPtr hBmp = lastBmp.GetHbitmap();
                    try
                    {
                        var src = Imaging.CreateBitmapSourceFromHBitmap(
                            hBmp,
                            IntPtr.Zero,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        ScreenImage.Source = src;
                    }
                    finally
                    {
                        DeleteObject(hBmp);
                    }
                }
                await Task.CompletedTask; // 添加 await，避免 CS1998 警告
            }
        }
        // GDI 对象释放
        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            string path3 = ""; // 用于存储输入路径
            string path4 = ""; // 用于存储输出路径
            var dialog = new CommonOpenFileDialog { IsFolderPicker = true, Title = "选择输出文件夹" };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                path3 = dialog.FileName;
            else return;
            var dialog2 = new CommonOpenFileDialog { IsFolderPicker = true, Title = "选择输出文件夹" };
            if (dialog2.ShowDialog() == CommonFileDialogResult.Ok)
                path4 = dialog2.FileName;
            else return;
            try
            {
                var rFiles = Directory.GetFiles(path3, "*R.txt", SearchOption.TopDirectoryOnly);
                var gFiles = Directory.GetFiles(path3, "*G.txt", SearchOption.TopDirectoryOnly);
                var bFiles = Directory.GetFiles(path3, "*B.txt", SearchOption.TopDirectoryOnly);
                var fileTriplets = rFiles.Select(r =>
                {
                    string name = Path.GetFileNameWithoutExtension(r);
                    string baseName = name.EndsWith("R") ? name[..^1] : name;
                    string g = Path.Combine(path3, $"{baseName}G.txt");
                    string b = Path.Combine(path3, $"{baseName}B.txt");
                    return (baseName, r, g, b);
                }).Where(t => File.Exists(t.g) && File.Exists(t.b)).ToList();
                if (fileTriplets.Count == 0)
                {
                    MessageBox.Show("未找到有效的R/G/B通道文件组。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var tasks = fileTriplets.Select(async triplet =>
                {
                    byte[] rArr = await ReadTxtToByteArray(triplet.r);
                    byte[] gArr = await ReadTxtToByteArray(triplet.g);
                    byte[] bArr = await ReadTxtToByteArray(triplet.b);
                    return (triplet.baseName, rArr, gArr, bArr);
                }).ToArray();
                var allData = await Task.WhenAll(tasks);
                int CPUTEMP23 = 200;
                using var context = Context.Create(builder => builder.OpenCL());
                var device = context.GetPreferredDevice(preferCPU: false);
                using var accelerator = device.CreateAccelerator(context);
                var kernel = accelerator.LoadAutoGroupedStreamKernel<ILGPU.Index1D, ArrayView<byte>, ArrayView<byte>, ArrayView<byte>, ArrayView<byte>>(Conv1x1Kernel);
                // 替换 Button_Click_1 方法中的 GPU 结果拷贝部分，确保不会越界
                foreach (var (baseName, rArr, gArr, bArr) in allData)
                {
                    int len = Math.Min(rArr.Length, Math.Min(gArr.Length, bArr.Length));
                    if (len == 0)
                        continue;
                    byte[] merged = new byte[len];
                    int cpuCount = Math.Min(CPUTEMP23, len);
                    for (int i = 0; i < cpuCount; i++)
                    {
                        merged[i] = (byte)((rArr[i] + gArr[i] + bArr[i]) / 3);
                    }
                    if (len > cpuCount)
                    {
                        int gpuLen = len - cpuCount;
                        using var dR = accelerator.Allocate1D(rArr);
                        using var dG = accelerator.Allocate1D(gArr);
                        using var dB = accelerator.Allocate1D(bArr);
                        using var dOut = accelerator.Allocate1D<byte>(len);
                        kernel(
                            new ILGPU.Index1D(gpuLen),
                            dR.View.SubView(cpuCount, gpuLen),
                            dG.View.SubView(cpuCount, gpuLen),
                            dB.View.SubView(cpuCount, gpuLen),
                            dOut.View.SubView(cpuCount, gpuLen)
                        );
                        accelerator.Synchronize();
                        var gpuResult = dOut.GetAsArray1D();
                        int copyLen = Math.Min(gpuResult.Length, merged.Length - cpuCount);
                        Array.Copy(gpuResult, 0, merged, cpuCount, copyLen);
                    }
                    string outPath = Path.Combine(path4, $"{baseName}Merged.txt");
                    int width = GetWidthFromTxt(fileTriplets.First(t => t.baseName == baseName).r);
                    if (width <= 0) width = 1;
                    using var sw = new StreamWriter(outPath);
                    for (int i = 0; i < merged.Length; i++)
                    {
                        sw.Write(merged[i]);
                        if ((i + 1) % width == 0)
                            sw.WriteLine();
                        else if (i != merged.Length - 1)
                            sw.Write(',');
                    }
                }
                MessageBox.Show("通道合并完成！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // 修正 ReadTxtToByteArray，跳过空字符串
        private static async Task<byte[]> ReadTxtToByteArray(string path)
        {
            var lines = await File.ReadAllLinesAsync(path);
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
        // 获取txt宽度（即每行元素数）
        private static int GetWidthFromTxt(string path)
        {
            using var sr = new StreamReader(path);
            string? line = sr.ReadLine();
            if (line == null) return 0;
            return line.Split(',').Length;
        }
        // ILGPU 1x1卷积核（简单平均）
        public static void Conv1x1Kernel(ILGPU.Index1D index, ArrayView<byte> r, ArrayView<byte> g, ArrayView<byte> b, ArrayView<byte> output)
        {
            int i = index;
            output[i] = (byte)((r[i] + g[i] + b[i]) / 3);
        }
    }
}
