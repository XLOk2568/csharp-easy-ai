using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
//using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using ScreenCapturerNS;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Windows.Devices.Lights;
using Windows.Storage;
using WindowsAPICodePack.Dialogs;
using WPFtransformer.Pages.child;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using Path = System.IO.Path;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Rectangle = System.Drawing.Rectangle;

namespace NavigationViewExample.Pages
{
    using RGB3Byte = Byte;
    // 常用类型别名
    using RGB3Int = Int32;
    using RGB3Ptr = IntPtr;
    public partial class GuaGua : Page
    {
        public GuaGua()
        {
            InitializeComponent();
            _proc = HookCallback;//鼠标检测                             
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;// 将 DLL 搜索路径设为程序运行目录 调用自己的C++库
            SetDllDirectory(baseDir);
            Unloaded += OnUnloaded;
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
                    MessageBoxResult f1 = MessageBox.Show("确认:\n" + $"要处理的文件路径为{path}" + "\n" + $"要保存的文件路径为{pathKeep}","Are you sure?", MessageBoxButton.OKCancel, MessageBoxImage.Information);
                    if (f1 == MessageBoxResult.OK)
                    {     
                        pathKeep2 = pathKeep; // 保存选择的路径
                        ring.Visibility = Visibility.Visible;
                        await ProcessImagesGpu(path,pathKeep);
                        MessageBox.Show($"CSV 文件已保存到: {pathKeep}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        ring.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }
        private async Task ProcessImagesGpu(string srcFolder, string dstFolder)
        {
            Directory.CreateDirectory(dstFolder);
            var files = Directory.GetFiles(srcFolder, "*.*", SearchOption.AllDirectories).Where(f => new[] { ".png", ".jpg", ".bmp" }.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).ToArray();
            using var context = Context.Create(builder => builder.OpenCL());
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
            ring.Visibility = Visibility.Collapsed;
            informr.Text="请注意：\n" +
                "1. 该功能需要安装 ILGPU 库和相应的 GPU 驱动。\n" +
                "2. 请确保选择的文件夹中包含有效的图像文件（PNG、JPG、BMP）。\n" +
                "3. 输出文件将保存为文本格式，每个通道的数据将分别存储在不同的文件中。\n" +
                "4. 处理完成后，您可以在指定的输出文件夹中找到生成的文本文件。";
        }
        private void Button_Click2(object sender, RoutedEventArgs e)
        {
            if (PCHW == 0)
            {
                _running = false;
                PCHW = 1;
            }
            else
            {
                _running = true;
                PCHW = 0;
            }
            if (_isRunning==false)
            {
                _hookId = SetHook(_proc);
                _isRunning = true;
                StartButton.Content = "Stop";
                PCHW = 1;
            }
            else
            {
                UnhookWindowsHookEx(_hookId);
                _isRunning = false;
                StartButton.Content = "Start";
                PCHW = 0;
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
        private volatile bool _running = false;
        private int CatchLeft = 1;
        private int CatchTop = 1;
        private int CatchWidth = 1;
        private int CatchHeight =1;
        //调用自己的C++库
        [DllImport("kernel32.dll", SetLastError = true)]        // 在应用启动时设置 DLL 搜索路径
        private static extern bool SetDllDirectory(string lpPathName);
        // 原有 P/Invoke 声明
        [DllImport("ScreenCaptureWpfEasy.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool CaptureFrame(int x, int y, int width, int height,out IntPtr buffer,out int outWidth,out int outHeight);
        [DllImport("ScreenCaptureWpfEasy.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FreeBuffer(IntPtr buffer);
        private void ButtonA_Click(object? sender, RoutedEventArgs? e)//  屏幕截获
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Appdata", "data2.txt");
            string content = File.ReadAllText(filePath);
            string[] parts = content.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);            
            CatchLeft = (int)Convert.ToDouble(parts[4]);            
            CatchTop = (int)Convert.ToDouble(parts[3]);
            CatchWidth = (int)Convert.ToDouble(parts[1]);
            CatchHeight = (int)Convert.ToDouble(parts[2]);
            if (_running == false)
            {
                Task.Run(CaptureLoopAsync);
                _running = true;
            }
            else { _running = false; }
        }
        private async Task CaptureLoopAsync()
        {
            while (_running == true && PCHW == 1)
            {
                if (!CaptureFrame(CatchLeft, CatchTop, CatchWidth, CatchHeight, out IntPtr bufPtr, out int width, out int height))
                {
                    MessageBox.Show("Capture failed");
                    return;
                }
                int stride = width * 2;
                int bufSize = height * stride;
                var bmp = BitmapSource.Create(width, height, 96, 96,PixelFormats.Bgr565, null,bufPtr,bufSize, stride);
                bmp.Freeze();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ScreenImage.Source = bmp;
                });
                FreeBuffer(bufPtr);
                await Task.Delay(100);
            }
        }
        private async void Button_Click_1(object sender, RoutedEventArgs e)// 多通道合并
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
            ring.Visibility = Visibility.Visible;
            var rFiles = Directory.GetFiles(path3, "*R.txt");
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
            using var context = Context.Create(builder => builder.OpenCL()); // or .Cuda()
            var device = context.GetPreferredDevice(preferCPU: false);
            using var accelerator = device.CreateAccelerator(context);
            var kernel = accelerator.LoadAutoGroupedStreamKernel<ILGPU.Index1D, ArrayView<byte>, ArrayView<byte>, ArrayView<byte>, ArrayView<byte>>(Conv1x1Kernel);
            // 异步多线程处理每个通道组
            var tasks = fileTriplets.Select(async triplet =>
            {
                var rArr = await ReadTxtToByteArray(triplet.r);
                var gArr = await ReadTxtToByteArray(triplet.g);
                var bArr = await ReadTxtToByteArray(triplet.b);
                var merged = KernelCom(accelerator, kernel, rArr, gArr, bArr);
                string outPath = Path.Combine(path4, $"{triplet.baseName}Merged.txt");
                int width = GetWidthFromTxt(triplet.r);
                if (width <= 0) width = 1;
                using var sw = new StreamWriter(outPath);
                for (int i = 0; i < merged.Length; i++)
                {
                    sw.Write(merged[i]);
                    if ((i + 1) % width == 0)sw.WriteLine();
                    else if (i != merged.Length - 1)sw.Write(',');
                }
            }).ToArray();
            await Task.WhenAll(tasks);
            MessageBox.Show("通道合并成功", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            ring.Visibility = Visibility.Collapsed;
        }
        private static byte[] KernelCom(Accelerator accelerator,Action<ILGPU.Index1D, ArrayView<byte>, ArrayView<byte>, ArrayView<byte>, ArrayView<byte>> kernel,byte[] rArr, byte[] gArr, byte[] bArr)// 1*1卷积核 算法
        {      
            int len = Math.Min(rArr.Length, Math.Min(gArr.Length, bArr.Length));  // 取三条通道数组长度的最小值，防止越界  
            using var dR = accelerator.Allocate1D(rArr);                          // 在 GPU 上分配并拷贝 R 通道数据  
            using var dG = accelerator.Allocate1D(gArr);                          // 在 GPU 上分配并拷贝 G 通道数据  
            using var dB = accelerator.Allocate1D(bArr);                          // 在 GPU 上分配并拷贝 B 通道数据  
            using var dOut = accelerator.Allocate1D<byte>(len);                   // 在 GPU 上分配输出数组，用于存放合并后的结果  
            kernel(new ILGPU.Index1D(len), dR.View, dG.View, dB.View, dOut.View);  // 调用 1×1 卷积 kernel，按索引并行计算每个像素的平均值  
            accelerator.Synchronize();                                            // 等待所有 GPU 线程完成计算  
            return dOut.GetAsArray1D();                                           // 将合并结果从 GPU 拷回到 CPU 并返回  
        }
        private static async Task<byte[]> ReadTxtToByteArray(string path)        // 修正 ReadTxtToByteArray，跳过空字符串
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
        private static int GetWidthFromTxt(string path)        // 获取txt宽度（即每行元素数）
        {
            using var sr = new StreamReader(path);
            string? line = sr.ReadLine();
            if (line == null) return 0;
            return line.Split(',').Length;
        }
        public static void Conv1x1Kernel(ILGPU.Index1D index, ArrayView<byte> r, ArrayView<byte> g, ArrayView<byte> b, ArrayView<byte> output)        // ILGPU 1x1卷积核（简单平均）
        {
            int i = index;
            output[i] = (byte)((r[i] + g[i] + b[i]) / 3);
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
        // 截屏 DLL 导入  //处理
        static class NativeMethods
        {
            [DllImport("ScreenCaptureWpfEasy.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern bool CaptureFrame(
                int x, int y, int width, int height,
                out IntPtr pR, out IntPtr pG, out IntPtr pB,
                out int w, out int h);

            [DllImport("ScreenCaptureWpfEasy.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern void FreeBuffer(IntPtr buffer);
        }

        // 模板类
        class TemplateRGB3ToOne
        {
            public byte[] Data = new byte[0];
            public int Width, Height;
        }
        Context _ctx=null!;
        Accelerator _acc=null!;
        Action<Index2D, ArrayView<byte>, ArrayView<byte>, ArrayView<byte>, int,ArrayView<byte>, int, int, ArrayView<float>> _gpuKernel = null!;
        TemplateRGB3ToOne[] _templates = null!;
        bool _runningRGB3ToOne;
        string pathRGBOLD = @"C:\Templates\tpl_233x233.txt;C:\Templates\tpl_100x50.txt";
        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 1. 初始化 ILGPU（不变）
            _ctx = Context.Create(builder => builder.Cuda());
            _acc = _ctx.GetPreferredDevice(false).CreateAccelerator(_ctx);
            _gpuKernel = _acc.LoadAutoGroupedStreamKernel<
                Index2D,
                ArrayView<byte>, ArrayView<byte>, ArrayView<byte>, int,
                ArrayView<byte>, int, int, ArrayView<float>>(ILKernel);
            // 2. 从 pathRGBOLD 加载 .txt 模板
            var templates = new List<TemplateRGB3ToOne>();
            foreach (var file in pathRGBOLD.Split(';'))
            {
                templates.Add(LoadTemplateFromTxt(file));
            }
            _templates = templates.ToArray();

            // 3. 启动循环（不变）
            _runningRGB3ToOne = true;
            await Task.Run(Loop);

        }
        TemplateRGB3ToOne LoadTemplateFromTxt(string path)
        {
            // 文本格式判断
            var fullText = File.ReadAllText(path);
            if (fullText.Contains("\n"))
            {
                // 文本版
                var lines = fullText
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var wh = lines[0]
                    .Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                int w = int.Parse(wh[0]), h = int.Parse(wh[1]);

                var data = lines
                    .Skip(1)
                    .SelectMany(l => l
                        .Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(byte.Parse)
                    .ToArray();

                if (data.Length != w * h)
                    throw new InvalidDataException($"{path} 中数据长度与 {w}×{h} 不符");

                return new TemplateRGB3ToOne { Width = w, Height = h, Data = data };
            }
            else
            {
                // 二进制版
                var data = File.ReadAllBytes(path);
               // 从文件名 tpl_{W}x{H}.txt 提取尺寸
                var name = Path.GetFileNameWithoutExtension(path);
                // e.g. parts = ["tpl","233x233"]
                var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
                var dims = parts[1].Split('x', StringSplitOptions.RemoveEmptyEntries);
                int w = int.Parse(dims[0]), h = int.Parse(dims[1]);
                if (data.Length != w * h)
                    throw new InvalidDataException($"{path} 二进制长度与 {w}×{h} 不符");
                return new TemplateRGB3ToOne { Width = w, Height = h, Data = data };
            }
        }
        void OnUnloaded(object s, RoutedEventArgs e)
        {
            _runningRGB3ToOne = false;
            _acc?.Dispose();
            _ctx?.Dispose();
        }
        async Task Loop()
        {
            while (_runningRGB3ToOne)
            {
                if (!NativeMethods.CaptureFrame(0, 0, 1920, 1080,
                    out var pR, out var pG, out var pB, out int W, out int H))
                {
                    await Task.Delay(100);
                    continue;
                }
                int len = W * H;
                var R = new byte[len];
                var G = new byte[len];
                var B = new byte[len];
                Marshal.Copy(pR, R, 0, len);
                Marshal.Copy(pG, G, 0, len);
                Marshal.Copy(pB, B, 0, len);
                NativeMethods.FreeBuffer(pR);
                NativeMethods.FreeBuffer(pG);
                NativeMethods.FreeBuffer(pB);
                var (x, y, w, h) = KernelRGB3ToOne(R, G, B, W, H, _templates);                // 4. GPU 算法合一，返回最优匹配位置和大小
                Console.WriteLine($"Match at ({x},{y}) size={w}×{h}");                // 这里你可以把 x,y,w,h 传给任何后续逻辑
                await Task.Delay(100);
            }
        }

        // 把所有逻辑合并到一个方法里
        (int X, int Y, int W, int H) KernelRGB3ToOne(
            byte[] R, byte[] G, byte[] B, int W, int H, TemplateRGB3ToOne[] templates)
        {
            // 申请输入缓存
            using var dR = _acc.Allocate1D(R);
            using var dG = _acc.Allocate1D(G);
            using var dB = _acc.Allocate1D(B);

            int bestX = 0, bestY = 0, bestW = 0, bestH = 0;
            float bestScore = float.MaxValue;

            foreach (var tpl in templates)
            {
                int ow = W - tpl.Width + 1;
                int oh = H - tpl.Height + 1;
                using var dTpl = _acc.Allocate1D(tpl.Data);
                using var dScores = _acc.Allocate1D<float>(ow * oh);

                // 调用 GPU Kernel
                _gpuKernel((oh, ow), dR.View, dG.View, dB.View, W,
                           dTpl.View, tpl.Width, tpl.Height, dScores.View);
                _acc.Synchronize();

                var scores = dScores.GetAsArray1D();
                for (int i = 0; i < scores.Length; i++)
                {
                    if (scores[i] < bestScore)
                    {
                        bestScore = scores[i];
                        bestX = i % ow;
                        bestY = i / ow;
                        bestW = tpl.Width;
                        bestH = tpl.Height;
                    }
                }
            }

            return (bestX, bestY, bestW, bestH);
        }

        // GPU 上把 RGB 三通道先算平均再做模板平方差
        static void ILKernel(
            Index2D idx,
            ArrayView<byte> r, ArrayView<byte> g, ArrayView<byte> b, int width,
            ArrayView<byte> tpl, int tw, int th,
            ArrayView<float> scores)
        {
            int y = idx.X, x = idx.Y;
            float sum = 0;
            for (int dy = 0; dy < th; dy++)
            {
                int baseBig = (y + dy) * width + x;
                int baseTpl = dy * tw;
                for (int dx = 0; dx < tw; dx++)
                {
                    byte avg = (byte)((r[baseBig + dx] + g[baseBig + dx] + b[baseBig + dx]) / 3);
                    float diff = avg - tpl[baseTpl + dx];
                    sum += diff * diff;
                }
            }
            scores[y * (width - tw + 1) + x] = sum;
        }
    }
}
