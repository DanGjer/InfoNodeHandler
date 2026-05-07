---
name: assistant-revit-extension-guide
description: Guidance for building Assistant Revit extensions in C#. Use this when implementing Args, Command, Result patterns, ValueCopy, Revit collectors, and UI control attributes.
---

# Assistant .NET Extension Development Guide for LLMs

Use this skill when implementing or updating C# Assistant extensions, especially Revit extensions.

## Core Concepts Overview

You are currently working on a Revit extension, but here is a brief overview of all extension types available in the Assistant ecosystem.
Each extension type is designed to run in a specific environment and perform specific tasks.
 
1. **Assistant Extensions**: For desktop automation, outside of any specific application
2. **Revit Extensions**: For Revit Automation
3. **Tekla Extensions**: For Tekla Structures automation
4. **AutoCAD Extensions**: For AutoCAD task automation
5. **Navisworks Extensions**: For Navisworks task automation

## Extension Development Framework

All extensions follow a consistent pattern with three key components:

1. **Args Class**: Defines input parameters and UI controls
2. **Command Class**: Implements the extension logic (IExtension interface)
3. **Result Class**: Standardizes the output format

## Revit Extension Development (Focus Area)

### Implementation Pattern

```csharp
// 1. Define Args class with input parameters
public class RevitExtensionArgs
{
    [Description("Parameter Name")]
    [ControlData(ToolTip = "Enter parameter name")]
    public string ParameterName { get; set; }
    
    // Value Copy functionality
    [ValueCopyCollector(typeof(ValueCopyRevitCollector))]
    public ValueCopy ValueCopy { get; set; }
}

// 2. Implement the extension logic
public class RevitExtensionCommand : IRevitExtension<RevitExtensionArgs>
{
    public IExtensionResult Run(IRevitExtensionContext context, RevitExtensionArgs args, CancellationToken cancellationToken)
    {
        var document = context.UIApplication.ActiveUIDocument?.Document;
        
        if (document is null)
            return Result.Text.Failed("Revit has no active model open");
            
        // Get selected elements in the model
        var selectedObjects = context.UIApplication.ActiveUIDocument.Selection.GetElementIds();
        
        // Create a transaction to modify the model
        using var transaction = new Transaction(document, "RevitExtension");
        transaction.Start();
        
        // Modify the model elements
        foreach (var elementId in selectedObjects)
        {
            var element = document.GetElement(elementId);
            // Perform operations on Revit elements
        }
        
        transaction.Commit();
        
        // Return success result
        return Result.Text.Succeeded("Operation completed successfully");
    }
}

// 3. Implementing ValueCopy functionality
public class ValueCopyRevitCollector : IValueCopyRevitCollector<RevitExtensionArgs>
{
    public ValueCopyRevitSources GetSources(IValueCopyRevitContext context, RevitExtensionArgs args)
    {
        var filter = new FilteredElementCollector(context.Document).WhereElementIsElementType();
        return new ValueCopyRevitSources(filter);
    }

    public ValueCopyRevitTargets GetTargets(IValueCopyRevitContext context, RevitExtensionArgs args)
    {
        var filter = new FilteredElementCollector(context.Document).WhereElementIsElementType();
        return new ValueCopyRevitTargets(filter);
    }
}
```

### Key Revit-Specific Features

#### ValueCopy Functionality

ValueCopy enables parameter and property value copying between Revit elements:

1. **Setup in Args**:
   ```csharp
   [ValueCopyCollector(typeof(ValueCopyRevitCollector))]
   public ValueCopy ValueCopy { get; set; }
   ```

2. **Filter Element Selection**:
   - Filter by element type: `.WhereElementIsElementType()`
   - Filter by category: `.OfCategory(BuiltInCategory.OST_Walls)`

3. **Using ValueCopy in Command**:
   ```csharp
   var valueCopyHandler = context.GetHandler(args.ValueCopy);
   
   // Copy between elements
   var result = valueCopyHandler.Handle(sourceElement, targetElement);
   
   // Copy within same element
   var result = valueCopyHandler.Handle(targetElement);
   
   // Copy to multiple targets
   var results = valueCopyHandler.Handle(sourceElement, targetElements);
   ```

4. **Exception Handling**:
   ```csharp
   throw new InvalidConfigurationException("Custom Error Message!");
   ```

#### Custom AutoFill Collectors

Implement intelligent parameter suggestions:

