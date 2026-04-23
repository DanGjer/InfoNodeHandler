using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InfoNodeHandler
{
    public class ProgressUI
    {
        public class HostListItem
        {
            public string DrofusOccurrenceId { get; set; }
            public string Name { get; set; }
            public string Mod { get; set; }
            public string Tag { get; set; }
            public string SubItems { get; set; }
        }

        private Window _progressWindow;
        private System.Windows.Controls.TextBox _logBox;
        private StackPanel _actionsPanel;
        private System.Windows.Controls.Grid _hostsPanel;
        private Button _showHostsButton;
        private Button _showNewHostsButton;
        private Button _showMovedHostsButton;
        private Button _showUpdatedHostsButton;
        private Button _closeButton;
        private DataGrid _hostsGrid;
        private System.Windows.Controls.TextBox _idFilter;
        private System.Windows.Controls.TextBox _nameFilter;
        private System.Windows.Controls.TextBox _modFilter;
        private System.Windows.Controls.TextBox _tagFilter;
        private System.Windows.Controls.TextBox _subItemsFilter;
        private TextBlock _hostsHint;
        private Func<IEnumerable<HostListItem>> _allHostsProvider;
        private Func<IEnumerable<HostListItem>> _newHostsProvider;
        private Func<IEnumerable<HostListItem>> _movedHostsProvider;
        private Func<IEnumerable<HostListItem>> _updatedHostsProvider;
        private List<HostListItem> _allHosts = new List<HostListItem>();

        public ProgressUI(string title = "Processing...")
        {
            InitializeWindow(title);
        }

        public ProgressUI(string title, int maxValue) : this(title) { }

        private void InitializeWindow(string title)
        {
            _logBox = new System.Windows.Controls.TextBox
            {
                IsReadOnly = true,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 16,
                Background = Brushes.Black,
                Foreground = Brushes.LightGreen,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0)
            };

            _actionsPanel = new StackPanel
            {
                Margin = new Thickness(10, 0, 0, 0)
            };

            _showHostsButton = new Button
            {
                Content = "Show hosts",
                Height = 30,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = false
            };
            _showHostsButton.Click += (s, e) => PopulateHostsList(_allHostsProvider, "all hosts");

            _showNewHostsButton = new Button
            {
                Content = "Show new hosts",
                Height = 30,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = false
            };
            _showNewHostsButton.Click += (s, e) => PopulateHostsList(_newHostsProvider, "new hosts");

            _showMovedHostsButton = new Button
            {
                Content = "Show moved hosts",
                Height = 30,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = false
            };
            _showMovedHostsButton.Click += (s, e) => PopulateHostsList(_movedHostsProvider, "moved hosts");

            _showUpdatedHostsButton = new Button
            {
                Content = "Show updated hosts",
                Height = 30,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = false
            };
            _showUpdatedHostsButton.Click += (s, e) => PopulateHostsList(_updatedHostsProvider, "updated hosts");

            _closeButton = new Button
            {
                Content = "Close",
                Height = 30,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Visibility = System.Windows.Visibility.Collapsed
            };

            var actionsHeader = new TextBlock
            {
                Text = "Actions",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10, 10, 10, 6)
            };

            var actionsHint = new TextBlock
            {
                Text = "Resevert plass for fremtidige knapper og sånn",
                Margin = new Thickness(10, 0, 10, 10),
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap
            };

            _actionsPanel.Children.Add(actionsHeader);
            _actionsPanel.Children.Add(actionsHint);
            _actionsPanel.Children.Add(_showHostsButton);
            _actionsPanel.Children.Add(_showNewHostsButton);
            _actionsPanel.Children.Add(_showMovedHostsButton);
            _actionsPanel.Children.Add(_showUpdatedHostsButton);
            _actionsPanel.Children.Add(_closeButton);

            _hostsHint = new TextBlock
            {
                Text = "Press Show hosts to load list",
                Margin = new Thickness(10, 0, 10, 6),
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap
            };

            _hostsGrid = new DataGrid
            {
                Margin = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 22, 26)),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 238, 220)),
                BorderThickness = new Thickness(0),
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.None,
                AlternationCount = 2,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = 120,
                MaxHeight = double.PositiveInfinity
            };
            ScrollViewer.SetVerticalScrollBarVisibility(_hostsGrid, System.Windows.Controls.ScrollBarVisibility.Auto);
            ScrollViewer.SetHorizontalScrollBarVisibility(_hostsGrid, System.Windows.Controls.ScrollBarVisibility.Auto);

            _hostsGrid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new System.Windows.Data.Binding(nameof(HostListItem.DrofusOccurrenceId)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            _hostsGrid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new System.Windows.Data.Binding(nameof(HostListItem.Name)), Width = new DataGridLength(1.8, DataGridLengthUnitType.Star) });
            _hostsGrid.Columns.Add(new DataGridTextColumn { Header = "Mod", Binding = new System.Windows.Data.Binding(nameof(HostListItem.Mod)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
            _hostsGrid.Columns.Add(new DataGridTextColumn { Header = "Tag", Binding = new System.Windows.Data.Binding(nameof(HostListItem.Tag)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
            _hostsGrid.Columns.Add(new DataGridTextColumn { Header = "SubItems", Binding = new System.Windows.Data.Binding(nameof(HostListItem.SubItems)), Width = new DataGridLength(0.8, DataGridLengthUnitType.Star) });

            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 33, 38))));
            var rowAltTrigger = new Trigger { Property = ItemsControl.AlternationIndexProperty, Value = 1 };
            rowAltTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 38, 44))));
            rowStyle.Triggers.Add(rowAltTrigger);
            var rowSelectedTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            rowSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(53, 96, 130))));
            rowSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, Brushes.White));
            rowStyle.Triggers.Add(rowSelectedTrigger);
            _hostsGrid.RowStyle = rowStyle;

            var filterGrid = new System.Windows.Controls.Grid { Margin = new Thickness(10, 0, 10, 8) };
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });

            _idFilter = CreateFilterBox("ID");
            _nameFilter = CreateFilterBox("Name");
            _modFilter = CreateFilterBox("Mod");
            _tagFilter = CreateFilterBox("Tag");
            _subItemsFilter = CreateFilterBox("SubItems");

            AddFilterControl(filterGrid, _idFilter, 0);
            AddFilterControl(filterGrid, _nameFilter, 1);
            AddFilterControl(filterGrid, _modFilter, 2);
            AddFilterControl(filterGrid, _tagFilter, 3);
            AddFilterControl(filterGrid, _subItemsFilter, 4);

            var hostsHeader = new TextBlock
            {
                Text = "Hosts",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10, 10, 10, 6)
            };

            var hostsBorder = new Border
            {
                Margin = new Thickness(10, 0, 10, 10),
                Padding = new Thickness(0),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 99, 109)),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(14, 18, 22)),
                CornerRadius = new CornerRadius(6),
                Child = _hostsGrid
            };

            _hostsPanel = new System.Windows.Controls.Grid();
            _hostsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _hostsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _hostsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _hostsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _hostsPanel.Children.Add(hostsHeader);
            _hostsPanel.Children.Add(_hostsHint);
            _hostsPanel.Children.Add(filterGrid);
            _hostsPanel.Children.Add(hostsBorder);

            System.Windows.Controls.Grid.SetRow(hostsHeader, 0);
            System.Windows.Controls.Grid.SetRow(_hostsHint, 1);
            System.Windows.Controls.Grid.SetRow(filterGrid, 2);
            System.Windows.Controls.Grid.SetRow(hostsBorder, 3);

            var layoutGrid = new System.Windows.Controls.Grid();
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
            layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            System.Windows.Controls.Grid.SetColumn(_logBox, 0);
            System.Windows.Controls.Grid.SetRow(_logBox, 0);
            System.Windows.Controls.Grid.SetColumn(_actionsPanel, 1);
            System.Windows.Controls.Grid.SetRow(_actionsPanel, 0);
            System.Windows.Controls.Grid.SetColumn(_hostsPanel, 0);
            System.Windows.Controls.Grid.SetColumnSpan(_hostsPanel, 2);
            System.Windows.Controls.Grid.SetRow(_hostsPanel, 1);

            layoutGrid.Children.Add(_logBox);
            layoutGrid.Children.Add(_actionsPanel);
            layoutGrid.Children.Add(_hostsPanel);

            _progressWindow = new Window
            {
                Title = title,
                Width = 1400,
                Height = 900,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize,
                Topmost = false,
                Content = layoutGrid
            };
        }

        public void Show()
        {
            _progressWindow.Show();
        }

        public void Hide()
        {
            _progressWindow.Hide();
        }

        public void Close()
        {
            if (_progressWindow == null) return;

            _showHostsButton.IsEnabled = _allHostsProvider != null;
            _showNewHostsButton.IsEnabled = _newHostsProvider != null;
            _showMovedHostsButton.IsEnabled = _movedHostsProvider != null;
            _showUpdatedHostsButton.IsEnabled = _updatedHostsProvider != null;
            AppendLog("--- Done. Click Close to exit ---");
            _closeButton.Visibility = System.Windows.Visibility.Visible;

            var frame = new System.Windows.Threading.DispatcherFrame();
            _closeButton.Click += (s, e) =>
            {
                frame.Continue = false;
                _progressWindow.Close();
            };
            _progressWindow.Closing += (s, e) => frame.Continue = false;

            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        public void AppendLog(string message)
        {
            _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            _logBox.ScrollToEnd();
            _progressWindow?.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() => { }));
        }

        public void SetHostsProvider(Func<IEnumerable<HostListItem>> hostsProvider)
        {
            SetHostProviders(hostsProvider, null, null, null);
        }

        public void SetHostProviders(
            Func<IEnumerable<HostListItem>> allHostsProvider,
            Func<IEnumerable<HostListItem>> newHostsProvider,
            Func<IEnumerable<HostListItem>> movedHostsProvider,
            Func<IEnumerable<HostListItem>> updatedHostsProvider)
        {
            _allHostsProvider = allHostsProvider;
            _newHostsProvider = newHostsProvider;
            _movedHostsProvider = movedHostsProvider;
            _updatedHostsProvider = updatedHostsProvider;

            _showHostsButton.IsEnabled = false;
            _showNewHostsButton.IsEnabled = false;
            _showMovedHostsButton.IsEnabled = false;
            _showUpdatedHostsButton.IsEnabled = false;

            _hostsHint.Text = _allHostsProvider == null
                ? "No hosts available"
                : "Hosts are available after run completes.";
        }

        private void PopulateHostsList(Func<IEnumerable<HostListItem>> hostsProvider, string label)
        {
            if (hostsProvider == null)
            {
                _hostsGrid.ItemsSource = null;
                _hostsHint.Text = "No hosts available";
                return;
            }

            var entries = hostsProvider();
            _allHosts = entries == null ? new List<HostListItem>() : new List<HostListItem>(entries);
            ApplyHostFilters();
            _hostsHint.Text = _allHosts.Count == 0
                ? $"No {label} found"
                : $"Loaded {_allHosts.Count} {label}";
        }

        private System.Windows.Controls.TextBox CreateFilterBox(string tooltip)
        {
            var textBox = new System.Windows.Controls.TextBox
            {
                Margin = new Thickness(3, 0, 3, 0),
                ToolTip = $"Filter by {tooltip}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };
            textBox.TextChanged += (s, e) => ApplyHostFilters();
            return textBox;
        }

        private static void AddFilterControl(System.Windows.Controls.Grid grid, System.Windows.Controls.TextBox textBox, int column)
        {
            System.Windows.Controls.Grid.SetColumn(textBox, column);
            grid.Children.Add(textBox);
        }

        private void ApplyHostFilters()
        {
            IEnumerable<HostListItem> filtered = _allHosts;

            if (!string.IsNullOrWhiteSpace(_idFilter.Text))
                filtered = filtered.Where(h => (h.DrofusOccurrenceId ?? string.Empty).Contains(_idFilter.Text, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(_nameFilter.Text))
                filtered = filtered.Where(h => (h.Name ?? string.Empty).Contains(_nameFilter.Text, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(_modFilter.Text))
                filtered = filtered.Where(h => (h.Mod ?? string.Empty).Contains(_modFilter.Text, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(_tagFilter.Text))
                filtered = filtered.Where(h => (h.Tag ?? string.Empty).Contains(_tagFilter.Text, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(_subItemsFilter.Text))
                filtered = filtered.Where(h => (h.SubItems ?? string.Empty).Contains(_subItemsFilter.Text, StringComparison.OrdinalIgnoreCase));

            _hostsGrid.ItemsSource = filtered.ToList();
        }

        // Legacy compatibility — routes to AppendLog
        public void UpdateStatus(string message) => AppendLog(message);
        public void UpdateProgress(int currentValue) { }
        public void UpdateProgressWithStatus(int currentValue, string message) => AppendLog(message);
    }
}
