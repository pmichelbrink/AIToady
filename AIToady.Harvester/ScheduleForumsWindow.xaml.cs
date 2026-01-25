using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace AIToady.Harvester
{
    public partial class ScheduleForumsWindow : Window
    {
        private ObservableCollection<string> _forums;
        private bool _darkMode;

        public ScheduleForumsWindow(ObservableCollection<string> forums, bool darkMode)
        {
            InitializeComponent();
            _forums = forums;
            _darkMode = darkMode;
            ForumsListBox.ItemsSource = _forums;
            Loaded += (s, e) => ApplyTheme();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var address = ForumAddressTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(address) && !_forums.Contains(address))
            {
                _forums.Add(address);
                ForumAddressTextBox.Clear();
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = ForumsListBox.SelectedItems.Cast<string>().ToList();
            foreach (var item in selected)
                _forums.Remove(item);
        }

        private void ForumsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RemoveButton.IsEnabled = ForumsListBox.SelectedItems.Count > 0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ScheduleForums = string.Join("|", _forums);
            Properties.Settings.Default.Save();
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ApplyTheme()
        {
            var bg = _darkMode ? "#1E1E1E" : "#FFFFFF";
            var fg = _darkMode ? "#FFFFFF" : "#000000";
            var controlBg = _darkMode ? "#2D2D30" : "#FFFFFF";
            var borderBrush = _darkMode ? "#3F3F46" : "#ABADB3";

            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(bg);
            Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(fg);

            if (_darkMode)
                SetWindowChromeDark();
            else
                SetWindowChromeLight();

            foreach (var element in FindVisualChildren<Control>(this))
            {
                if (element is TextBox || element is ListBox)
                {
                    element.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(controlBg);
                    element.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(fg);
                    element.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(borderBrush);
                }
                else if (element is Label)
                {
                    element.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(fg);
                }
            }
        }

        private void SetWindowChromeDark()
        {
            if (System.Environment.OSVersion.Version.Build >= 22000)
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int value = 1;
                DwmSetWindowAttribute(hwnd, 20, ref value, sizeof(int));
            }
        }

        private void SetWindowChromeLight()
        {
            if (System.Environment.OSVersion.Version.Build >= 22000)
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int value = 0;
                DwmSetWindowAttribute(hwnd, 20, ref value, sizeof(int));
            }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t) yield return t;
                    foreach (var childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
                }
            }
        }
    }
}
