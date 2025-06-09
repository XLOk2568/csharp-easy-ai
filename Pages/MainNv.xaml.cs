using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
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
using Windows.ApplicationModel.Appointments.AppointmentsProvider;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace NavigationViewExample.Pages
{
    /// <summary>
    /// Interaction logic for AppsPage.xaml
    /// </summary>
    public partial class MainNvPage : Page
    {
        public MainNvPage()
        {
            InitializeComponent();
        }
        //NV控制页面切换
        public Pages.HomePage Page_Home = new Pages.HomePage();
        public Pages.AboutPage About = new Pages.AboutPage();
        public Pages.ToolPage Tool = new Pages.ToolPage();
        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var item = sender.SelectedItem;
            Page? page = null;

            if (item == NavigationViewItem_Home)
            {
                page = Page_Home;
            }
            else if(item==NavigationViewItem_Tool)
            {
                page = Tool;
            }
            else if (item == Aboutb)
            {
                page = About;
            }
            if (page != null)
            {
                NavigationView_Root.Header = page.Title;
                Frame_Main.Navigate(page);
            }
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            NavigationView_Root.SelectedItem = NavigationViewItem_Home;
            NavigationViewItem_Home.Content =App.IL_all_list[5];
            NvSet.Content = App.IL_all_list[4];
            Aboutb.Content = App.IL_all_list[3];
            Add_list.Content = App.IL_all_list[19];
        }
        //添加
        private int item_Count = 0;
        private List<NavigationViewItem> NVI_ = new List<NavigationViewItem>();
        private void AI_(object sender, RoutedEventArgs e)
        {
            item_Count++;
            var newItem = new NavigationViewItem
            {
                Content = $"A{item_Count}",
                Tag = $"A{item_Count}"
            };
            NVI_.Add(newItem);
            Add_list.MenuItems.Add(newItem);
        }
        private void SetImagesButton_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < NVI_.Count; i++)
            {
                var imageUri = new Uri($"pack://application:,,,/YourAssemblyName;component/Images/Image{i + 1}.png", UriKind.Absolute);
                NVI_[i].Icon = new BitmapImage(imageUri);
            }
         }
    }
}
