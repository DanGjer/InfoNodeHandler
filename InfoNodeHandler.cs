using dRofusClient.Filters;
using dRofusClient.Enums;
using dRofusClient.Occurrences;
using InfoNodeHandler;

namespace InfoNode;

public class InfoNodeHandlerCommand : IRevitExtension<AssistantArgs>
{
    private sealed class HostCollections
    {
        private readonly IReadOnlyList<Revit.ActualRevitHost> _hosts;

        public HostCollections(IReadOnlyList<Revit.ActualRevitHost> hosts)
        {
            _hosts = hosts;
        }

        public IEnumerable<Revit.ActualRevitHost> All => _hosts;
        public IEnumerable<Revit.ActualRevitHost> Created => _hosts.Where(h => h.Status == Revit.ActualHostStatus.Created);
        public IEnumerable<Revit.ActualRevitHost> Moved => _hosts.Where(h => h.Status == Revit.ActualHostStatus.Moved);
        public IEnumerable<Revit.ActualRevitHost> Updated => _hosts.Where(h => h.Status == Revit.ActualHostStatus.Updated);
    }

    private static ProgressUI.HostListItem ToHostListItem(Revit.ActualRevitHost host)
    {
        return new ProgressUI.HostListItem
        {
            DrofusOccurrenceId = host.DrofusOccurrenceId.ToString(),
            Name = host.ItemName ?? string.Empty,
            Mod = host.Modname ?? string.Empty,
            Tag = host.Tag ?? string.Empty,
            SubItems = (host.SubItems?.Count ?? 0).ToString()
        };
    }

