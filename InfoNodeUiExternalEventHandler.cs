namespace InfoNode;

internal enum HostUiActionType
{
    Select,
    JumpTo
}

internal sealed class InfoNodeUiExternalEventHandler : IExternalEventHandler
{
    private sealed class DuplicateInfoNodeItem
    {
        public FamilyInstance Instance { get; set; } = null!;
        public string ElementId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Modname { get; set; } = string.Empty;
    }

    private readonly object _gate = new();
    private readonly Action<string> _log;

    private int? _pendingHostId;
    private HostUiActionType _pendingAction = HostUiActionType.Select;

    public InfoNodeUiExternalEventHandler(Action<string> log)
    {
        _log = log;
    }

    public void Queue(int hostId, HostUiActionType action)
    {
        lock (_gate)
        {
            _pendingHostId = hostId;
            _pendingAction = action;
        }
    }

    public void Execute(UIApplication app)
    {
        int? hostId;
        HostUiActionType action;

        lock (_gate)
        {
            hostId = _pendingHostId;
            action = _pendingAction;
            _pendingHostId = null;
        }

        if (hostId is null)
            return;

        var uiDoc = app.ActiveUIDocument;
        var document = uiDoc?.Document;
        if (uiDoc == null || document == null)
        {
            _log("UI-handling mislyktes: ingen aktiv Revit-dokument.");
            return;
        }

        var matches = new FilteredElementCollector(document)
            .OfClass(typeof(FamilyInstance))
            .OfCategory(BuiltInCategory.OST_SpecialityEquipment)
            .Cast<FamilyInstance>()
            .Where(f =>
            {
                var idParam = f.LookupParameter("InfoNode_hostID");
                return idParam != null && idParam.AsString() == hostId.Value.ToString();
            })
            .ToList();

        if (matches.Count == 0)
        {
            _log($"{GetActionVerb(action)} mislyktes: ingen InfoNode funnet for Infonode {hostId.Value}.");
            return;
        }

        if (matches.Count > 1)
        {
            var selectedIds = ResolveDuplicateSelection(matches, hostId.Value, action);
            if (selectedIds.Count == 0)
            {
                _log($"{GetActionVerb(action)} avbrutt for Infonode {hostId.Value}.");
                return;
            }

            uiDoc.Selection.SetElementIds(selectedIds);
            if (action == HostUiActionType.JumpTo)
                uiDoc.ShowElements(selectedIds);

            if (selectedIds.Count > 1)
                _log($"{GetActionVerb(action)} {selectedIds.Count} dupliserte InfoNoder for Infonode {hostId.Value}.");
            else
                _log($"{GetActionVerb(action)} InfoNode {selectedIds.First().IntegerValue} for Infonode {hostId.Value}.");

            return;
        }

        var instance = matches.First();
        uiDoc.Selection.SetElementIds(new List<ElementId> { instance.Id });
        if (action == HostUiActionType.JumpTo)
            uiDoc.ShowElements(instance.Id);

        _log($"{GetActionVerb(action)} InfoNode {instance.Id.IntegerValue} for Infonode {hostId.Value}.");
    }

    public string GetName() => "InfoNode UI External Event Handler";

    private static string GetActionVerb(HostUiActionType action)
    {
        return action == HostUiActionType.JumpTo ? "Gå til" : "Valgt";
    }

    private static string GetActionLabel(HostUiActionType action)
    {
        return action == HostUiActionType.JumpTo ? "gå til" : "velge";
    }

    private static List<ElementId> ResolveDuplicateSelection(List<FamilyInstance> matches, int hostId, HostUiActionType action)
    {
        var items = matches.Select(f => new DuplicateInfoNodeItem
        {
            Instance = f,
            ElementId = f.Id.IntegerValue.ToString(),
            Name = f.LookupParameter("InfoNode_hostname")?.AsString() ?? string.Empty,
            Tag = f.LookupParameter("InfoNode_hosttag")?.AsString() ?? string.Empty,
            Modname = f.LookupParameter("InfoNode_modname")?.AsString() ?? string.Empty
        }).ToList();

        var listBox = new System.Windows.Controls.ListBox
        {
            Margin = new System.Windows.Thickness(10),
            ItemsSource = items,
            SelectionMode = System.Windows.Controls.SelectionMode.Single
        };

        listBox.ItemTemplate = BuildDuplicateItemTemplate();
        if (items.Count > 0)
            listBox.SelectedIndex = 0;

        var primaryButton = new System.Windows.Controls.Button
        {
            Content = action == HostUiActionType.JumpTo ? "Gå til valgt" : "Velg",
            MinWidth = 120,
            Margin = new System.Windows.Thickness(0, 0, 8, 0),
            IsDefault = true
        };

        var secondaryButton = new System.Windows.Controls.Button
        {
            Content = action == HostUiActionType.JumpTo ? "Gå til alle" : "Velg alle",
            MinWidth = 120,
            Margin = new System.Windows.Thickness(0, 0, 8, 0)
        };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Avbryt",
            MinWidth = 90,
            IsCancel = true
        };

