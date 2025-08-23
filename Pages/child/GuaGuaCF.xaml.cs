using NavigationViewExample;
using NavigationViewExample.Pages;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WindowsAPICodePack.Dialogs;
using Path = System.IO.Path;
using Point = System.Windows.Point;

namespace WPFtransformer.Pages.child
{
    /// <summary>
    /// GuaGua.xaml 的交互逻辑
    /// </summary>
    public partial class GuaGuaCF : Window
    {
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Appdata", "data2.1.txt");
        string temptext = "";
        private const int WM_NCHITTEST = 0x0084;        //标题栏
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int ResizeBorder = 8; // 拖拽触发区域宽度
        public GuaGuaCF()
        {
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(filePath))
            {
                // 假设文件内容类似："window_800_600_100_200"
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
            else
            {
                File.WriteAllText(filePath, MainWindow.writeText);
                MessageBox.Show("Please reuse the execution.");
            }
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            GuaGua.PCHW = 0;
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
        }
        //标题栏
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            hwndSource.AddHook(WndProc);
        }
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                handled = true;
                var pt = new Point((lParam.ToInt32() & 0xFFFF), (lParam.ToInt32() >> 16));
                var relative = PointFromScreen(pt);
                var w = ActualWidth; var h = ActualHeight;
                // 四角
                if (relative.X <= ResizeBorder && relative.Y <= ResizeBorder) return (IntPtr)HTTOPLEFT;
                if (relative.X >= w - ResizeBorder && relative.Y <= ResizeBorder) return (IntPtr)HTTOPRIGHT;
                if (relative.X <= ResizeBorder && relative.Y >= h - ResizeBorder) return (IntPtr)HTBOTTOMLEFT;
                if (relative.X >= w - ResizeBorder && relative.Y >= h - ResizeBorder) return (IntPtr)HTBOTTOMRIGHT;
                // 边缘
                if (relative.X <= ResizeBorder) return (IntPtr)HTLEFT;
                if (relative.X >= w - ResizeBorder) return (IntPtr)HTRIGHT;
                if (relative.Y <= ResizeBorder) return (IntPtr)HTTOP;
                if (relative.Y >= h - ResizeBorder) return (IntPtr)HTBOTTOM;
                return (IntPtr)HTCLIENT;
            }
            return IntPtr.Zero;
        }
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
        private void Minimize_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaxRestore_Click(object s, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void Close_Click(object s, RoutedEventArgs e) => Close();
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
                        GuaGua.pathKeep2 = pathKeep; // 保存选择的路径
                        ring.Visibility = Visibility.Visible;
                        await ImageFeatures(path, pathKeep);
                        MessageBox.Show($"CSV 文件已保存到: {pathKeep}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        ring.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }
        public static async Task ImageFeatures(string pathDaiShiBie, string pathBaoCunShiBie)
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
                    // 算术平均法计算灰度
                    int gray = (int)(r * 0.6 + g * 0.25 + b * 0.15);
                    sb.Append(gray);
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
        }//提取指定文件夹下所有图片的RGB并且转换成矩阵特征矩阵存储
    }
}