    public IExtensionResult Run(IRevitExtensionContext context, AssistantArgs args, CancellationToken cancellationToken)
    {
        var document = context.UIApplication.ActiveUIDocument?.Document;
        var progressUI = new ProgressUI("InfoNode Processing...");
        progressUI.Show();

        try
        {
            progressUI.AppendLog("Starting InfoNode processing...");

            if (document is null)
            {
                progressUI.AppendLog("Error: Revit has no active model open.");
                return Result.Text.Failed("Revit has no active model open");
            }

            progressUI.AppendLog("Checking requirements...");

            bool pathcheckerResult = Requirements.PathChecker();
            if (!pathcheckerResult)
            {
                progressUI.AppendLog("Error: Paths to required files not found.");
                return Result.Text.Failed("Paths to required InfoNode family or shared parameter file not found.");
            }

            if (!Requirements.FamilyChecker(document))
            {
                progressUI.AppendLog("Family not found, attempting to import...");
                if (!Requirements.FamilyImporter(document, out var importError))
                {
                    var reason = string.IsNullOrWhiteSpace(importError)
                        ? "Required InfoNode family could not be loaded into the model."
                        : $"Required InfoNode family could not be loaded into the model: {importError}";

                    progressUI.AppendLog("Error: " + reason);
                    return Result.Text.Failed(reason);
                }

                if (!Requirements.FamilyChecker(document))
                {
                    progressUI.AppendLog("Error: Family still not found after import.");
                    return Result.Text.Failed("Required InfoNode family does not exist in the model after attempted import.");
                }

                progressUI.AppendLog("Family imported successfully.");
            }

            progressUI.AppendLog("Checking parameters...");
            string parameterCheckerResult = Requirements.ParameterChecker(document);
            if (!string.IsNullOrEmpty(parameterCheckerResult))
            {
                progressUI.AppendLog("Error: Missing parameters.");
                return Result.Text.Failed($"One or more required parameters missing from the project:\n{parameterCheckerResult}");
            }

            progressUI.AppendLog("Checking linked models...");
            string modelCheckerResult = Requirements.ModelChecker(document, args.IgnoredRevitLinks);
            if (!string.IsNullOrEmpty(modelCheckerResult))
            {
                progressUI.AppendLog("Error: Links not loaded.");
                return Result.Text.Failed($"One or more relevant links not loaded:\n{modelCheckerResult}");
            }

            progressUI.AppendLog("Requirements OK");
            progressUI.AppendLog("Fetching occurrences from dRofus...");

            var client = new dRofusClientFactory().Create(document);

            // Build query with filters: is_sub_occurrence = true
            // Note: We no longer filter by host model name because IFC-imported elements won't have a model name in dRofus
            var querySubs = Query.List()
                .Select("Id", "article_id_number", "article_id_name", "parent_occurrence_id_id", args.ParamHostOccModelName, "parent_occurrence_id_article_id_name", args.ParamHostItemData1, args.ParamHostItemData2, "parent_occurrence_id_classification_number")
                .Filter(Filter.Eq("is_sub_occurrence", true));

            var allOccurrences = client.GetOccurrences(querySubs);
            progressUI.AppendLog($"Fetched {allOccurrences.Count()} occurrences.");

            progressUI.AppendLog("Mapping dRofus host data...");

            // Convert the new client occurrences to the same format as the old DrofusOccurrence objects
            var subsInDrofus = allOccurrences.Select(occ => new DrofusOccurrence
            {
                SubOccId = occ.Id ?? 0,
                SubIdNumber = occ.AdditionalProperties?.GetValueOrDefault("article_id_number")?.ToString(),
                SubItemName = occ.AdditionalProperties?.GetValueOrDefault("article_id_name")?.ToString(),
                HostOccId = int.TryParse(occ.AdditionalProperties?.GetValueOrDefault("parent_occurrence_id_id")?.ToString(), out var hostId) ? hostId : 0,
                HostOccModname = occ.AdditionalProperties?.GetValueOrDefault(args.ParamHostOccModelName)?.ToString(),
                HostItemName = occ.AdditionalProperties?.GetValueOrDefault("parent_occurrence_id_article_id_name")?.ToString(),
                HostOccDyn1 = occ.AdditionalProperties?.GetValueOrDefault(args.ParamHostItemData1)?.ToString(),
                HostItemDyn2 = occ.AdditionalProperties?.GetValueOrDefault(args.ParamHostItemData2)?.ToString(),
                HostOccTag = occ.AdditionalProperties?.GetValueOrDefault("parent_occurrence_id_classification_number")?.ToString()
            }).ToList();

            // Now use the exact same syntax as the original commented code
            var hostsInDrofus = subsInDrofus.GroupBy(o => o.HostOccId).Select(group => new DrofusHost
            {
                HostOccID = group.Key,
                HostItemName = group.First().HostItemName,
                HostItemData1 = group.First().HostOccDyn1?.ToString(),
                HostItemData2 = group.First().HostItemDyn2?.ToString(),
                HostOccTag = group.First().HostOccTag,
                HostOccModname = group.First().HostOccModname?.ToString(),
                RevitModname = group.First().RevitModname,
                SubItems = group.ToList()
            }).ToList();

            progressUI.AppendLog("Collecting instances from linked models...");
            var instancesInRevit = Revit.CollectAllInstancesFromLinkedModels(document, args.OccurrenceIdParameterNames, args.IgnoredRevitLinks);
            progressUI.AppendLog($"Found {instancesInRevit.Count} instances in Revit.");

            progressUI.AppendLog("Matching dRofus hosts to Revit instances...");

            // Clear hosts to avoid using stale data from previous runs
            Revit.ActualRevitHosts.Clear();

            foreach (var instance in instancesInRevit)
            {
                int occurrenceId = instance.DrofusOccurrenceId;
                var matchingHost = hostsInDrofus.FirstOrDefault(h => h.HostOccID == occurrenceId);

                if (matchingHost != null)
                {
                    // Set RevitModname from the instance so it can be used as fallback if dRofus modname is empty
                    matchingHost.RevitModname = instance.RevitModname;

                    Revit.ActualRevitHosts.Add(new Revit.ActualRevitHost
                    {
                        DrofusOccurrenceId = instance.DrofusOccurrenceId,
                        Position = instance.Position,
                        ItemName = matchingHost.HostItemName,
                        ItemData1 = matchingHost.HostItemData1,
                        ItemData2 = matchingHost.HostItemData2,
                        Tag = matchingHost.HostOccTag,
                        Modname = string.IsNullOrWhiteSpace(matchingHost.HostOccModname) ? matchingHost.RevitModname : matchingHost.HostOccModname,
                        SubItems = matchingHost.SubItems,
                    });
                }
            }

            var activeRevitHosts = Revit.ActualRevitHosts;
            var hostCollections = new HostCollections(activeRevitHosts);
            int totalHosts = activeRevitHosts.Count;
            progressUI.SetHostProviders(
                () => hostCollections.All.Select(ToHostListItem),
                () => hostCollections.Created.Select(ToHostListItem),
                () => hostCollections.Moved.Select(ToHostListItem),
                () => hostCollections.Updated.Select(ToHostListItem));
            progressUI.AppendLog($"Matched {totalHosts} hosts. Starting placement...");

            if (!args.DryRun)
            {
                using (var tx = new Transaction(document, "Place or update Infonodes"))
                {
                    tx.Start();

                    int processed = 0;
                    foreach (var host in activeRevitHosts)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Revit.PlaceOrUpdateInfoNode(document, host, args.DryRun, args.RevitPhases, args.RevitWorkset);
                        processed++;
                    }

                    tx.Commit();
                    progressUI.AppendLog($"Placed/updated {processed} Infonodes.");
                }
            }
            else
            {
                int processed = 0;
                foreach (var host in activeRevitHosts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Revit.PlaceOrUpdateInfoNode(document, host, args.DryRun, args.RevitPhases, args.RevitWorkset);
                    processed++;
                }
                progressUI.AppendLog($"Dry run: evaluated {processed} Infonodes.");
            }

            var createdIDs = new List<int>();
            var createdNames = new List<string>();
            var movedIDs = new List<int>();
            var movedNames = new List<string>();
            var duplicateIDs = new List<int>();
            var duplicateNames = new List<string>();
            var seenIds = new HashSet<int>();

            createdIDs.AddRange(hostCollections.Created.Select(h => h.DrofusOccurrenceId));
            createdNames.AddRange(hostCollections.Created.Select(h => h.ItemName ?? string.Empty));
            int createdCount = createdIDs.Count;

            movedIDs.AddRange(hostCollections.Moved.Select(h => h.DrofusOccurrenceId).Distinct());
            movedNames.AddRange(hostCollections.Moved.Select(h => h.ItemName ?? string.Empty).Distinct());
            int movedCount = movedIDs.Count;

            int updatedCount = hostCollections.Updated.Count();
            var deletedCount = Revit.TheGreatPurge(document, activeRevitHosts, args.DryRun);

            foreach (var host in hostCollections.All)
            {
                if (!seenIds.Add(host.DrofusOccurrenceId))
                {
                    if (!duplicateIDs.Contains(host.DrofusOccurrenceId))
                        duplicateIDs.Add(host.DrofusOccurrenceId);
                    if (!duplicateNames.Contains(host.ItemName ?? string.Empty))
                        duplicateNames.Add(host.ItemName ?? string.Empty);
                }
            }

            progressUI.AppendLog("Finalizing summary...");

            string dryRunPrefix = args.DryRun ? "[DRY RUN]\n " : "";
            string summarySuccess = ($"{dryRunPrefix}Success!\n\nCreated {createdCount} Infonodes for these hosts: \n({String.Join(", ", createdIDs)})\nHost names: \n({String.Join(", ", createdNames)})\n\nMoved {movedCount} Infonodes for these hosts: \n({String.Join(", ", movedIDs)})\nHost names: \n({String.Join(", ", movedNames)})\n\nUpdated {updatedCount} Infonodes\n\nDeleted {deletedCount} Infonodes");
            string summaryPartial = ($"{dryRunPrefix}Duplicates detected!\nThese duplicates exist in one of the linked models and confuse the script, triggering move ops for each run\nDuplicate IDs: \n({String.Join(", ", duplicateIDs)})\nDuplicate names: \n({String.Join(", ", duplicateNames)})\n\nCreated {createdCount} Infonodes for these hosts: \n({String.Join(", ", createdIDs)})\nHost names: \n({String.Join(", ", createdNames)})\n\nMoved {movedCount} Infonodes for these hosts: \n({String.Join(", ", movedIDs)})\nHost names: \n({String.Join(", ", movedNames)})\n\nUpdated {updatedCount} Infonodes\nDeleted {deletedCount} Infonodes");

            progressUI.AppendLog("Completed.");

            return duplicateIDs.Count > 0
                ? Result.Text.PartiallySucceeded(summaryPartial)
                : Result.Text.Succeeded(summarySuccess);
        }
        finally
        {
            progressUI.Close();
        }
    }
}