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
            public string DrofusOccurrenceId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Mod { get; set; } = string.Empty;
            public string Tag { get; set; } = string.Empty;
            public string SubItems { get; set; } = string.Empty;
            public List<string> SubItemDetails { get; set; } = new List<string>();
        }

        private Window _progressWindow = null!;
        private System.Windows.Controls.TextBox _logBox = null!;
        private StackPanel _actionsPanel = null!;
        private System.Windows.Controls.Grid _hostsPanel = null!;
        private Button _showHostsButton = null!;
        private Button _showNewHostsButton = null!;
        private Button _showMovedHostsButton = null!;
        private Button _showUpdatedHostsButton = null!;
        private Button _closeButton = null!;
        private DataGrid _hostsGrid = null!;
        private System.Windows.Controls.TextBox _idFilter = null!;
        private System.Windows.Controls.TextBox _nameFilter = null!;
        private System.Windows.Controls.TextBox _modFilter = null!;
        private System.Windows.Controls.TextBox _tagFilter = null!;
        private System.Windows.Controls.TextBox _subItemsFilter = null!;
        private TextBlock _hostsHint = null!;
        private Func<IEnumerable<HostListItem>>? _allHostsProvider;
        private Func<IEnumerable<HostListItem>>? _newHostsProvider;
        private Func<IEnumerable<HostListItem>>? _movedHostsProvider;
        private Func<IEnumerable<HostListItem>>? _updatedHostsProvider;
        private Action<HostListItem>? _selectHostAction;
        private Action<HostListItem>? _jumpToHostAction;
        private List<HostListItem> _allHosts = new List<HostListItem>();
        private readonly Brush _themeBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 38, 44));
        private readonly Brush _themePanel = new SolidColorBrush(System.Windows.Media.Color.FromRgb(27, 32, 37));
        private readonly Brush _themePanelAlt = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 27, 31));
        private readonly Brush _themeAccent = new SolidColorBrush(System.Windows.Media.Color.FromRgb(214, 102, 74));
        private readonly Brush _themeAccentMuted = new SolidColorBrush(System.Windows.Media.Color.FromRgb(154, 84, 67));
        private readonly Brush _themeTextPrimary = new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 231, 238));
        private readonly Brush _themeTextMuted = new SolidColorBrush(System.Windows.Media.Color.FromRgb(154, 164, 174));

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
                Background = _themePanelAlt,
                Foreground = _themeTextPrimary,
                BorderThickness = new Thickness(1),
                BorderBrush = _themeAccentMuted,
                Margin = new Thickness(0)
            };

            _actionsPanel = new StackPanel
            {
                Margin = new Thickness(10, 0, 0, 0),
                Background = _themePanel
            };

            var sidebarButtonStyle = new Style(typeof(Button));
            sidebarButtonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, _themePanelAlt));
            sidebarButtonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, _themeAccent));
            sidebarButtonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, _themeAccent));
            sidebarButtonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderThicknessProperty, new Thickness(1)));
            sidebarButtonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.FontWeightProperty, FontWeights.SemiBold));

            _showHostsButton = new Button
            {
                Content = "Vis Infonoder",
                Height = 30,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = sidebarButtonStyle,
                IsEnabled = false
            };
            _showHostsButton.Click += (s, e) => PopulateHostsList(_allHostsProvider, "alle Infonoder");

            _showNewHostsButton = new Button
            {
                Content = "Vis nye Infonoder",
                Height = 30,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = sidebarButtonStyle,
                IsEnabled = false
            };
            _showNewHostsButton.Click += (s, e) => PopulateHostsList(_newHostsProvider, "nye Infonoder");

            _showMovedHostsButton = new Button
            {
                Content = "Vis flyttede Infonoder",
                Height = 30,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = sidebarButtonStyle,
                IsEnabled = false
            };
            _showMovedHostsButton.Click += (s, e) => PopulateHostsList(_movedHostsProvider, "flyttede Infonoder");

            _showUpdatedHostsButton = new Button
            {
                Content = "Vis oppdaterte Infonoder",
                Height = 30,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = sidebarButtonStyle,
                IsEnabled = false
            };
            _showUpdatedHostsButton.Click += (s, e) => PopulateHostsList(_updatedHostsProvider, "oppdaterte Infonoder");

            _closeButton = new Button
            {
                Content = "Lukk",
                Height = 30,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = sidebarButtonStyle,
                Visibility = System.Windows.Visibility.Collapsed
            };
            _closeButton.Click += (s, e) => _progressWindow.Close();

            var actionsHeader = new TextBlock
            {
                Text = "Handlinger",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10, 10, 10, 6),
                Foreground = _themeTextPrimary
            };

            var actionsHint = new TextBlock
            {
                Text = "Resevert plass for fremtidige knapper og sånn",
                Margin = new Thickness(10, 0, 10, 10),
                Foreground = _themeTextMuted,
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
                Foreground = _themeTextMuted,
                TextWrapping = TextWrapping.Wrap
            };

            _hostsGrid = new DataGrid
            {
                Margin = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Background = _themePanelAlt,
                Foreground = _themeTextPrimary,
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

            var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, _themeTextPrimary));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, _themePanel));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, _themeAccentMuted));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.FontWeightProperty, FontWeights.SemiBold));
            _hostsGrid.ColumnHeaderStyle = headerStyle;

            _hostsGrid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new System.Windows.Data.Binding(nameof(HostListItem.DrofusOccurrenceId)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            _hostsGrid.Columns.Add(new DataGridTextColumn { Header = "Navn", Binding = new System.Windows.Data.Binding(nameof(HostListItem.Name)), Width = new DataGridLength(1.8, DataGridLengthUnitType.Star) });
            _hostsGrid.Columns.Add(new DataGridTextColumn { Header = "Mod", Binding = new System.Windows.Data.Binding(nameof(HostListItem.Mod)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
            _hostsGrid.Columns.Add(new DataGridTextColumn { Header = "Tag", Binding = new System.Windows.Data.Binding(nameof(HostListItem.Tag)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
            _hostsGrid.Columns.Add(CreateSubItemsButtonColumn());
            _hostsGrid.Columns.Add(CreateActionButtonColumn("Velg", "Velg", OnSelectHostClicked));
            _hostsGrid.Columns.Add(CreateActionButtonColumn("Gå til", "Gå til", OnJumpToHostClicked));

            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, _themePanel));
            var rowAltTrigger = new Trigger { Property = ItemsControl.AlternationIndexProperty, Value = 1 };
            rowAltTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, _themePanelAlt));
            rowStyle.Triggers.Add(rowAltTrigger);
            var rowSelectedTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            rowSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 44, 38))));
            rowSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, _themeTextPrimary));
            rowStyle.Triggers.Add(rowSelectedTrigger);
            _hostsGrid.RowStyle = rowStyle;

            var filterGrid = new System.Windows.Controls.Grid { Margin = new Thickness(10, 0, 10, 8) };
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) }); // Select column
            filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) }); // Jump column

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
                Text = "Infonoder",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10, 10, 10, 6),
                Foreground = _themeTextPrimary
            };

            var hostsBorder = new Border
            {
                Margin = new Thickness(10, 0, 10, 10),
                Padding = new Thickness(0),
                BorderThickness = new Thickness(1),
                BorderBrush = _themeAccentMuted,
                Background = _themePanelAlt,
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
            layoutGrid.Background = _themeBackground;
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
                Background = _themeBackground,
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

        public void Complete()
        {
            if (_progressWindow == null) return;

            _showHostsButton.IsEnabled = _allHostsProvider != null;
            _showNewHostsButton.IsEnabled = _newHostsProvider != null;
            _showMovedHostsButton.IsEnabled = _movedHostsProvider != null;
            _showUpdatedHostsButton.IsEnabled = _updatedHostsProvider != null;
            AppendLog("--- Ferdig. Klikk Lukk for å avslutte ---");
            _closeButton.Visibility = System.Windows.Visibility.Visible;
        }

        public void Close()
        {
            _progressWindow?.Close();
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

        public void SetHostActions(Action<HostListItem>? selectHostAction, Action<HostListItem>? jumpToHostAction)
        {
            _selectHostAction = selectHostAction;
            _jumpToHostAction = jumpToHostAction;
        }

        public void SetHostProviders(
            Func<IEnumerable<HostListItem>>? allHostsProvider,
            Func<IEnumerable<HostListItem>>? newHostsProvider,
            Func<IEnumerable<HostListItem>>? movedHostsProvider,
            Func<IEnumerable<HostListItem>>? updatedHostsProvider)
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
                ? "Ingen Infonoder tilgjengelig"
                : "Infonoder er tilgjengelige etter kjøring er fullført.";
        }

        private void PopulateHostsList(Func<IEnumerable<HostListItem>>? hostsProvider, string label)
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
                ToolTip = $"Filtrer etter {tooltip}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = _themePanelAlt,
                Foreground = _themeTextPrimary,
                BorderBrush = _themeAccentMuted,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
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

        private DataGridTemplateColumn CreateActionButtonColumn(string header, string buttonText, RoutedEventHandler clickHandler)
        {
            var buttonFactory = new FrameworkElementFactory(typeof(Button));
            buttonFactory.SetValue(Button.ContentProperty, buttonText);
            buttonFactory.SetValue(Button.MarginProperty, new Thickness(4, 1, 4, 1));
            buttonFactory.SetValue(Button.PaddingProperty, new Thickness(6, 2, 6, 2));
            buttonFactory.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            buttonFactory.SetValue(Button.BackgroundProperty, _themePanelAlt);
            buttonFactory.SetValue(Button.ForegroundProperty, _themeAccent);
            buttonFactory.SetValue(Button.BorderBrushProperty, _themeAccent);
            buttonFactory.SetValue(Button.BorderThicknessProperty, new Thickness(1));
            buttonFactory.AddHandler(Button.ClickEvent, clickHandler);

            var template = new DataTemplate { VisualTree = buttonFactory };

            return new DataGridTemplateColumn
            {
                Header = header,
                CellTemplate = template,
                Width = new DataGridLength(0.8, DataGridLengthUnitType.Star)
            };
        }

        private DataGridTemplateColumn CreateSubItemsButtonColumn()
        {
            var buttonFactory = new FrameworkElementFactory(typeof(Button));
            buttonFactory.SetValue(Button.MarginProperty, new Thickness(4, 1, 4, 1));
            buttonFactory.SetValue(Button.PaddingProperty, new Thickness(6, 2, 6, 2));
            buttonFactory.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            buttonFactory.SetValue(Button.BackgroundProperty, _themePanelAlt);
            buttonFactory.SetValue(Button.ForegroundProperty, _themeAccent);
            buttonFactory.SetValue(Button.BorderBrushProperty, _themeAccent);
            buttonFactory.SetValue(Button.BorderThicknessProperty, new Thickness(1));
            buttonFactory.SetBinding(Button.ContentProperty, new System.Windows.Data.Binding(nameof(HostListItem.SubItems)) { StringFormat = "Vis ({0})" });
            buttonFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnSubItemsClicked));

            var template = new DataTemplate { VisualTree = buttonFactory };

            return new DataGridTemplateColumn
            {
                Header = "Tilleggsartikler",
                CellTemplate = template,
                Width = new DataGridLength(0.8, DataGridLengthUnitType.Star)
            };
        }

        private void OnSubItemsClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not HostListItem item)
                return;

            if (item.SubItemDetails == null || item.SubItemDetails.Count == 0)
            {
                AppendLog($"Ingen tilleggsartikler funnet for Infonode {item.DrofusOccurrenceId}.");
                return;
            }

            var listBox = new ListBox
            {
                Margin = new Thickness(10),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Background = _themePanelAlt,
                Foreground = _themeTextPrimary,
                BorderBrush = _themeAccentMuted,
                BorderThickness = new Thickness(1),
                ItemsSource = item.SubItemDetails
            };

            var infoText = new TextBlock
            {
                Margin = new Thickness(10, 10, 10, 0),
                Text = $"{item.SubItemDetails.Count} tilleggsartikler for Infonode {item.DrofusOccurrenceId}",
                Foreground = _themeTextPrimary,
                TextWrapping = TextWrapping.Wrap
            };

            var closeButton = new Button
            {
                Content = "Lukk",
                MinWidth = 90,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = _themePanelAlt,
                Foreground = _themeAccent,
                BorderBrush = _themeAccent,
                BorderThickness = new Thickness(1),
                IsDefault = true,
                IsCancel = true
            };

            var layout = new System.Windows.Controls.Grid();
            layout.Background = _themePanel;
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.Children.Add(infoText);
            layout.Children.Add(listBox);
            layout.Children.Add(closeButton);
            System.Windows.Controls.Grid.SetRow(infoText, 0);
            System.Windows.Controls.Grid.SetRow(listBox, 1);
            System.Windows.Controls.Grid.SetRow(closeButton, 2);

            var dialog = new Window
            {
                Title = $"Tilleggsartikler - Infonode {item.DrofusOccurrenceId}",
                Width = 700,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize,
                Background = _themeBackground,
                Content = layout,
                Owner = _progressWindow
            };

            closeButton.Click += (_, _) => dialog.Close();
            dialog.ShowDialog();
        }

        private void OnSelectHostClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is HostListItem item)
            {
                if (_selectHostAction == null)
                {
                    AppendLog("Select action is not available.");
                    return;
                }

                _selectHostAction(item);
            }
        }

        private void OnJumpToHostClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is HostListItem item)
            {
                if (_jumpToHostAction == null)
                {
                    AppendLog("Jump action is not available.");
                    return;
                }

                _jumpToHostAction(item);
            }
        }

        // Legacy compatibility — routes to AppendLog
        public void UpdateStatus(string message) => AppendLog(message);
        public void UpdateProgress(int currentValue) { }
        public void UpdateProgressWithStatus(int currentValue, string message) => AppendLog(message);
    }
}
