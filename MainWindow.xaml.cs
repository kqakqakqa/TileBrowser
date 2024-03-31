using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Timers;
using Windows.UI.StartScreen;
using Microsoft.Web.WebView2.Core;
using Windows.Storage;
using Windows.System;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.Diagnostics;
using CefSharp.OffScreen;
using CefSharp;
using CefSharp.DevTools.Page;
using CefSharp.DevTools.Target;
using System.Collections;
using System.Drawing;
using Windows.ApplicationModel;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Markup;
using System.Text.Json;
using System.Xml;
using System.Collections.Specialized;
using Windows.UI.ViewManagement;
using System.Runtime.InteropServices;
using WinRT;
using WinUIEx;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Shapes;
using System.Runtime.ConstrainedExecution;
using Windows.System.UserProfile;
using CefSharp.DevTools.CacheStorage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Windows.Devices.Input;
using System.Reflection.Metadata;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TileBrowser
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WinUIEx.WindowEx
    {







        private class TileArg
        {
            public bool IsEnabled { get; set; }
            public string TileId { get; set; }
            public string WebUrl { get; set; }
            public string DisplayName { get; set; }
            public double IntervalMinute { get; set; }
            public TileSizeIncludingWin11 TileSizeIncludingWin11 { get; set; }
            public UrlMode UrlMode { get; set; }
        }

        private class TileArgs
        {

            public static TileArgs instance;

            public static readonly OrderedDictionary tileArgs = new();

            private TileArgs() { }

            public static TileArgs Instance
            {
                get
                {
                    if (instance == null)
                    {
                        instance = new TileArgs();
                    }
                    return instance;
                }
            }

            public static async Task ReadFromFileAsync()
            {
                // read tiles.json
                // await Task.Delay(1000);
                var localFolder = ApplicationData.Current.LocalFolder;
                StorageFile configFile = await localFolder.CreateFileAsync($"Tiles.json", CreationCollisionOption.OpenIfExists);
                var configPath = configFile.Path;
                string tileArgsJsonString = await File.ReadAllTextAsync(configPath);
                JsonDocument configJsonDocument;
                try
                {
                    configJsonDocument = JsonDocument.Parse(tileArgsJsonString);
                }
                catch (JsonException)
                { // if tiles.json not exist
                    configJsonDocument = JsonDocument.Parse("{\"tiles\":[]}");
                }
                var configJsonElement = configJsonDocument.RootElement;
                var configDict = JsonElement2Dictionary(configJsonElement) as Dictionary<string, object>;
                var tiles = configDict["tiles"] as List<object>;

                tileArgs.Clear();

                // deserialize json
                foreach (Dictionary<string, object> tile in tiles.Cast<Dictionary<string, object>>())
                {

                    string tileId = (tile["tileId"] ?? Guid.NewGuid().ToString("N")) as string;
                    while (tileArgs.Contains(tileId))
                    { tileId = Guid.NewGuid().ToString("N"); }

                    var tileArg = new TileArg
                    {
                        TileId = tileId,

                        // 统一把默认值放到一起，ReadFromFileAsync的和xaml的，还有其他的
                        WebUrl = tile.ContainsKey("webUrl") ? tile["webUrl"] as string : "",
                        DisplayName = tile.ContainsKey("displayName") ? tile["displayName"] as string : "",
                        IntervalMinute = tile.ContainsKey("intervalMinute") ? (double)tile["intervalMinute"] : 5,
                        IsEnabled = tile.ContainsKey("isEnabled") ? (bool)tile["isEnabled"] : true,

                        TileSizeIncludingWin11 = (tile.ContainsKey("tileSize") ? tile["tileSize"] : null) switch
                        {
                            "small" => TileSizeIncludingWin11.Small,
                            "medium" => TileSizeIncludingWin11.Medium,
                            "wide" => TileSizeIncludingWin11.Wide,
                            "large" => TileSizeIncludingWin11.Large,
                            "win11" => TileSizeIncludingWin11.Win11,
                            _ => TileSizeIncludingWin11.Win11,
                        },

                        UrlMode = (tile.ContainsKey("urlMode") ? tile["urlMode"] : null) switch
                        {
                            "online" => UrlMode.Online,
                            "local" => UrlMode.Local,
                            _ => UrlMode.Online,
                        },

                    };

                    tileArgs[tileId] = tileArg;
                }
            }

            public static async Task WriteToFileAsync()
            {
                var tileArgsOutput = new List<object>();
                foreach (TileArg tileArg in tileArgs.Values)
                {
                    var tile = new Dictionary<string, object>
                    {
                        ["tileId"] = tileArg.TileId,
                        ["webUrl"] = tileArg.WebUrl,
                        ["displayName"] = tileArg.DisplayName,
                        ["intervalMinute"] = tileArg.IntervalMinute,
                        ["isEnabled"] = tileArg.IsEnabled,

                        ["tileSize"] = tileArg.TileSizeIncludingWin11 switch
                        {
                            TileSizeIncludingWin11.Small => "small",
                            TileSizeIncludingWin11.Medium => "medium",
                            TileSizeIncludingWin11.Wide => "wide",
                            TileSizeIncludingWin11.Large => "large",
                            TileSizeIncludingWin11.Win11 => "win11",
                            _ => "win11",
                        },

                        ["urlMode"] = tileArg.UrlMode switch
                        {
                            UrlMode.Online => "online",
                            UrlMode.Local => "local",
                            _ => "online",
                        },

                    };

                    tileArgsOutput.Add(tile);
                }
                // parse tileargs
                var tiles = new
                {
                    tiles = tileArgsOutput
                };

                // serialize json
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                };
                string tileArgsJsonString = JsonSerializer.Serialize(tiles, options);

                // write tiles.json
                var localFolder = ApplicationData.Current.LocalFolder;
                StorageFile tileArgsFile = await localFolder.CreateFileAsync($"Tiles.json", CreationCollisionOption.OpenIfExists);
                var tileArgsPath = tileArgsFile.Path;
                await File.WriteAllTextAsync(tileArgsPath, tileArgsJsonString);
            }

            private static object JsonElement2Dictionary(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dict[property.Name] = JsonElement2Dictionary(property.Value);
                    }
                    return dict;
                }
                return element.ValueKind switch
                {
                    JsonValueKind.Array => element.EnumerateArray().Select(JsonElement2Dictionary).ToList(),
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null,
                };
            }
        }

        private class TileProperty : DependencyObject
        {
            public static readonly DependencyProperty TileIdProperty =
            DependencyProperty.RegisterAttached(
              "TileId",
              typeof(string),
              typeof(TileProperty),
              new PropertyMetadata(false)
            );
            public static void SetTileId(UIElement element, string value)
            {
                if (element == null)
                {
                    return;
                }
                element.SetValue(TileIdProperty, value);
            }
            public static string GetTileId(UIElement element)
            {
                if (element == null)
                {
                    return null;
                }
                return element.GetValue(TileIdProperty) as string;
            }
        }

        enum TileSizeIncludingWin11
        {
            Small = TileSize.Square70x70,
            Medium = TileSize.Square150x150,
            Wide = TileSize.Wide310x150,
            Large = TileSize.Square310x310,
            Win11 = 8,
        }

        enum UrlMode { Online, Local }

        static readonly Dictionary<string, Timer> tileUpdaters = new();

        public static string GetLocalizedString(string resource)
        {
            var resourceLoader = new Windows.ApplicationModel.Resources.ResourceLoader();
            string localizedString = resourceLoader.GetString(resource);
            return localizedString;
        }








        public MainWindow()
        {
            this.InitializeComponent();

            // title
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBar);

            // cef
            var settings = new CefSettings()
            {
                WindowlessRenderingEnabled = true,
                LogSeverity = LogSeverity.Disable,
                Locale = System.Globalization.CultureInfo.CurrentCulture.Name,
            };
            settings.CefCommandLineArgs.Add("--disable-web-security", "1");
            settings.CefCommandLineArgs.Add("--js-flags", "--max_old_space_size=64");
            settings.SetOffScreenRenderingBestPerformanceArgs();
            Cef.InitializeAsync(settings);
        }

        private void TitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            RightPaddingColumn.Width = new GridLength(AppWindow.TitleBar.RightInset / TitleBar.XamlRoot.RasterizationScale);
            LeftPaddingColumn.Width = new GridLength(AppWindow.TitleBar.LeftInset / TitleBar.XamlRoot.RasterizationScale);
        }

        private async void ListViewTiles_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(50);
            await RefreshAllTilesAsync();
        }

        private void TileEdit_Url_UrlMode_Loaded(object sender, RoutedEventArgs e)
        {
            TileEdit_Url_UrlMode.SelectionChanged += UrlModeSelectionChanged;
        }









        private void UrlModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedIndex = TileEdit_Url_UrlMode.SelectedIndex;
            if (selectedIndex == 0)
            {
                TileEdit_Url_Textbox.PlaceholderText = "https://example.com";
                TileEdit_Url_LocalPageFolder.Visibility = Visibility.Collapsed;
                return;
            };
            if (selectedIndex == 1)
            {
                TileEdit_Url_Textbox.PlaceholderText = "example.html";
                TileEdit_Url_LocalPageFolder.Visibility = Visibility.Visible;
                return;
            }
        }

        private void ListViewTiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string tileId;
            if (ListViewTiles.SelectedItems.Count == 0)
            {
                tileId = null;
            }
            else
            {
                var listViewItem = ListViewTiles.SelectedItem as UIElement;
                tileId = TileProperty.GetTileId(listViewItem);
            }
            ApplyTileArgToTileEdit(tileId);
        }

        private async void ButtonRefreshAllTiles_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAllTilesAsync();
        }

        private async void ButtonLocalPageFolder_Click(object sender, RoutedEventArgs e)
        {
            var offlineWebPageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("LocalPages", CreationCollisionOption.OpenIfExists);
            await Launcher.LaunchFolderAsync(offlineWebPageFolder);
        }

        private async void AddTile_Button_Click(object sender, RoutedEventArgs e)
        {
            await TileArgs.ReadFromFileAsync();
            string tileId;
            do
            {
                tileId = Guid.NewGuid().ToString("N");
            } while (TileArgs.tileArgs.Contains(tileId));

            var tileArg = new TileArg
            {
                TileId = tileId,
                WebUrl = "",
                DisplayName = GetLocalizedString("New Tile"),
                IntervalMinute = 5,
                TileSizeIncludingWin11 = TileSizeIncludingWin11.Win11
            };
            TileArgs.tileArgs.Insert(0, tileId, tileArg);
            await TileArgs.WriteToFileAsync();
            ApplyTileArgsToListViewTiles();
            ListViewTiles.SelectedIndex = 0;
        }

        private async void TileEdit_Delete_Button_Click(object sender, RoutedEventArgs e)
        {
            var listViewItem = ListViewTiles.SelectedItem as UIElement;
            var tileId = TileProperty.GetTileId(listViewItem);
            if (tileId != null)
            {
                TileArgs.tileArgs.Remove(tileId);
                await TileArgs.WriteToFileAsync();
                ApplyTileArgsToListViewTiles();
            }
            TileEdit_Delete_Flyout.Hide();
        }

        private async void TileEdit_Save_Button_Click(object sender, RoutedEventArgs e)
        {
            string tileId = TileProperty.GetTileId(ListViewTiles.SelectedItem as UIElement);
            ApplyTileEditToTileArg(tileId);
            await TileArgs.WriteToFileAsync();
            var tileArg = TileArgs.tileArgs[tileId] as TileArg;
            SetTileUpdater(tileArg);
            ApplyTileArgsToListViewTiles();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            args.Handled = true;
            this.Hide();
        }

        private void TaskbarMenu_Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Closed -= Window_Closed;
            this.Close();
            Application.Current.Exit();
        }

        [RelayCommand]
        public void TaskbarIcon_LeftClick()
        {
            this.Show();
        }

        private async void TaskbarMenu_Refresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAllTilesAsync();
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, long nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public int X;
            public int Y;
        }

        private void MoveWindow(object window, int x, int y)
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            const int HWND_TOPMOST = -1;
            const int GWL_STYLE = -16;
            const int GWL_EXSTYLE = -20;
            const int WS_OVERLAPPED = 0x00000000;
            const int WS_BORDER = 0x00800000;
            const int WS_CAPTION = 0x00C00000;
            const int WS_SYSMENU = 0x00080000;
            const int WS_MINIMIZEBOX = 0x00020000;
            const int WS_MAXIMIZEBOX = 0x00010000;
            const int WS_THICKFRAME = 0x00040000;
            const uint SWP_NOMOVE = 0x0002;
            const uint SWP_NOSIZE = 0x0001;
            const uint SWP_HIDEWINDOW = 0x0080;
            const int WS_SIZEBOX = 0x00040000;
            const int WS_VISIBLE = 0x10000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_TRANSPARENT = 0x00000020;
            const int WS_EX_LAYERED = 0x00080000;
            const int WS_DLGFRAME = 0x00400000;
            const long WS_POPUP = 0x80000000;

            long style = GetWindowLong(hwnd, GWL_STYLE);
            long exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            style &= WS_DLGFRAME | WS_POPUP | ~(WS_CAPTION | WS_THICKFRAME | WS_SIZEBOX | WS_VISIBLE);
            exStyle &= WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_LAYERED;

            SetWindowLong(hwnd, GWL_STYLE, style);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            SetWindowPos(hwnd, new IntPtr(HWND_TOPMOST), x, y, 160, 160, SWP_HIDEWINDOW);
        }


        [RelayCommand]
        public void TaskbarIcon_RightClick()
        {
            var window = new WindowEx()
            {
                Width = 0,
                Height = 0,
                IsTitleBarVisible = false,
                SystemBackdrop = new TransparentTintBackdrop(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
        };
            Win32Point cursorPosition;
            GetCursorPos(out cursorPosition);
            MoveWindow(window, cursorPosition.X, cursorPosition.Y);

            MenuFlyout menuFlyout = new MenuFlyout()
            {
                Placement = FlyoutPlacementMode.TopEdgeAlignedLeft,
            };
            menuFlyout.Closed += (object sender, object e) =>
            {
                window.Close();
            };
            var taskbarMenu_Refresh = new MenuFlyoutItem { Text = GetLocalizedString("Refresh") };
            var taskbarMenu_Exit = new MenuFlyoutItem { Text = GetLocalizedString("Exit") };
            taskbarMenu_Refresh.Click += TaskbarMenu_Refresh_Click;
            taskbarMenu_Exit.Click += TaskbarMenu_Exit_Click;

            menuFlyout.Items.Add(taskbarMenu_Refresh);
            menuFlyout.Items.Add(taskbarMenu_Exit);

            var grid = new Grid();
            window.Content = grid;
            grid.Loaded += (object sender, RoutedEventArgs e) =>
            {
                menuFlyout.ShowAt(grid);
            };

            window.Show();

            // this.Show();
            // 不用nuget提供的menu，用自己写的
        }









        private async Task RefreshAllTilesAsync()
        {
            await TileArgs.ReadFromFileAsync();
            ApplyTileArgsToListViewTiles();
            foreach (TileArg tileArg in TileArgs.tileArgs.Values)
            {
                SetTileUpdater(tileArg);
            }
        }

        private static UIElement CreateListViewTileItem(TileArg tileArg)
        {
            var listViewItem = new TextBlock
            {
                Text = tileArg.DisplayName,
                TextLineBounds = TextLineBounds.Tight,
                VerticalAlignment = VerticalAlignment.Center,
            };
            TileProperty.SetTileId(listViewItem, tileArg.TileId);
            return listViewItem;
        }

        private void ApplyTileArgsToListViewTiles()
        {
            var tileIndex = ListViewTiles.SelectedIndex;
            ListViewTiles.Items.Clear();
            foreach (TileArg tileArg in TileArgs.tileArgs.Values)
            {
                var listViewTileItem = CreateListViewTileItem(tileArg);
                ListViewTiles.Items.Add(listViewTileItem);
            }
            if (ListViewTiles.Items.Count - 1 < tileIndex)
            {
                ListViewTiles.SelectedIndex = ListViewTiles.Items.Count - 1;
            }
            else
            {
                ListViewTiles.SelectedIndex = tileIndex;
            }
        }

        private void ApplyTileEditToTileArg(string tileId)
        {
            var tileArg = new TileArg
            {
                TileId = tileId,
                IsEnabled = TileEdit_IsEnabled_ToggleSwitch.IsOn,
                WebUrl = TileEdit_Url_Textbox.Text,
                DisplayName = TileEdit_DisplayName.Text,
                IntervalMinute = TileEdit_Interval_NumberBox.Value,

                TileSizeIncludingWin11 = TileEdit_TileSize_ComboBox.SelectedIndex switch
                {
                    0 => TileSizeIncludingWin11.Small,
                    1 => TileSizeIncludingWin11.Medium,
                    2 => TileSizeIncludingWin11.Wide,
                    3 => TileSizeIncludingWin11.Large,
                    4 => TileSizeIncludingWin11.Win11,
                    _ => TileSizeIncludingWin11.Win11,
                },

                UrlMode = TileEdit_Url_UrlMode.SelectedIndex switch
                {
                    0 => UrlMode.Online,
                    1 => UrlMode.Local,
                    _ => UrlMode.Online
                }
            };

            TileArgs.tileArgs[tileId] = tileArg;
        }

        private void ApplyTileArgToTileEdit(string tileId)
        {
            if (tileId == null)
            {
                TileEdit.Visibility = Visibility.Collapsed; // 用frame，有动画
                return;
            }

            var tileArg = TileArgs.tileArgs[tileId] as TileArg;

            TileEdit_IsEnabled_ToggleSwitch.IsOn = tileArg.IsEnabled;
            TileEdit_Url_Textbox.Text = tileArg.WebUrl;
            TileEdit_DisplayName.Text = tileArg.DisplayName;
            TileEdit_Interval_NumberBox.Value = tileArg.IntervalMinute;

            TileEdit_Url_UrlMode.SelectedIndex = tileArg.UrlMode switch
            {
                UrlMode.Online => 0,
                UrlMode.Local => 1,
                _ => 0,
            };

            TileEdit_TileSize_ComboBox.SelectedIndex = tileArg.TileSizeIncludingWin11 switch
            {
                TileSizeIncludingWin11.Small => 0,
                TileSizeIncludingWin11.Medium => 1,
                TileSizeIncludingWin11.Wide => 2,
                TileSizeIncludingWin11.Large => 3,
                TileSizeIncludingWin11.Win11 => 4,
                _ => 4,
            };

            TileEdit.Visibility = Visibility.Visible;
        }









        private async void SetTileUpdater(TileArg tileArg)
        {
            string tileId = tileArg.TileId;

            // stop timer
            Timer timer;
            bool isUpdating = tileUpdaters.ContainsKey(tileId);
            if (isUpdating)
            {
                timer = tileUpdaters[tileId];
                timer.Stop();
                timer.Close();
            }

            if (!tileArg.IsEnabled)
            { return; }

            string displayName = tileArg.DisplayName;
            double intervalMinute = tileArg.IntervalMinute;
            var tileSizeIncludingWin11 = tileArg.TileSizeIncludingWin11;

            var urlMode = tileArg.UrlMode;
            string webUrl;
            if (urlMode == UrlMode.Local)
            {
                var localPagesFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("LocalPages", CreationCollisionOption.OpenIfExists);
                webUrl = System.IO.Path.Combine(localPagesFolder.Path, tileArg.WebUrl);
            }
            else
            {
                webUrl = tileArg.WebUrl;
            }

            // tile
            string arguments = tileId;
            var uri = new Uri($"ms-appdata:///local/WebCapture/{tileId}.png");
            var tileSize = tileSizeIncludingWin11 switch
            {
                TileSizeIncludingWin11.Small => TileSize.Square70x70,
                TileSizeIncludingWin11.Medium => TileSize.Square150x150,
                TileSizeIncludingWin11.Wide => TileSize.Wide310x150,
                TileSizeIncludingWin11.Large => TileSize.Square310x310,
                TileSizeIncludingWin11.Win11 => TileSize.Default,
                _ => TileSize.Default,
            };

            SecondaryTile tile;
            try
            {
                tile = new SecondaryTile(tileId, displayName, arguments, uri, tileSize)
                {
                    DisplayName = displayName
                };
            }
            catch (Exception)
            {
                tile = new SecondaryTile(tileId, displayName, arguments, uri, TileSize.Default)
                {
                    DisplayName = displayName
                };
            }
            tile.VisualElements.Square30x30Logo = uri;
            tile.VisualElements.Square70x70Logo = uri;
            tile.VisualElements.Square150x150Logo = uri;
            tile.VisualElements.Wide310x150Logo = uri;
            tile.VisualElements.Square310x310Logo = uri;
            tile.VisualElements.ShowNameOnSquare150x150Logo = false;
            tile.VisualElements.ShowNameOnWide310x150Logo = false;
            tile.VisualElements.ShowNameOnSquare310x310Logo = false;
            bool isPinned = SecondaryTile.Exists(tileId);
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(tile, hWnd);
            if (!isPinned)
            {
                await tile.RequestCreateAsync();
            }

            // web capture
            byte[] bitmapByteArray = tileSizeIncludingWin11 switch
            {
                TileSizeIncludingWin11.Small => await GetWebCapture(webUrl, 70, 70),
                TileSizeIncludingWin11.Medium => await GetWebCapture(webUrl, 150, 150),
                TileSizeIncludingWin11.Wide => await GetWebCapture(webUrl, 310, 150),
                TileSizeIncludingWin11.Large => await GetWebCapture(webUrl, 310, 310),
                _ => await GetWebCapture(webUrl, 70, 70), // TileSize win11 的大小与 small 相同
            };
            if (bitmapByteArray != null)
            {
                try
                {
                    var webCaptureFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("WebCapture", CreationCollisionOption.OpenIfExists);
                    StorageFile webCaptureFile = await webCaptureFolder.CreateFileAsync($"{tileId}.png", CreationCollisionOption.ReplaceExisting);
                    var webCapturePath = webCaptureFile.Path;
                    await File.WriteAllBytesAsync(System.IO.Path.Combine(webCapturePath), bitmapByteArray);
                }
                catch (Exception) { }
            }

            if (isPinned)
            {
                await tile.UpdateAsync(); // 为什么第一次更新磁贴没有图片
            }

            // start timer
            double interval = intervalMinute * 1000 * 60;
            if (interval < 1000)
            {
                interval = 1000;
            }
            timer = new Timer(interval)
            {
                AutoReset = false,
                Enabled = false
            };
            timer.Elapsed += (source, e) => {
                SetTileUpdater(tileArg);
            };
            tileUpdaters[tileId] = timer;
            timer.Start();
        }

        private static async Task<byte[]> GetWebCapture(string url, int width, int height)
        {
            var browserSettings = new BrowserSettings(true)
            {
                WindowlessFrameRate = 0,
            };
            var browser = new ChromiumWebBrowser(url, browserSettings);
            Task timeout = Task.Delay(5000);
            await Task.WhenAny(browser.WaitForInitialLoadAsync(), timeout);
            if (timeout.IsCompleted)
            {
                return null;
            }
            browser.ExecuteScriptAsync("document.body.style.overflow = 'hidden';");
            await browser.ResizeAsync(width, height);
            var cefBrowserHost = browser.GetBrowserHost();
            cefBrowserHost.Invalidate(PaintElementType.View);
            await Task.WhenAny(browser.WaitForRenderIdleAsync(), Task.Delay(5000));
            var viewport = new Viewport
            {
                Width = width,
                Height = height,
            };
            var bitmapByteArray = await browser.CaptureScreenshotAsync(CaptureScreenshotFormat.Png, 100, viewport);
            browser.GetBrowser().CloseBrowser(true);
            browser.Dispose();
            return bitmapByteArray;
        }
    }
}
