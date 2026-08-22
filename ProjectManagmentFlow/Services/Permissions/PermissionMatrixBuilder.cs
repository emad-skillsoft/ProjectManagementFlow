using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.ViewModels;

namespace ProjectManagmentFlow.Services.Permissions;

public static class PermissionMatrixBuilder
{
    private static readonly string[] OperationOrder = ["view", "create", "edit", "delete", "manage"];

    public static (PermissionMatrixViewModel Matrix, RolePanelViewModel Panel) Build(
        IStringLocalizer text,
        string roleName,
        int holderCount,
        IReadOnlyList<PermissionChoice> permissions,
        bool canManage)
    {
        var parsed = permissions
            .Select(permission =>
            {
                var separator = permission.Name.IndexOf(':');
                return separator <= 0
                    ? (Service: permission.Name, Operation: string.Empty, Choice: permission)
                    : (Service: permission.Name[..separator],
                       Operation: permission.Name[(separator + 1)..],
                       Choice: permission);
            })
            .ToList();

        var operationKeys = parsed
            .Select(item => item.Operation)
            .Distinct()
            .OrderBy(key => Array.IndexOf(OperationOrder, key) is var index && index >= 0 ? index : int.MaxValue)
            .ThenBy(key => key, StringComparer.Ordinal)
            .ToList();

        var rows = new List<PermissionRowViewModel>();
        var previewLines = new List<RolePreviewLineViewModel>();

        foreach (var group in parsed.GroupBy(item => item.Service).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var serviceName = Translate(text, $"Service_{group.Key}", group.Key);
            var cells = new List<PermissionCellViewModel>();

            foreach (var operationKey in operationKeys)
            {
                var match = group.FirstOrDefault(item => item.Operation == operationKey);

                cells.Add(match.Choice is null
                    ? new PermissionCellViewModel
                    {
                        OperationKey = operationKey,
                        State = PermissionCellState.NotApplicable,
                        Label = text["Perm_NotApplicable"]
                    }
                    : new PermissionCellViewModel
                    {
                        PermissionId = match.Choice.Id,
                        OperationKey = operationKey,
                        State = match.Choice.IsGranted ? PermissionCellState.Granted : PermissionCellState.Denied,
                        Label = $"{serviceName} — {OperationLabel(text, operationKey)}"
                    });
            }

            var applicable = cells.Where(cell => cell.State != PermissionCellState.NotApplicable).ToList();
            var grantedCount = applicable.Count(cell => cell.State == PermissionCellState.Granted);

            rows.Add(new PermissionRowViewModel
            {
                Key = group.Key,
                Name = serviceName,
                Tag = applicable.Count == 1 && applicable[0].OperationKey == "view"
                    ? text["Perm_ReadOnlyTag"].Value
                    : null,
                ToggleLabel = text["Perm_ToggleRow", serviceName],
                GrantedCountLabel = $"{grantedCount}/{applicable.Count}",
                State = StateOf(grantedCount, applicable.Count),
                Cells = cells
            });

            if (grantedCount > 0)
            {
                previewLines.Add(new RolePreviewLineViewModel
                {
                    Service = serviceName,
                    Operations = string.Join(
                        text["Perm_ListSeparator"].Value,
                        applicable
                            .Where(cell => cell.State == PermissionCellState.Granted)
                            .Select(cell => OperationLabel(text, cell.OperationKey)))
                });
            }
        }

        var operations = operationKeys.Select(key =>
        {
            var label = OperationLabel(text, key);
            var column = rows
                .SelectMany(row => row.Cells)
                .Where(cell => cell.OperationKey == key && cell.State != PermissionCellState.NotApplicable)
                .ToList();

            return new PermissionOperationViewModel
            {
                Key = key,
                Label = label,
                ToggleLabel = text["Perm_ToggleColumn", label],
                State = StateOf(column.Count(cell => cell.State == PermissionCellState.Granted), column.Count)
            };
        }).ToList();

        var totalGranted = permissions.Count(permission => permission.IsGranted);

        var matrix = new PermissionMatrixViewModel
        {
            Label             = text["Perm_MatrixLabel"],
            Title             = text["RolePerms_Title", roleName],
            Subtitle          = text["Perm_MatrixSubtitle", holderCount],
            ServiceLabel      = text["Perm_Service"],
            RowToggleLabel    = text["Perm_EntireRow"],
            NotApplicableLabel = text["Perm_NotApplicable"],
            NotApplicableHint = text["Perm_NotApplicableHint"],
            IsReadOnly        = !canManage,
            Operations        = operations,
            Rows              = rows,
            Legend =
            [
                new() { State = PermissionCellState.Granted, Label = text["Perm_LegendGranted"] },
                new() { State = PermissionCellState.Denied, Label = text["Perm_LegendDenied"] },
                new() { State = PermissionCellState.NotApplicable, Label = text["Perm_LegendNotApplicable"] }
            ]
        };

        var panel = new RolePanelViewModel
        {
            Label               = text["Perm_RolePanel"],
            NameLabel           = text["Perm_RoleName"],
            Name                = roleName,
            PreviewTitle        = text["Perm_PreviewTitle"],
            OperationCount      = totalGranted,
            OperationCountLabel = text["Perm_OperationCountLabel"],
            EmptyPreviewLabel   = text["Perm_PreviewEmpty"],
            SaveLabel           = text["RolePerms_Save"],
            ResetLabel          = text["Perm_Reset"],
            IsSaveDisabled      = !canManage,
            PreviewLines        = previewLines
        };

        return (matrix, panel);
    }

    private static PermissionCellState StateOf(int granted, int total) => granted switch
    {
        0 => PermissionCellState.Denied,
        _ when granted == total => PermissionCellState.Granted,
        _ => PermissionCellState.Mixed
    };

    private static string OperationLabel(IStringLocalizer text, string key) =>
        Translate(text, $"Operation_{key}", key);

    private static string Translate(IStringLocalizer text, string key, string fallback)
    {
        var translated = text[key];
        return translated.ResourceNotFound ? fallback : translated.Value;
    }
}