```csharp
public class ExtensionArgs
{
    [CustomRevitAutoFill(typeof(ParameterAutoFillCollector))]
    public List<string> ParameterNames { get; set; }
}

public class ParameterAutoFillCollector : IRevitAutoFillCollector<ExtensionArgs>
{
    public Dictionary<string, string> Get(UIApplication uiApplication, ExtensionArgs args)
    {
        var result = new Dictionary<string, string>();
        
        try
        {
            var document = uiApplication.ActiveUIDocument.Document;
            
            // Fetch parameter names from the model
            var parameterNames = new List<string> { "Parameter1", "Parameter2", "Parameter3" };
            
            foreach (var parameterName in parameterNames)
            {
                result.Add(parameterName, parameterName);
            }
        }
        catch (Exception e)
        {
            result.Add(string.Empty, $"Failed to get autofill: {e.Message}");
        }
        
        return result;
    }
}
```

## Working with Revit Elements

Common operations:

1. **Access Document**:
   ```csharp
   var document = context.UIApplication.ActiveUIDocument?.Document;
   ```

2. **Get Selected Elements**:
   ```csharp
   var selectedIds = context.UIApplication.ActiveUIDocument.Selection.GetElementIds();
   ```

3. **Element Collection**:
   ```csharp
   var collector = new FilteredElementCollector(document);
   var walls = collector.OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType();
   ```

4. **Transactions**:
   ```csharp
   using var transaction = new Transaction(document, "Description");
   transaction.Start();
   // Modify elements
   transaction.Commit();
   ```

5. **Parameter Access**:
   ```csharp
   var parameter = element.LookupParameter("ParameterName");
   var value = parameter.AsString(); // or AsDouble(), AsInteger(), etc.
   parameter.Set(newValue);
   ```

## Extension UI Controls and Attributes

### Basic Control Attributes
- `[Description("Label")]`: Sets field label
- `[ControlData(ToolTip = "Help text")]`: Adds tooltip
- `[Required]`: Makes input mandatory
- `[DefaultValue("Default")]`: Sets default value
- `[ControlSettings("PropertyName", "Value")]`: Configure control properties

### Control Types
- `[ControlType(ControlType.ComboBox)]`: Dropdown selection
- `[ControlType(ControlType.ListBox)]`: Multi-selection list
- `[ControlType(ControlType.Option)]`: Single-option selection
- `[ControlType(ControlType.RadioButton)]`: Radio button group
- `[ControlType(ControlType.Browse)]`: File browser dialog
- `[ControlType(ControlType.Save)]`: File save dialog
- `[ControlType(ControlType.ImageViewer)]`: Image display control
- `[ControlType(ControlType.Password)]`: Password input field
- `[ControlType(ControlType.Url)]`: URL input field

### File System Controls
- `[FileExtension("json")]`: Filter by file extension
- `[ControlSettings("SelectFolder", "true")]`: Configure for folder selection

### Text Input Customization
- `[ControlSettings("IsMultiline", "True")]`: Enable multi-line text input
- `[ControlSettings("MinLines", "5")]`: Set minimum lines for text area
- `[ControlSettings("MaxLines", "10")]`: Set maximum lines for text area
- `[ControlSettings("Foreground", "Red")]`: Change text color

### List Controls
- `[ControlSettings("CompactMode", "true")]`: Compact display for lists
- `[ControlSettings("MaxHeight", "200")]`: Set maximum height for list controls
- `[ControlSettings("Orientation", "Vertical")]`: Set orientation for radio buttons

### Date Controls
- `[ControlSettings("ShowTime", "true")]`: Configure date picker to include time

