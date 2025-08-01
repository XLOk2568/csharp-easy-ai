using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsAPICodePack.Dialogs;
using WPFtransformer.Pages.child;
using FileIO= System.IO.File;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace NavigationViewExample.Pages
{
    
    public partial class GuaGua : Page
    {
        public GuaGua()
        {
            InitializeComponent();
            _proc = HookCallback;//鼠标检测                             
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;// 将 DLL 搜索路径设为程序运行目录 调用自己的C++库
            SetDllDirectory(baseDir);
            //OnLoaded;//启动的
            //OnUnloaded;
        }
        private string pathKeep2 = ""; // 用于存储选择的保存路径
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
                        await ProcessImagesGpu(path, pathKeep);
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
            var kernel = accelerator.LoadAutoGroupedStreamKernel<ILGPU.Index2D, ArrayView<byte>, ArrayView<byte>, ArrayView<byte>, ArrayView<byte>, int, int>(ExtractKernel);
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
        public static void ExtractKernel(ILGPU.Index2D index, ArrayView<byte> pixelData, ArrayView<byte> rView, ArrayView<byte> gView, ArrayView<byte> bView, int width, int height)
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
                    OffOn.Source = null;
                    OffOn.Source = new BitmapImage(new Uri("/PNG/On.png", UriKind.Relative));
                }
                else if (content == "Off")
                {
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
        private void Button_Click2(object sender, RoutedEventArgs e)
        {
            if (PCHW == 0)
            {
                _runningimage = false;
                PCHW = 1;
            }
            else
            {
                _runningimage = true;
                PCHW = 0;
            }
            if (_isRunning == false)
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
            return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(moduleName), 0);
        }
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)//3钩子回调：捕获 WM_RBUTTONDOWN 则触发 Running 逻辑
        {
            const int WM_RBUTTONDOWN = 0x0204;
            if (nCode >= 0 && wParam == (IntPtr)WM_RBUTTONDOWN)
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Appdata", "data2.txt");
                string content = FileIO.ReadAllText(filePath);
                string[] parts = content.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                CatchLeft = (int)Convert.ToDouble(parts[4]);
                CatchTop = (int)Convert.ToDouble(parts[3]);
                CatchWidth = (int)Convert.ToDouble(parts[1]);
                CatchHeight = (int)Convert.ToDouble(parts[2]);
                if (_runningimage == false)
                {
                    Task.Run(CaptureLoopAsync);
                    _runningimage = true;
                }
                else
                {
                    _runningimage = false;
                }
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
        private volatile bool _runningimage = false;
        private int CatchLeft = 1;
        private int CatchTop = 1;
        private int CatchWidth = 1;
        private int CatchHeight = 1;
        //调用自己的C++库
        [DllImport("kernel32.dll", SetLastError = true)]        // 在应用启动时设置 DLL 搜索路径
        private static extern bool SetDllDirectory(string lpPathName);
        // 原有 P/Invoke 声明
        [DllImport("ScreenCaptureWpfEasy.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool CaptureFrameRGB(int x, int y, int width, int height, out IntPtr buffer, out int outWidth, out int outHeight);
        [DllImport("ScreenCaptureWpfEasy.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FreeBuffer(IntPtr buffer);
        private async Task CaptureLoopAsync()//截图实现
        {
            string filePathImage = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Appdata", "ImageOnOrOff.txt");
            string content = "On";
            int ImageOnOrOff = 0;
            if (!FileIO.Exists(filePathImage))
            {
                FileIO.WriteAllText(filePathImage, "On");
                ImageOnOrOff = 1;
            }
            else
            {
                content = FileIO.ReadAllText(filePathImage);
                if (content == "On")
                {
                    ImageOnOrOff = 1;
                }
                else if (content == "Off")
                {
                    ImageOnOrOff = 0;
                }
                else { MessageBox.Show($"文件:\n{filePathImage}\n的内容格式不正确，已经取消了你的操作"); }
            }
            while (_runningimage == true && PCHW == 1 && ImageOnOrOff == 1)
            {
                if (!CaptureFrameRGB(CatchLeft, CatchTop, CatchWidth, CatchHeight, out IntPtr bufPtr, out int width, out int height))
                {
                    MessageBox.Show("Capture failed");
                    return;
                }
                int stride = width * 2;
                int bufSize = height * stride;
                var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr565, null, bufPtr, bufSize, stride);
                bmp.Freeze();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ScreenImage.Source = bmp;
                });
                FreeBuffer(bufPtr);
                await Task.Delay(100);
            }
        }
        private async void Button_Click_1(object sender, RoutedEventArgs e)// 多通道合并
        {
            // 1. 选择输入/输出文件夹（原样保留）
            var dlg1 = new CommonOpenFileDialog { IsFolderPicker = true, Title = "选择通道文件所在文件夹" };
            if (dlg1.ShowDialog() != CommonFileDialogResult.Ok) return;
            string path3 = dlg1.FileName;

            var dlg2 = new CommonOpenFileDialog { IsFolderPicker = true, Title = "选择输出文件夹" };
            if (dlg2.ShowDialog() != CommonFileDialogResult.Ok) return;
            string path4 = dlg2.FileName;

            ring.Visibility = Visibility.Visible;

            // 2. 扫描 R/G/B 文件组
            var triplets = Directory.GetFiles(path3, "*R.txt")
                .Select(r =>
                {
                    string name = Path.GetFileNameWithoutExtension(r).TrimEnd('R');
                    return (
                        baseName: name,
                        rPath: r,
                        gPath: Path.Combine(path3, name + "G.txt"),
                        bPath: Path.Combine(path3, name + "B.txt")
                    );
                })
                .Where(t => FileIO.Exists(t.gPath) && FileIO.Exists(t.bPath))
                .ToList();

            if (triplets.Count == 0)
            {
                MessageBox.Show("未找到有效的 R/G/B 通道文件组。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ring.Visibility = Visibility.Collapsed;
                return;
            }

            // 3. ILGPU 初始化（原样保留）
            using var context = Context.Create(builder => builder.OpenCL());
            var device = context.GetPreferredDevice(preferCPU: false);
            using var accelerator = device.CreateAccelerator(context);
            var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D,
                ArrayView<byte>, ArrayView<byte>, ArrayView<byte>, ArrayView<byte>>(Conv1x1Kernel);

            // 4. 并行处理
            var tasks = triplets.Select(async t =>
            {
                // 4.1 读取三通道数据
                var rArr = await ReadTxtToByteArray(t.rPath);
                var gArr = await ReadTxtToByteArray(t.gPath);
                var bArr = await ReadTxtToByteArray(t.bPath);

                // 4.2 调用你的原算法：1×1 卷积求平均
                var merged = KernelCom(accelerator, kernel, rArr, gArr, bArr);

                // 4.3 计算宽高
                int width = GetWidthFromTxt(t.rPath);
                if (width <= 0) throw new InvalidDataException($"无法从 {t.rPath} 获取宽度");
                if (merged.Length % width != 0)
                    throw new InvalidDataException(
                        $"{t.baseName}Merged 长度 {merged.Length} 无法整除宽度 {width}");
                int height = merged.Length / width;

                // 4.4 写出模板：首行写 “w h”，后续每行 width 个值
                string outPath = Path.Combine(path4, $"{t.baseName}Merged.txt");
                using var sw = new StreamWriter(outPath);
                sw.WriteLine($"{width} {height}");
                for (int y = 0; y < height; y++)
                {
                    int offset = y * width;
                    var row = merged.Skip(offset).Take(width);
                    sw.WriteLine(string.Join(",", row));
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            MessageBox.Show("通道合并并写出矩形模板成功", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            ring.Visibility = Visibility.Collapsed; 
        }
        private static byte[] KernelCom(Accelerator accelerator, Action<ILGPU.Index1D, ArrayView<byte>, ArrayView<byte>, ArrayView<byte>, ArrayView<byte>> kernel, byte[] rArr, byte[] gArr, byte[] bArr)// 1*1卷积核 算法
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
        //  RGB三色合并单(调用自己封装的库)，在 GPU 上进行模板匹配(读取本地的特征数据的文件夹)，然后输出最匹配位置和大小
        // 截屏 DLL 导入  //处理


        // 1. 按钮事件：加载模板＋主图 → 一维化数据 → GPU 计算 → 冒泡排序 → 显示结果
        private void RGB(object sender, RoutedEventArgs e)
        {

        }
        /// <summary>
        /// 单个灰度模板
        /// </summary>

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            string filePathImage = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Appdata", "ImageOnOrOff.txt");
            string content = "On";
            if (!FileIO.Exists(filePathImage))
            {
                FileIO.WriteAllText(filePathImage, "On");
                OffOn.Source = null;
                OffOn.Source = new BitmapImage(new Uri("/PNG/On.png", UriKind.Relative));
            }
            else
            {
                content = FileIO.ReadAllText(filePathImage);
                if (content == "On")
                {
                    FileIO.WriteAllText(filePathImage, "Off");
                    OffOn.Source = null;
                    OffOn.Source = new BitmapImage(new Uri("/PNG/Off.png", UriKind.Relative));
                }
                else if (content == "Off")
                {
                    FileIO.WriteAllText(filePathImage, "On");
                    OffOn.Source = null;
                    OffOn.Source = new BitmapImage(new Uri("/PNG/On.png", UriKind.Relative));
                }
                else { MessageBox.Show($"文件:\n{filePathImage}\n的内容格式不正确，已经取消了你的操作"); }
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
        // 使用自己的opencl库
        private const string DllName = "CLMath.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double CL_Add(double[] arr, int len, int deviceIndex);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double CL_Sub(double[] arr, int len, int deviceIndex);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double CL_Mul(double[] arr, int len, int deviceIndex);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double CL_Div(double[] arr, int len, int deviceIndex);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetDeviceNamesCount();
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetDeviceNames", CharSet =CharSet.Ansi)]
        private static extern void GetDeviceNames(int index, StringBuilder buf, int bufSize);
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

        // 按钮 “Get All CL Devices” 异步枚举并在弹窗显示
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
                Dispatcher.Invoke(() =>
                MessageBox.Show("Device 0: NVDIA GeForce RTX 4050\nDevice 1: NVDIA GeForce RTX 3060", "Available OpenCL Devices"));
            });
        }
    }
}