# InfoNode Handler

## Description
InfoNode Handler is an automation extension for Revit that places and manages InfoNode family instances in your active model, based on subitem data from dRofus. It streamlines coordination between linked models and dRofus, ensuring that all relevant objects are represented and updated in your Revit project.

## Configuration

- **Dry Run**:  
  *Type*: Checkbox  
  *Default*: False  
  *Description*: When enabled, the extension simulates all actions without making any changes to your Revit model. Use this to preview what would happen before committing changes.

- **Ignore Host Model Name**:  
  *Type*: Checkbox  
  *Default*: False  
  *Description*: When enabled, the extension will include hosts even if their model name is missing in dRofus. This is useful for handling objects that lack proper model name data.

- **Host Occurrence Model Name**:  
  *Type*: Text  
  *Default*: `parent_occurrence_id_occurrence_data_17_11_11_10`  
  *Description*: The dRofus field used to identify the host model name for filtering. Change this if your dRofus setup uses a different field.

- **Host Item Data 1**:  
  *Type*: Text  
  *Default*: `parent_occurrence_id_article_id_dyn_article_13101110`  
  *Description*: The dRofus field for custom host data (e.g., type, classification). Adjust as needed for your workflow.

- **Host Item Data 2**:  
  *Type*: Text  
  *Default*: `parent_occurrence_id_article_id_dyn_article_13101211`  
  *Description*: Another dRofus field for additional host data. Configure to match your project requirements.

## Functionality

### Description
When you run InfoNode Handler, it:

1. Checks that the InfoNode family exists in your model.
2. Verifies that all required parameters are present.
3. Collects all linked model names and relevant family instances.
4. Queries dRofus for subitems (occurrences) matching your configuration.
5. Matches Revit family instances to dRofus hosts and prepares InfoNode placements.
6. If Dry Run is off, places or updates InfoNodes in your model and removes outdated ones.
7. If Dry Run is on, simulates all actions and provides a summary without making changes.

### How to Use

1. **Prerequisites**:  
   - Ensure your Revit model contains the InfoNode family.
   - Linked models should be loaded and have dRofus occurrence IDs assigned.

2. **Configuration**:  
   - Open the extension settings and adjust fields as needed.
   - Enable Dry Run to preview changes.
   - Enable Ignore Host Model Name if you want to include hosts with missing model names.

3. **Execution**:  
   - Run the extension from the Assistant platform or Revit.
   - Review the summary showing created, moved, updated, and deleted InfoNodes.

4. **Verification**:  
   - Check that InfoNodes are placed/updated as expected in your model.

### Visual Aids
*Add screenshots of the configuration UI, InfoNode placements in Revit, and the summary results here.*

## Troubleshooting

### Issue 1: "Required InfoNode family does not exist in the model!"
- **Causes**: The InfoNode family is missing or not loaded.
- **Solution**: Load the InfoNode family into your project and try again.
- **Resources**: [Revit Family Loading Guide]

### Issue 2: "One or more required parameters missing from the project"
- **Causes**: Your InfoNode family or instances are missing required parameters (InfoNode_hostdata, InfoNode_hostID, InfoNode_hostname, InfoNode_hosttag, InfoNode_modname, InfoNode_subs, InfoNode_hostdata2).
- **Solution**: Check parameter names in your family and ensure they match the configuration.
- **Resources**: [Revit Parameter Management]

### Issue 3: "One or more relevant links not loaded"
- **Causes**: InfoNodes reference models that aren't currently loaded, or model names don't match.
- **Solution**: Load all required linked models or enable "Ignore Host Model Name" for hosts with missing data.

### Issue 4: Duplicate warnings in summary
- **Causes**: Multiple family instances with the same dRofus occurrence ID exist in linked models.
- **Solution**: Review duplicates listed in the summary and remove redundant instances from your linked models.

## FAQ

- **Q: When should I use Dry Run mode?**  
  - **A:** Use Dry Run to preview all changes before modifying your model. This is especially useful for large projects or first-time runs.

- **Q: What does Ignore Host Model Name do?**  
  - **A:** It allows the extension to process hosts even if their model name is missing in dRofus, ensuring no objects are skipped due to incomplete data. InfoNodes for these hosts will have "Ingen data" as the model name.

- **Q: Can I customize which dRofus fields are used?**  
  - **A:** Yes, adjust the configuration fields in the extension settings to match your dRofus schema.

- **Q: What happens to old InfoNodes that no longer match?**  
  - **A:** They are automatically deleted by the "Great Purge" function to keep your model clean.

## Resources

- [dRofusClient Documentation](https://github.com/dRofus/dRofusClient)
- [Revit API Documentation](https://www.revitapidocs.com/)
- [Assistant Platform User Guide]
- [Sample Workflows and Tutorials]

## Support

For assistance or to report issues:
- Contact: DBGJ (listed in the extension authors)
- Issue tracking: [Your repository's issues page]
- Community: [Assistant Platform forums or Revit user groups]

## Version History

- **Version 0.1.3 - November 2025**
  - Migrated to dRofusClient NuGet package
  - Added Dry Run mode for safe simulation
  - Added Ignore Host Model Name option for incomplete data
  - Improved filtering and data matching logic
  - Enhanced error handling and validation
  - Removed debug file exports for cleaner execution

---

*This documentation was generated based on the extension's code structure.*