### Auto-Fill Sources
- `[CustomRevitAutoFill(typeof(CustomCollectorClass))]`: Custom Revit data collector
- `[CustomAutoFill(typeof(CustomCollectorClass))]`: Generic auto-fill collector
- `[RevitAutoFill(RevitAutoFillSource.Phases)]`: Use built-in Revit phases
- `[RevitAutoFill(RevitAutoFillSource.Categories)]`: Use Revit categories
- `[RevitAutoFill(RevitAutoFillSource.FamilyAndType)]`: Use family types
- `[RevitAutoFill(RevitAutoFillSource.ByCustomFilter)]`: Custom filtered auto-fill
- `[RevitAutoFill(RevitAutoFillSource.SharedParameters)]`: Use shared parameters
- `[RevitAutoFill(RevitAutoFillSource.ProjectParameters)]`: Use project parameters
- `[RevitAutoFill(RevitAutoFillSource.BuiltInParameters)]`: Use built-in parameters
- `[RevitAutoFill(RevitAutoFillSource.Views)]`: Use Revit views
- `[RevitAutoFill(RevitAutoFillSource.Sheets)]`: Use Revit sheets
- `[RevitAutoFill(RevitAutoFillSource.Worksets)]`: Use Revit worksets
- `[RevitAutoFill(RevitAutoFillSource.Families)]`: Use Revit families
- `[RevitAutoFill(RevitAutoFillSource.FamilySymbols)]`: Use Revit family symbols
- `[RevitAutoFill(RevitAutoFillSource.Filters)]`: Use Revit filters
- `[RevitAutoFill(RevitAutoFillSource.CableTraySizes)]`: Use cable tray sizes
- `[RevitAutoFill(RevitAutoFillSource.ConduitSizes)]`: Use conduit sizes
- `[RevitAutoFill(RevitAutoFillSource.PipeSizes)]`: Use pipe sizes
- `[RevitAutoFill(RevitAutoFillSource.DuctSizes)]`: Use duct sizes
- `[RevitAutoFill(RevitAutoFillSource.Levels)]`: Use Revit levels
- `[RevitAutoFill(RevitAutoFillSource.Grids)]`: Use Revit grids
- `[RevitAutoFill(RevitAutoFillSource.RevitType)]`: Use Revit types
- `[RevitAutoFill(RevitAutoFillSource.ParametersForCategory)]`: Use parameters for a category
- `[AutoFill(SortOrder = SortOrder.SortByAscending)]`: Sort auto-fill values

### Advanced Controls
- `[Authorization(Login.Autodesk)]`: Configure authorization for APIs
- `[BaseUrl("https://developer.api.autodesk.com/")]`: Set base URL for API client
- `public IExtensionHttpClient Client { get; set; }`: HTTP client for API calls
- `public FilteredElementCollector FilterControl { get; set; }`: Element filter control

### Element ID Access
- Use `int` type with RevitAutoFill to get ElementId
- Use `string` type with RevitAutoFill to get UniqueId

### Complex Data Types
- `public Dictionary<string, string> Dictionary { get; set; }`: Dictionary/key-value pair control
- `public List<CustomEnum> ListControl { get; set; }`: List of enum values
- `public List<string> StringList { get; set; }`: List of strings
- `public DateTime DateControl { get; set; }`: Date and time picker
- `public FilteredElementCollector FilterControl { get; set; }`: Element filter control
- `public ElementId ElementIdProperty { get; set; }`: Revit element ID
- `public List<ElementId> ElementIdList { get; set; }`: List of Revit element IDs

## Best Practices

1. Always check for null document/elements
2. Use transactions for all model modifications
3. Handle exceptions and provide clear error messages
4. Filter elements appropriately to improve performance
5. Use ValueCopy for complex parameter transfers
6. Include informative help documentation

### Supported Property Types for Args
The following property types are supported for extension input arguments and will be rendered as UI controls:

- `string`
- `int`
- `double`
- `bool`
- `DateTime`
- `enum` (including custom enums)
- `List<T>` (e.g., `List<string>`, `List<int>`, `List<CustomEnum>`)
- `Dictionary<string, string>`
- Custom types for advanced controls (e.g., `IExtensionHttpClient`, `FilteredElementCollector`)

Use these types when defining properties in your Args class to ensure proper UI generation.

#### Common ControlSettings Options by Property Type

| Property Type         | Common ControlSettings Options         | Example Usage                                      |
|---------------------- |---------------------------------------|----------------------------------------------------|
| string                | IsMultiline, MinLines, MaxLines, Foreground | `[ControlSettings("IsMultiline", "True")]`     |
| List<T>               | MaxHeight, CompactMode                | `[ControlSettings("MaxHeight", "200")]`        |
| DateTime              | ShowTime                              | `[ControlSettings("ShowTime", "true")]`        |
| enum                  | Orientation (for radio buttons)       | `[ControlSettings("Orientation", "Vertical")]` |
| int                   | (with autofill)                       | `[ControlSettings("MaxHeight", "200")]`        |
| Dictionary            | MaxHeight                             | `[ControlSettings("MaxHeight", "200")]`        |

Not all options are valid for all types. Refer to the examples above and use the options that make sense for your property type.

## Quick Troubleshooting

