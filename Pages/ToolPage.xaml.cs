using ILGPU.Runtime;
using ILGPU;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WindowsAPICodePack.Dialogs;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using Path = System.IO.Path;
using ILGPU.Runtime.OpenCL;
using System.Reflection;
using CSScripting;
using CSScriptLib;
using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace NavigationViewExample.Pages
{
    /// <summary>
    /// Interaction logic for HomePage.xaml
    /// </summary>
    public partial class ToolPage : Page
    {
        public ToolPage()
        {
            InitializeComponent();
        }
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            wait2.Visibility = Visibility.Visible;
            string path = "";
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                path = dialog.FileName; // 获取用户选择的文件夹路径  
                await ProcessTextFilesAsync(path); // 使用 await 运算符等待异步方法完成  
                wait2.Visibility = Visibility.Collapsed;
            }
            else
            {
                wait2.Visibility = Visibility.Collapsed;
            }
        }
        public static async Task ProcessTextFilesAsync(string folderPath)
        {
            // 获取所有子文件夹中的 .txt 文件
            var txtFiles = Directory.EnumerateFiles(folderPath, "*.txt", SearchOption.AllDirectories);
            if (!txtFiles.Any())
            {
                MessageBox.Show("文件夹及其子文件夹中没有 .txt 文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // 弹窗让用户选择 CSV 存储路径
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv",
                Title = "选择 CSV 文件保存位置"
            };

            if (saveDialog.ShowDialog() != true) return;
            string csvPath = saveDialog.FileName;
            // 异步处理文件并写入 CSV
            await Task.Run(() =>
            {
                using (StreamWriter writer = new StreamWriter(csvPath))
                {
                    long maxIndex = 9999999999999; // 设定索引最大值，达到后归零
                    foreach (string file in txtFiles)
                    {
                        string[] lines = File.ReadAllLines(file); // 读取所有行
                        for (long i = 0; i < lines.LongLength; i++)
                        {
                            long index = (long)(i % maxIndex); // 达到maxIndex后归零
                            writer.WriteLine($"{lines[i]},{index}"); // 第一列存文本内容，第二列存索引
                        }
                    }
                }
            });
            MessageBox.Show($"CSV 文件已保存到: {csvPath}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            wait2.Visibility = Visibility.Visible;
            string path = "";
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                path = dialog.FileName; // 获取用户选择的文件夹路径  
                await ProcessTextFilesAsync2(path); // 使用 await 运算符等待异步方法完成  
                wait2.Visibility = Visibility.Collapsed;
            }
            else
            {
                wait2.Visibility = Visibility.Collapsed;
            }
        }
        public static async Task ProcessTextFilesAsync2(string folderPath)
        {
            // 获取所有子文件夹中的 .txt 文件
            var txtFiles = Directory.EnumerateFiles(folderPath, "*.txt", SearchOption.AllDirectories);
            if (!txtFiles.Any())
            {
                MessageBox.Show("文件夹及其子文件夹中没有 .txt 文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // 异步修改每个 .txt 文件
            await Task.Run(() =>
            {
                foreach (string file in txtFiles)
                {
                    string[] lines = File.ReadAllLines(file); // 读取所有行
                    for (long i = 0; i < lines.LongLength; i++)
                    {
                        // 使用正则提取每行的第一个完整词组
                        Match match = Regex.Match(lines[i], @"^([\p{L}\p{N}]+)");
                        if (match.Success)
                        {
                            lines[i] = match.Value;
                        }
                    }
                    File.WriteAllLines(file, lines); // 直接写回原文件
                }
            });
            MessageBox.Show("所有 .txt 文件已修改完成！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            wait2.Visibility = Visibility.Visible;
            string path = "";
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                path = dialog.FileName; // 获取用户选择的文件夹路径  
                await ProcessTextFilesAsync3(path); // 使用 await 运算符等待异步方法完成  
                wait2.Visibility = Visibility.Collapsed;
            }
            else
            {
                wait2.Visibility = Visibility.Collapsed;
            }
        }
        public static async Task ProcessTextFilesAsync3(string folderPath)
        {
            // 获取所有子文件夹中的 .txt 文件
            var txtFiles = Directory.EnumerateFiles(folderPath, "*.txt", SearchOption.AllDirectories);
            if (!txtFiles.Any())
            {
                MessageBox.Show("文件夹及其子文件夹中没有 .txt 文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // 异步修改每个 .txt 文件
            await Task.Run(() =>
            {
                foreach (string file in txtFiles)
                {
                    string[] lines = File.ReadAllLines(file) // 读取所有行
                                         .Where(line => line.Length > 1) // 过滤掉单个字符的行
                                         .ToArray();

                    File.WriteAllLines(file, lines); // 直接写回原文件
                }
            });
            MessageBox.Show("所有单字符行已删除！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            wait2.Visibility = Visibility.Visible;
            string path = "";
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = false, // 允许选择文件
                Multiselect = false // 只选一个文件
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                path = dialog.FileName; // 获取用户选择的文件路径  
                await ProcessTextFilesAsync4(path);
                wait2.Visibility = Visibility.Collapsed;
            }
            else
            {
                wait2.Visibility = Visibility.Collapsed;
            }
        }
        public static async Task ProcessTextFilesAsync4(string folderPath)
        {
            // 异步去重并覆盖原文件
            await Task.Run(() =>
            {
                var lines = File.ReadAllLines(folderPath);
                var distinctLines = lines.Distinct().ToArray();
                File.WriteAllLines(folderPath, distinctLines);
            });
            MessageBox.Show("CSV 文件已去重完成！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