        var buttonsPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new System.Windows.Thickness(10, 0, 10, 10)
        };
        buttonsPanel.Children.Add(primaryButton);
        buttonsPanel.Children.Add(secondaryButton);
        buttonsPanel.Children.Add(cancelButton);

        var mainPanel = new System.Windows.Controls.DockPanel();
        var messageText = new System.Windows.Controls.TextBlock
        {
            Text = $"Infonode {hostId} har {items.Count} InfoNoder med samme Infonode-ID. Velg hva du vil {GetActionLabel(action)}:",
            Margin = new System.Windows.Thickness(10, 10, 10, 0),
            TextWrapping = System.Windows.TextWrapping.Wrap
        };
        System.Windows.Controls.DockPanel.SetDock(messageText, System.Windows.Controls.Dock.Top);
        System.Windows.Controls.DockPanel.SetDock(buttonsPanel, System.Windows.Controls.Dock.Bottom);
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonsPanel);
        mainPanel.Children.Add(listBox);

        var window = new System.Windows.Window
        {
            Title = "Dupliserte InfoNoder oppdaget",
            Width = 760,
            Height = 420,
            ResizeMode = System.Windows.ResizeMode.CanResize,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
            Content = mainPanel
        };

        List<ElementId>? selected = null;

        primaryButton.Click += (_, _) =>
        {
            if (listBox.SelectedItem is DuplicateInfoNodeItem selectedItem)
            {
                selected = new List<ElementId> { selectedItem.Instance.Id };
                window.DialogResult = true;
                window.Close();
            }
        };

        secondaryButton.Click += (_, _) =>
        {
            selected = items.Select(i => i.Instance.Id).ToList();
            window.DialogResult = true;
            window.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            selected = new List<ElementId>();
            window.DialogResult = false;
            window.Close();
        };

        window.ShowDialog();
        return selected ?? new List<ElementId>();
    }

    private static System.Windows.DataTemplate BuildDuplicateItemTemplate()
    {
        var panelFactory = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.StackPanel));
        panelFactory.SetValue(System.Windows.Controls.StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Horizontal);
        panelFactory.SetValue(System.Windows.FrameworkElement.MarginProperty, new System.Windows.Thickness(2));

        panelFactory.AppendChild(CreateColumnText("ID: ", nameof(DuplicateInfoNodeItem.ElementId), true));
        panelFactory.AppendChild(CreateColumnText(" Navn: ", nameof(DuplicateInfoNodeItem.Name), false));
        panelFactory.AppendChild(CreateColumnText(" Tag: ", nameof(DuplicateInfoNodeItem.Tag), false));
        panelFactory.AppendChild(CreateColumnText(" Mod: ", nameof(DuplicateInfoNodeItem.Modname), false));

        return new System.Windows.DataTemplate { VisualTree = panelFactory };
    }

    private static System.Windows.FrameworkElementFactory CreateColumnText(string prefix, string bindingPath, bool first)
    {
        var textFactory = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
        textFactory.SetValue(System.Windows.Controls.TextBlock.TextTrimmingProperty, System.Windows.TextTrimming.CharacterEllipsis);
        textFactory.SetValue(System.Windows.FrameworkElement.MarginProperty, first ? new System.Windows.Thickness(0, 0, 0, 0) : new System.Windows.Thickness(8, 0, 0, 0));

        var multiBinding = new System.Windows.Data.MultiBinding { StringFormat = $"{prefix}{{0}}" };
        multiBinding.Bindings.Add(new System.Windows.Data.Binding(bindingPath));
        textFactory.SetBinding(System.Windows.Controls.TextBlock.TextProperty, multiBinding);

        return textFactory;
    }
}