- **Nothing happens**: Check for errors in exception handling
- **Transaction issues**: Ensure Start() and Commit() are paired
- **Element not found**: Verify element selection filters
- **Parameter problems**: Confirm parameter existence and type match

## Example: Parameter Copy Extension

```csharp
public class ParameterCopyArgs
{
    [Description("Source Parameter")]
    [CustomRevitAutoFill(typeof(ParameterAutoFillCollector))]
    public string SourceParameter { get; set; }
    
    [Description("Target Parameter")]
    [CustomRevitAutoFill(typeof(ParameterAutoFillCollector))]
    public string TargetParameter { get; set; }
}

public class ParameterCopyCommand : IRevitExtension<ParameterCopyArgs>
{
    public IExtensionResult Run(IRevitExtensionContext context, ParameterCopyArgs args, CancellationToken cancellationToken)
    {
        var document = context.UIApplication.ActiveUIDocument?.Document;
        if (document == null) return Result.Text.Failed("No active document");
        
        var selectedIds = context.UIApplication.ActiveUIDocument.Selection.GetElementIds();
        if (!selectedIds.Any()) return Result.Text.Failed("No elements selected");
        
        using var transaction = new Transaction(document, "Parameter Copy");
        transaction.Start();
        
        int successCount = 0;
        foreach (var id in selectedIds)
        {
            var element = document.GetElement(id);
            var sourceParam = element.LookupParameter(args.SourceParameter);
            var targetParam = element.LookupParameter(args.TargetParameter);
            
            if (sourceParam != null && targetParam != null && sourceParam.StorageType == targetParam.StorageType)
            {
                switch (sourceParam.StorageType)
                {
                    case StorageType.String:
                        targetParam.Set(sourceParam.AsString());
                        break;
                    case StorageType.Double:
                        targetParam.Set(sourceParam.AsDouble());
                        break;
                    case StorageType.Integer:
                        targetParam.Set(sourceParam.AsInteger());
                        break;
                    case StorageType.ElementId:
                        targetParam.Set(sourceParam.AsElementId());
                        break;
                }
                successCount++;
            }
        }
        
        transaction.Commit();
        return Result.Text.Succeeded($"Copied parameter values for {successCount} elements");
    }
}
```

Examples of Args classes below:

```csharp
using CW.Assistant.Extensions.Assistant.Collectors;
using CW.Assistant.Extensions.Contracts.Enums;
using CW.Assistant.Extensions.Contracts.Fields;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace AssistantDemoExtension;

/// <summary>
/// Represents the inputs to an Assistant extension.
/// This class is used for defining the inputs required by the extension.
/// The properties in this class are parsed into UI elements in the Extension Task configuration in Assistant.
/// </summary>
public class AssistantDemoExtensionArgs
{
    [UrlField(
        Label = "Demo Extension GitHub URL",
        ToolTip = "Link to this demo extension")]
    public string ExtensionGitUrl { get; } = "https://github.com/AEC-AB/tools-extensions-public/blob/main/Assistant/dotnet/AssistantDemoExtension/AssistantDemoExtensionArgs.cs";

    [TextField(
       Label = "Text input",
       Hint = "Enter some text",
       ToolTip = """
        Its possible to add a tooltip to provide additional information about the input.
        You can use triple quotes to create multi-line tooltips.
        Create informative and user-friendly tooltips to enhance the user experience.
        """,
       HelperText = "The text must be clear and describe something")]
    [Required(ErrorMessage = "This field is required.")]
    public string Input { get; set; } = "Default input";

    [TextField(
        Label = "Multiline Text input",
        ToolTip = "Multiline text input lets you enter several lines of text.",
        IsMultiline = true,
        MinLines = 3,
        MaxLines = 6)]
    public string TextInputMultiline { get; set; } = """
        This is a sample multiline text input.
        You can enter multiple lines of text here.
        """;

    [TextField(
        Label = "Read-Only Text input",
        ToolTip = "Read-Only text input displays information that cannot be modified by the user."
    )]
    public string ReadOnlyTextInput { get; } = "This is a read-only text input.";

    [TextField(
        Label = "Text input with AutoFill",
        ToolTip = "Text input with AutoFill provides suggestions as you type.",
        CollectorType = typeof(CustomAutoFillCollector))]
    public string? AutoFillTextInput { get; set; }

    [OptionsField(
        Label = "Options field",
        ToolTip = "Options field provides a dropdown list of options populated by a custom collector.",
        CollectorType = typeof(CustomAutoFillCollector),
        CollectorSortOrder = SortOrder.SortByAscending
    )]
    public string? OptionsField { get; set; }

    [FilePickerField(
        Label = "Browse for File input",
        ToolTip = "Open file dialog control",
        Hint = "Select a JSON file",
        FileExtensions = ["json", "*"])]
    public string? BrowseForFile { get; set; }

    [FilePickerField(
        Label = "Browse for Multiple Files input",
        ToolTip = "Open file dialog control to select multiple files",
        FileExtensions = ["json", "*"])]
    public List<string> BrowseForMultipleFiles { get; set; } = [];

    [FolderPickerField(
        Label = "Browse for Directory input",
        ToolTip = "Open file dialog control to select a directory")]
    public string? BrowseForDirectory { get; set; }

    [FolderPickerField(
        Label = "Browse for Multiple Directories input",
        ToolTip = "Open file dialog control to select multiple directories")]
    public List<string> BrowseForMultipleDirectories { get; set; } = [];

    [SaveFileField(
        Label = "Save File input",
        Hint = "Save as JSON file",
        ToolTip = "Save file dialog control",
        FileExtensions = ["json", "*"])]
    public string? SaveFile { get; set; }

    [UrlField(
        Label = "URL input",
        Hint = "https://www.example.com",
        ToolTip = "URL input allows you to enter web addresses.")]
    [Url(ErrorMessage = "Please enter a valid URL.")]
    public string? UrlInput { get; set; }

    [IntegerField(
        Label = "Integer input",
        ToolTip = "Integer input allows you to enter whole numbers only.")]
    public int IntegerInput { get; set; } = 5;

    [IntegerField(
        Label = "Integer Slider input",
        ToolTip = "Integer Slider input allows you to select a value within a specified range using a slider control.",
        MinimumValue = 0,
        MaximumValue = 30,
        StepValue = 5)]
    public int IntegerSliderInput { get; set; } = 15;

    [DoubleField(
        Label = "Number input",
        ToolTip = "Number input allows you to enter numeric values only.")]
    public double NumberInput { get; set; } = 10.5;

    [BooleanField(
        Label = "Boolean input",
        ToolTip = "Boolean input represents a true/false value.")]
    public bool BooleanInput { get; set; }

    [DateTimeField(
        Label = "Date and Time Picker",
        ToolTip = "Date and time picker control for selecting both date and time values.",
        ShowTime = true)]
    public DateTime DateAndTime { get; set; }

    [DateTimeField(
        Label = "Date Only Picker",
        ToolTip = "Date only picker control for selecting date values without time.",
        ShowTime = false)]
    public DateTime DateOnly { get; set; }

    [OptionsField(
        Label = "ComboBox with custom enums",
        ToolTip = "ComboBox control with custom enums",
        CollectorSortOrder = SortOrder.None)]
    public CustomEnum CustomEnumControl { get; set; } = CustomEnum.Option1;

    [OptionsField(
        Label = "ListBox with custom enums",
        ToolTip = "ListBox control with custom enums",
        CollectorSortOrder = SortOrder.SortByDescending,
        MaxHeight = 200)]
    public List<CustomEnum> ListBoxWithEnum { get; set; } = [];

    [OptionsField(
        Label = "Compact ListBox with custom enums",
        ToolTip = "Compact ListBox control with custom enums",
        CompactMode = true,
        CollectorSortOrder = SortOrder.SortByAscending)]
    public List<CustomEnum> ListBoxWithEnumCompact { get; set; } = [];

    [ChoiceField(
        Label = "RadioButton with custom enums",
        ToolTip = "RadioButton control with custom enums")]
    [AllowedValues(nameof(CustomEnum.Option2), nameof(CustomEnum.Option3),
        ErrorMessage = "Please select either Option2 or Option3.")]
    public CustomEnum RadioButtonWithEnum { get; set; } = CustomEnum.Option2;

    [ChoiceField(
        Label = "Vertical RadioButton with custom enums",
        ToolTip = "Vertical RadioButton control with custom enums",
        Orientation = ChoiceOrientation.Vertical)]
    public CustomEnum RadioButtonVerticalWithEnum { get; set; } = CustomEnum.Option3;

    [ListField(
        Label = "String List input",
        ToolTip = "String List input allows you to enter multiple string values.")]
    public List<string> StringListInput { get; set; } = ["Item1", "Item2", "Item3"];

    [OptionsField(
        Label = "String List input with options",
        ToolTip = "String List input with predefined options for values.",
        CollectorType = typeof(CustomAutoFillCollector))]
    public List<string> StringListOptionsInput { get; set; } = [];

    [DictionaryField(
        Label = "Dictionary input",
        ToolTip = "Dictionary input allows you to enter key-value pairs.")]
    public Dictionary<string, string> DictionaryInput { get; set; } = [];

    [DictionaryField(
        Label = "Dictionary input with options",
        ToolTip = "Dictionary input with predefined options for values.",
        CollectorType = typeof(CustomAutoFillCollector))]
    public Dictionary<string, string> DictionaryWithOptionsInput { get; set; } = [];

    [PasswordField(
        Label = "Credentials for Application Id",
        ToolTip = "Select credentials stored in the Credential Manager for the specified Application Id.")]
    public string CredentialsForApplicationId { get; } = "TestApplication";

    [PasswordField(
        Label = "Credentials for Editable Application Id",
        ToolTip = "Select credentials stored in the Credential Manager for the specified Application Id.")]
    public string CredentialsForEditableApplicationId { get; set; } = "EditableApplication";

    [ColorField(
        Label = "Some color",
        ToolTip = "Color picker control to select a color.")]
    public System.Drawing.Color Color { get; set; } = System.Drawing.Color.Red;

    // Conditional visibility example fields
    [TextField(Label = "")]
    public string ConditionalVisibilityExamples { get; } = "*** This section demonstrates conditional visibility of fields based on user input. ***";

    [BooleanField(Label = "Show the text field by clicking this")]
    public bool ShowTextField { get; set; }

    [TextField(HelperText = "Write 'Apple' to show more options.",
        Visibility = nameof(ShowTextField),
        ToolTip = "This field is shown or hidden based on the 'Show Text Field' checkbox.")]
    [RegularExpression("Apple", ErrorMessage = "Please enter 'Apple' to proceed.")]
    public string? TextInput { get; set; }

    [OptionsField(HelperText = "Select Beta to get more options",
        Visibility = $"{nameof(ShowTextField)} && {nameof(TextInput)} == 'Apple'")]
    [RegularExpression("Beta", ErrorMessage = "Please select 'Beta' to proceed.")]
    public SampleEnum OptionsInput { get; set; }

    [IntegerField(HelperText = "Are you over 18?",
        Visibility = $"{nameof(OptionsInput)} == 'Beta'")]
    [Range(0, 120, ErrorMessage = "Please enter a valid age between 0 and 120.")]
    public int NumericInput { get; set; }

    [TextField(
        Visibility = $"{nameof(NumericInput)} >= 18 && {nameof(OptionsInput)} == 'Beta'")]
    public string Notification { get; } = "You are old enough!";

    [ListField(
        Label = "Add items to the list",
        HelperText = "Add at least 3 items to see a notification",
        Visibility = $"{nameof(TextInput)} == 'Apple'")]
    [MinLength(3, ErrorMessage = "Please add at least 3 items to the list.")]
    public List<string>? Items { get; set; }

    [TextField(
        Label = "List Count Notification",
        Visibility = $"{nameof(Items)}.Count > 2")]
    public string? MoreThanTwoItemsNotification { get; } = "You have added more than two items!";
}


/// <summary>
/// This class implements a custom AutoFill collector for providing dynamic options.
/// It generates a dictionary of key-value pairs to be used as options in the UI.
/// Use values from the args class to customize the options if needed.
/// </summary>
internal class CustomAutoFillCollector : IAsyncAutoFillCollector<AssistantDemoExtensionArgs>
{
    public Task<Dictionary<string, string>> Get(AssistantDemoExtensionArgs args, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>();
        for (int i = 1; i <= 5; i++)
        {
            result.Add($"Key{i}", $"Display value {i}");
        }
        return Task.FromResult(result);
    }
}

/// <summary>
/// This enum represents sample options for demonstration purposes.
/// </summary>
public enum SampleEnum
{
    Alpha,
    Beta,
    Gamma
}

/// <summary>
/// This enum represents custom options for demonstration purposes.
/// Descriptions are shown as user-friendly names in the UI.
/// </summary>
public enum CustomEnum
{
    [Description("Option 1")]
    Option1,

    [Description("Option 2")]
    Option2,

    [Description("Option 3")]
    Option3,

    [Description("Option 4")]
    Option4,

    [Description("Option 5")]
    Option5
}
```
