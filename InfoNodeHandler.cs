using dRofusClient.Filters;
using dRofusClient.Enums;
using dRofusClient.Occurrences;
using InfoNodeHandler;

namespace InfoNode;

public class InfoNodeHandlerCommand : IRevitExtension<AssistantArgs>
{
    private sealed class OwnershipFailurePreprocessor : IFailuresPreprocessor
    {
        public bool HasOwnershipFailure { get; private set; }
        public string FailureDescription { get; private set; } = string.Empty;

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failureMessages = failuresAccessor.GetFailureMessages();
            foreach (var failureMessage in failureMessages)
            {
                var description = failureMessage.GetDescriptionText() ?? string.Empty;
                if (!IsOwnershipConflictMessage(description))
                    continue;

                HasOwnershipFailure = true;
                if (string.IsNullOrWhiteSpace(FailureDescription))
                    FailureDescription = description;

                return FailureProcessingResult.ProceedWithRollBack;
            }

            return FailureProcessingResult.Continue;
        }
    }

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

    private static ProgressUI.HostListItem ToHostListItem(Revit.ActualRevitHost host, HashSet<int>? duplicateIds = null)
    {
        var subItemDetails = (host.SubItems ?? new List<DrofusOccurrence>())
            .Select(s => $"{(string.IsNullOrWhiteSpace(s.SubIdNumber) ? "-" : s.SubIdNumber)} | {(string.IsNullOrWhiteSpace(s.SubItemName) ? "(unnamed)" : s.SubItemName)}")
            .ToList();

        bool isDuplicate = duplicateIds != null
            && duplicateIds.Contains(host.DrofusOccurrenceId)
            && host.Status == Revit.ActualHostStatus.Moved;

        return new ProgressUI.HostListItem
        {
            DrofusOccurrenceId = host.DrofusOccurrenceId.ToString(),
            Name = host.ItemName ?? string.Empty,
            Mod = host.Modname ?? string.Empty,
            Tag = host.Tag ?? string.Empty,
            SubItems = subItemDetails.Count.ToString(),
            SubItemDetails = subItemDetails,
            IsDuplicate = isDuplicate,
            DuplicateWarning = isDuplicate
                ? "This element is a duplicate and causes repeated move operations on every run."
                : string.Empty
        };
    }

    private static bool IsOwnershipConflictMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("owned by", StringComparison.OrdinalIgnoreCase)
            || message.Contains("another user", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cannot edit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("can't edit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || message.Contains("eies av", StringComparison.OrdinalIgnoreCase)
            || message.Contains("annen bruker", StringComparison.OrdinalIgnoreCase)
            || message.Contains("kan ikke redigere", StringComparison.OrdinalIgnoreCase)
            || message.Contains("tilgang", StringComparison.OrdinalIgnoreCase)
            || message.Contains("låst", StringComparison.OrdinalIgnoreCase)
            || message.Contains("laast", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildProgressBar(int current, int total, int width = 20)
    {
        if (total <= 0)
            return "[--------------------] 0%";

        var boundedCurrent = Math.Clamp(current, 0, total);
        var filled = (int)Math.Round((double)boundedCurrent / total * width, MidpointRounding.AwayFromZero);
        filled = Math.Clamp(filled, 0, width);
        var percent = (int)Math.Round((double)boundedCurrent / total * 100, MidpointRounding.AwayFromZero);

        return $"[{new string('#', filled)}{new string('-', width - filled)}] {percent}%";
    }

    public IExtensionResult Run(IRevitExtensionContext context, AssistantArgs args, CancellationToken cancellationToken)
    {
        var document = context.UIApplication.ActiveUIDocument?.Document;
        var progressUI = new ProgressUI("InfoNodes");
        var uiEventHandler = new InfoNodeUiExternalEventHandler(progressUI.AppendLog);
        var uiExternalEvent = ExternalEvent.Create(uiEventHandler);
        progressUI.Show();

        try
        {
            progressUI.AppendLog("Starting InfoNode script");

            if (document is null)
            {
                progressUI.AppendLog("Error: Revit has no active model open.");
                return Result.Text.Failed("Revit has no active model open");
            }

            progressUI.SetHostActions(
                item =>
                {
                    if (!int.TryParse(item.DrofusOccurrenceId, out var hostId))
                    {
                        progressUI.AppendLog($"Select failed: invalid InfoNode ID '{item.DrofusOccurrenceId}'.");
                        return;
                    }

                    uiEventHandler.Queue(hostId, HostUiActionType.Select);
                    var request = uiExternalEvent.Raise();
                    if (request != ExternalEventRequest.Accepted)
                        progressUI.AppendLog($"Select request for InfoNode {hostId} was not accepted ({request}).");
                },
                item =>
                {
                    if (!int.TryParse(item.DrofusOccurrenceId, out var hostId))
                    {
                        progressUI.AppendLog($"Jump failed: invalid InfoNode ID '{item.DrofusOccurrenceId}'.");
                        return;
                    }

                    uiEventHandler.Queue(hostId, HostUiActionType.JumpTo);
                    var request = uiExternalEvent.Raise();
                    if (request != ExternalEventRequest.Accepted)
                        progressUI.AppendLog($"Jump request for InfoNode {hostId} was not accepted ({request}).");
                });

            progressUI.AppendLog("Checking requirements...");

            bool pathcheckerResult = Requirements.PathChecker();
            if (!pathcheckerResult)
            {
                progressUI.AppendLog("Error: Required file paths were not found.");
                return Result.Text.Failed("Path to required InfoNode family or shared parameter file was not found.");
            }

            if (!Requirements.FamilyChecker(document))
            {
                progressUI.AppendLog("Family not found, attempting import...");
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

                progressUI.AppendLog("Family import succeeded.");
            }

            progressUI.AppendLog("Checking parameters...");
            string parameterCheckerResult = Requirements.ParameterChecker(document);
            if (!string.IsNullOrEmpty(parameterCheckerResult))
            {
                progressUI.AppendLog("Error: Parameters are missing.");
                return Result.Text.Failed($"One or more required parameters are missing from the project:\n{parameterCheckerResult}");
            }

            progressUI.AppendLog("Checking linked models...");
            string modelCheckerResult = Requirements.ModelChecker(document);
            if (!string.IsNullOrEmpty(modelCheckerResult))
            {
                progressUI.AppendLog($"Error: One or more links are not loaded: {modelCheckerResult}");
                return Result.Text.Failed($"One or more relevant links are not loaded:\n{modelCheckerResult}");
            }

            progressUI.AppendLog("Requirements OK");
            if (args.SubFilter == null || args.SubFilter.Count == 0)
            {
                progressUI.AppendLog("No filter set, fetching all subitems from dRofus");
            }
            else
            {
                progressUI.AppendLog("Filtering by: " + string.Join(", ", args.SubFilter));
            }
            progressUI.AppendLog("Fetching occurrences from dRofus...");

            var client = new dRofusClientFactory().Create(document);

            var filterSelect = Filter.And(Filter.Eq("is_sub_occurrence", true));

            if (args.SubFilter != null && args.SubFilter.Any())
            {
                filterSelect = Filter.And(
                    Filter.Eq("is_sub_occurrence", true),
                    Filter.In("article_sub_category_id_name", args.SubFilter.ToArray())
                );
            }

            var selectFields = new[] { "Id", "article_id_number", "article_id_name", "parent_occurrence_id_id", args.ParamHostOccModelName, "parent_occurrence_id_article_id_name", args.ParamHostData1, args.ParamHostData2, "parent_occurrence_id_classification_number", args.ParamHostData3, args.ParamHostData4, args.ParamHostData5 }
                .Where(f => !string.IsNullOrWhiteSpace(f)).ToArray();
            var querySubs = Query.List()
                .Select(selectFields)
                .Filter(filterSelect);
                

            var allOccurrences = client.GetOccurrences(querySubs);
            progressUI.AppendLog($"Fetched {allOccurrences.Count()} occurrences.");
            if (allOccurrences.Count() == 0)
            {
                progressUI.AppendLog("No results found in dRofus, check the filter");
                return Result.Text.Failed("Check filter");
            }

            progressUI.AppendLog("Mapping dRofus InfoNode data...");

            // Convert the new client occurrences to the same format as the old DrofusOccurrence objects
            var subsInDrofus = allOccurrences.Select(occ => new DrofusOccurrence
            {
                SubOccId = occ.Id ?? 0,
                SubIdNumber = occ.AdditionalProperties?.GetValueOrDefault("article_id_number")?.ToString(),
                SubItemName = occ.AdditionalProperties?.GetValueOrDefault("article_id_name")?.ToString(),
                HostOccId = int.TryParse(occ.AdditionalProperties?.GetValueOrDefault("parent_occurrence_id_id")?.ToString(), out var hostId) ? hostId : 0,
                HostOccModname = occ.AdditionalProperties?.GetValueOrDefault(args.ParamHostOccModelName)?.ToString(),
                HostItemName = occ.AdditionalProperties?.GetValueOrDefault("parent_occurrence_id_article_id_name")?.ToString(),
                HostData1 = occ.AdditionalProperties?.GetValueOrDefault(args.ParamHostData1)?.ToString(),
                HostData2 = occ.AdditionalProperties?.GetValueOrDefault(args.ParamHostData2)?.ToString(),
                HostData3 = occ.AdditionalProperties?.GetValueOrDefault(args.ParamHostData3)?.ToString(),
                HostData4 = occ.AdditionalProperties?.GetValueOrDefault(args.ParamHostData4)?.ToString(),
                HostData5 = occ.AdditionalProperties?.GetValueOrDefault(args.ParamHostData5)?.ToString(),
                HostOccTag = occ.AdditionalProperties?.GetValueOrDefault("parent_occurrence_id_classification_number")?.ToString()
            }).ToList();

            // Now use the exact same syntax as the original commented code
            var hostsInDrofus = subsInDrofus.GroupBy(o => o.HostOccId).Select(group => new DrofusHost
            {
                HostOccID = group.Key,
                HostItemName = group.First().HostItemName,
                HostData1 = group.First().HostData1?.ToString(),
                HostData2 = group.First().HostData2?.ToString(),
                HostData3 = group.First().HostData3?.ToString(),
                HostData4 = group.First().HostData4?.ToString(),
                HostData5 = group.First().HostData5?.ToString(),
                HostOccTag = group.First().HostOccTag,
                HostOccModname = group.First().HostOccModname?.ToString(),
                RevitModname = group.First().RevitModname,
                SubItems = group.ToList()
            }).ToList();

            progressUI.AppendLog("Collecting instances from linked models...");
            var instancesInRevit = Revit.CollectAllInstancesFromLinkedModels(document, args.OccurrenceIdParameterNames, args.IncludeLocalModel);

            progressUI.AppendLog($"Found {instancesInRevit.Count} instances in Revit.");

            progressUI.AppendLog("Matching dRofus InfoNodes with Revit instances...");

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
                        ItemData1 = matchingHost.HostData1,
                        ItemData2 = matchingHost.HostData2,
                        ItemData3 = matchingHost.HostData3,
                        ItemData4 = matchingHost.HostData4,
                        ItemData5 = matchingHost.HostData5,
                        Tag = matchingHost.HostOccTag,
                        Modname = string.IsNullOrWhiteSpace(matchingHost.HostOccModname) ? matchingHost.RevitModname : matchingHost.HostOccModname,
                        SubItems = matchingHost.SubItems,
                    });
                }
            }

            var activeRevitHosts = Revit.ActualRevitHosts;
            var hostCollections = new HostCollections(activeRevitHosts);

            var seenIds = new HashSet<int>();
            var duplicateIdSet = hostCollections.All
                .Where(h => !seenIds.Add(h.DrofusOccurrenceId))
                .Select(h => h.DrofusOccurrenceId)
                .ToHashSet();

            int totalHosts = activeRevitHosts.Count;
            progressUI.SetHostProviders(
                () => hostCollections.All.Select(h => ToHostListItem(h, duplicateIdSet)),
                () => hostCollections.Created.Select(h => ToHostListItem(h, duplicateIdSet)),
                () => hostCollections.Moved.Select(h => ToHostListItem(h, duplicateIdSet)),
                () => hostCollections.Updated.Select(h => ToHostListItem(h, duplicateIdSet)));
            progressUI.AppendLog($"Matched {totalHosts} InfoNodes. Starting placement...");

            if (!args.DryRun)
            {
                using (var tx = new Transaction(document, "Place or update InfoNodes"))
                {
                    var ownershipFailurePreprocessor = new OwnershipFailurePreprocessor();
                    var failureHandlingOptions = tx.GetFailureHandlingOptions();
                    failureHandlingOptions.SetFailuresPreprocessor(ownershipFailurePreprocessor);
                    failureHandlingOptions.SetClearAfterRollback(true);
                    tx.SetFailureHandlingOptions(failureHandlingOptions);

                    tx.Start();

                    int processed = 0;
                    foreach (var host in activeRevitHosts)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int current = processed + 1;
                        progressUI.UpdateProgressLine($"Processing {current}/{totalHosts} {BuildProgressBar(current, totalHosts)}");

                        try
                        {
                            Revit.PlaceOrUpdateInfoNode(document, host, args.DryRun, args.RevitPhases, args.RevitWorkset);
                            processed++;
                        }
                        catch (Exception ex) when (IsOwnershipConflictMessage(ex.Message))
                        {
                            tx.RollBack();
                            progressUI.AppendLog($"Error: Could not edit InfoNode {host.DrofusOccurrenceId}.");
                            progressUI.AppendLog($"Reason: {ex.Message}");
                            progressUI.AppendLog("Please ask a colleague to sync...");
                            return Result.Text.Failed($"Ownership lock: {ex.Message}\n\nPlease ask a colleague to sync or request edit access to InfoNode {host.DrofusOccurrenceId}.");
                        }
                    }

                    var commitStatus = tx.Commit();
                    if (ownershipFailurePreprocessor.HasOwnershipFailure || commitStatus != TransactionStatus.Committed)
                    {
                        var reason = string.IsNullOrWhiteSpace(ownershipFailurePreprocessor.FailureDescription)
                            ? "Revit aborted the transaction due to access/ownership."
                            : ownershipFailurePreprocessor.FailureDescription;

                        progressUI.AppendLog("Error: Could not complete placement/update because one or more elements are locked by another user.");
                        progressUI.AppendLog($"Reason: {reason}");
                        progressUI.AppendLog("Ask a colleague to sync...");
                        return Result.Text.Failed($"Ownership lock: {reason}\n\nPlease ask a colleague to sync or request edit access.");
                    }

                    progressUI.AppendLog($"Placed/updated {processed} InfoNodes.");
                }
            }
            else
            {
                int processed = 0;
                foreach (var host in activeRevitHosts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int current = processed + 1;
                    progressUI.UpdateProgressLine($"Processing {current}/{totalHosts} {BuildProgressBar(current, totalHosts)}");

                    try
                    {
                        Revit.PlaceOrUpdateInfoNode(document, host, args.DryRun, args.RevitPhases, args.RevitWorkset);
                        processed++;
                    }
                    catch (Exception ex) when (IsOwnershipConflictMessage(ex.Message))
                    {
                        progressUI.AppendLog($"Error: Could not edit InfoNode {host.DrofusOccurrenceId}.");
                        progressUI.AppendLog($"Reason: {ex.Message}");
                        progressUI.AppendLog("Please ask a colleague to sync or request edit access.");
                        return Result.Text.Failed($"Ownership lock: {ex.Message}\n\nPlease ask a colleague to sync or request edit access to InfoNode {host.DrofusOccurrenceId}.");
                    }
                }
                progressUI.AppendLog($"Dry run: evaluated {processed} InfoNodes.");
            }

            var createdIDs = new List<int>();
            var createdNames = new List<string>();
            var movedIDs = new List<int>();
            var movedNames = new List<string>();
            var duplicateIDs = new List<int>();
            var duplicateNames = new List<string>();

            createdIDs.AddRange(hostCollections.Created.Select(h => h.DrofusOccurrenceId));
            createdNames.AddRange(hostCollections.Created.Select(h => h.ItemName ?? string.Empty));
            int createdCount = createdIDs.Count;

            movedIDs.AddRange(hostCollections.Moved.Select(h => h.DrofusOccurrenceId).Distinct());
            movedNames.AddRange(hostCollections.Moved.Select(h => h.ItemName ?? string.Empty).Distinct());
            int movedCount = movedIDs.Count;

            int updatedCount = hostCollections.Updated.Count();
            var deletedCount = Revit.TheGreatPurge(document, activeRevitHosts, args.DryRun);
            progressUI.AppendLog($"Deleted {deletedCount} InfoNodes.");

            duplicateIDs.AddRange(duplicateIdSet.OrderBy(id => id));
            duplicateNames.AddRange(hostCollections.All
                .Where(h => duplicateIdSet.Contains(h.DrofusOccurrenceId))
                .Select(h => h.ItemName ?? string.Empty)
                .Distinct());

            progressUI.AppendLog("Finalizing summary...");

            string dryRunPrefix = args.DryRun ? "[DRY RUN] " : "";
            string summarySuccess = ($"{dryRunPrefix}Success!\n\nCreated {createdCount} InfoNodes for these hosts: \n({String.Join(", ", createdIDs)})\nHost names: \n({String.Join(", ", createdNames)})\n\nMoved {movedCount} InfoNodes for these hosts: \n({String.Join(", ", movedIDs)})\nHost names: \n({String.Join(", ", movedNames)})\n\nUpdated {updatedCount} InfoNodes\n\nDeleted {deletedCount} InfoNodes");
            string summaryPartial = ($"{dryRunPrefix}Duplicates detected!\nThese duplicates exist in one of the linked models and confuse the script, triggering move operations on every run\nDuplicate IDs: \n({String.Join(", ", duplicateIDs)})\nDuplicate names: \n({String.Join(", ", duplicateNames)})\n\nCreated {createdCount} InfoNodes for these hosts: \n({String.Join(", ", createdIDs)})\nHost names: \n({String.Join(", ", createdNames)})\n\nMoved {movedCount} InfoNodes for these hosts: \n({String.Join(", ", movedIDs)})\nHost names: \n({String.Join(", ", movedNames)})\n\nUpdated {updatedCount} InfoNodes\nDeleted {deletedCount} InfoNodes");

            progressUI.AppendLog("Completed.");

            return duplicateIDs.Count > 0
                ? Result.Text.PartiallySucceeded(summaryPartial)
                : Result.Text.Succeeded(summarySuccess);
        }
        finally
        {
            progressUI.Complete();
        }
    }
}