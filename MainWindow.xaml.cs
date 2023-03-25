using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using REghZyFramework.Themes;

namespace PIDViewer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public const int MainViewWidth = 250;
        public const int MainViewMaxHeight = 500;

        public const int LabelValueWidth = 180;
        public const int KillButtonHeight = 30;

        public class LabelKeyValue
        {
            public TextBlock Title { set; get; }
            public TextBox Content { set; get; }

            public LabelKeyValue(TextBlock title, TextBox content)
            {
                Title = title;
                Content = content;
            }

            public void SetText(object v)
            {
                var text = v?.ToString();
                Content.Text = text;
                Content.ToolTip = text;
            }
        }

        public class ViewInfo
        {
            public Button LightMode { set; get; }
            public Button DarkMode { set; get; }

            public LabelKeyValue PID { set; get; }
            public LabelKeyValue Title { set; get; }
            public LabelKeyValue Name { set; get; }
            public LabelKeyValue Version { set; get; }
            public LabelKeyValue StartTime { set; get; }
            public LabelKeyValue Argument { set; get; }
            public Button Kill { set; get; }

            public MyProcessInfo Info;


            public IEnumerable<UIElement> CreatePanels()
            {
                yield return AddThemeBtn();

                var elementNames = new List<string>() { nameof(PID), nameof(Title), nameof(Name),
                                                        nameof(Version), nameof(StartTime), nameof(Argument),
                                                        nameof(Kill) };

                for (int i = 0; i < elementNames.Count; i++)
                {
                    var name = elementNames[i];
                    var getter = new Func<Type>(() => { return GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance).PropertyType; });
                    var setter = new Action<object>((object obj) => GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance).SetValue(this, obj));
                    var itemType = getter();
                    var isLabel = itemType == typeof(LabelKeyValue);
                    var isButton = itemType == typeof(Button);
                    UIElement element = null;

                    if (isLabel)
                    {
                        var stackPanel = new StackPanel();
                        stackPanel.Orientation = Orientation.Horizontal;
                        stackPanel.Margin = new Thickness(5);

                        var labelKey = new TextBlock();
                        labelKey.TextAlignment = TextAlignment.Center;
                        labelKey.HorizontalAlignment = HorizontalAlignment.Center;
                        labelKey.Text = $"{name}: ";
                        stackPanel.Children.Add(labelKey);

                        var labelValue = new TextBox();
                        labelValue.Width = LabelValueWidth;
                        labelValue.TextWrapping = TextWrapping.Wrap;
                        labelValue.TextAlignment = TextAlignment.Left;
                        labelValue.HorizontalAlignment = HorizontalAlignment.Left;
                        labelValue.IsReadOnly = true;
                        labelValue.BorderThickness = new Thickness(0);
                        labelValue.Text = string.Empty;
                        ToolTipService.SetShowDuration(labelValue, int.MaxValue);
                        stackPanel.Children.Add(labelValue);

                        var labelKeyValue = new LabelKeyValue(labelKey, labelValue);
                        setter(labelKeyValue);
                        element = stackPanel;
                    }
                    else if (isButton)
                    {
                        var button = new Button();
                        button.Height = KillButtonHeight;
                        button.Content = name;

                        setter(button);
                        element = button;
                    }

                    yield return element;
                }
            }

            public UIElement AddThemeBtn()
            {
                var stackPanel = new StackPanel();
                stackPanel.Orientation = Orientation.Horizontal;
                stackPanel.Margin = new Thickness(0);

                var lightBtn = new Button();
                lightBtn.Content = "Light Mode";
                LightMode = lightBtn;
                stackPanel.Children.Add(lightBtn);

                var darkBtn = new Button();
                darkBtn.Content = "Dark Mode";
                DarkMode = darkBtn;
                stackPanel.Children.Add(darkBtn);

                foreach (Button item in stackPanel.Children)
                {
                    item.Width = MainViewWidth / 2;
                    item.Height = KillButtonHeight;
                    item.FontSize = 12;
                }

                return stackPanel;
            }

            public void SetData(MyProcessInfo processInfo)
            {
                Info = processInfo;
                PID.SetText(processInfo.PID);
                Title.SetText(processInfo.Title);
                Name.SetText(processInfo.Name);
                Version.SetText(processInfo.Version);
                StartTime.SetText(processInfo.StartTime);
                Argument.SetText(processInfo.Argument);
            }
        }


        TextBox input;
        ViewInfo viewInfo;

        public MainWindow()
        {
            InitializeComponent();
            InitView();

            PIDManager.Instance.OnForegroundWindowChanged += Instance_OnForegroundWindowChanged;
            PIDManager.Instance.UpdateInfo();
        }

        void Instance_OnForegroundWindowChanged(MyProcessInfo info)
        {
            if (info != null && viewInfo?.Info == info)
                UpdateInfo(info);

            if (string.IsNullOrEmpty(input.Text))
                UpdateInfo(info);
        }

        void InitView()
        {
            Title = "PIDViewer";
            MaxWidth = MinWidth = Width = MainViewWidth;
            ResizeMode = ResizeMode.CanMinimize;

            var children = grid.Children;
            var stackPanel = new StackPanel();
            stackPanel.Orientation = Orientation.Vertical;
            stackPanel.HorizontalAlignment = HorizontalAlignment.Left;
            children.Add(stackPanel);

            input = new TextBox();
            input.TextChanged += Input_TextChanged;
            stackPanel.Children.Add(input);

            viewInfo = new ViewInfo();
            foreach (var item in viewInfo.CreatePanels())
                stackPanel.Children.Add(item);

            viewInfo.Kill.Click += Kill_Click;
            viewInfo.DarkMode.Click += DarkMode_Click;
            viewInfo.LightMode.Click += LightMode_Click;

            try
            {
                if (Win32API.ShouldSystemUseDarkMode())
                    SetDark();
                else
                    SetLight();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                SetLight();
            }
        }

        void Input_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(input.Text, out var pid))
            {
                try
                {
                    var info = PIDManager.Instance.GetProcessInfo(pid);
                    UpdateInfo(info);
                }
                catch { }
            }
        }

        void LightMode_Click(object sender, RoutedEventArgs e)
        {
            SetLight();
        }

        void DarkMode_Click(object sender, RoutedEventArgs e)
        {
            SetDark();
        }

        void Kill_Click(object sender, RoutedEventArgs e)
        {
            var processName = PIDManager.Instance.ForegroundProcessInfo?.Name;
            if (!string.IsNullOrEmpty(processName) && MessageBox.Show($"是否确定终止 {processName} ?", "提示", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                PIDManager.Instance.KillForegroundWindow();
        }

        void SetDark()
        {
            SetTheme(ThemesController.ThemeTypes.Dark);
        }

        void SetLight()
        {
            SetTheme(ThemesController.ThemeTypes.Light);
        }

        void SetTheme(ThemesController.ThemeTypes theme)
        {
            ThemesController.SetTheme(theme);

            var activeColor = System.Windows.Media.Brushes.Gray;
            var deactiveColor = System.Windows.Media.Brushes.Transparent;
            var isLight = theme == ThemesController.ThemeTypes.Light || theme == ThemesController.ThemeTypes.ColourfulLight;
            viewInfo.LightMode.Background = isLight ? activeColor : deactiveColor;
            viewInfo.DarkMode.Background = isLight ? deactiveColor : activeColor;
        }

        async void UpdateInfo(MyProcessInfo info)
        {
            viewInfo?.SetData(info);
            Topmost = true;

            await Task.Delay(1);

            var desiredSize = DesiredSize;
            desiredSize.Height = MainViewMaxHeight;
            Measure(desiredSize);
            Height = DesiredSize.Height;

            Console.WriteLine($"UpdateInfo:{info}");
        }
    }
}
