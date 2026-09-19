namespace Sms.Api.Middleware;

internal static class UserActivityMessageFormatter
{
    private static readonly Dictionary<(string Controller, string Action), (string Success, string Failure)> Messages = new()
    {
        [("Messages", "Send")] = ("Sent an SMS message.", "Could not send an SMS message."),
        [("Messages", "GetById")] = ("Viewed an SMS message.", "Could not view the SMS message."),
        [("Messages", "GetStatusHistory")] = ("Viewed SMS delivery history.", "Could not view SMS delivery history."),
        [("Messages", "GetHistory")] = ("Viewed SMS history.", "Could not view SMS history."),
        [("Logs", "Get")] = ("Viewed activity logs.", "Could not view activity logs."),
        [("Overview", "Get")] = ("Viewed the account overview.", "Could not view the account overview."),
        [("Reports", "Sms")] = ("Viewed the SMS report.", "Could not view the SMS report."),
        [("TenantUsers", "List")] = ("Viewed tenant users.", "Could not view tenant users."),
        [("TenantUsers", "Create")] = ("Created a tenant user.", "Could not create the tenant user."),
        [("TenantUsers", "SetState")] = ("Updated a tenant user's status.", "Could not update the tenant user's status."),
        [("TenantUsers", "ResetPassword")] = ("Reset a tenant user's password.", "Could not reset the tenant user's password."),
        [("Alerts", "Rules")] = ("Viewed alert rules.", "Could not view alert rules."),
        [("Alerts", "CreateRule")] = ("Created an alert rule.", "Could not create the alert rule."),
        [("Alerts", "UpdateRule")] = ("Updated an alert rule.", "Could not update the alert rule."),
        [("Alerts", "DeleteRule")] = ("Deleted an alert rule.", "Could not delete the alert rule."),
        [("Alerts", "List")] = ("Viewed alerts.", "Could not view alerts."),
        [("Alerts", "MarkRead")] = ("Marked an alert as read.", "Could not mark the alert as read."),
        [("Alerts", "MarkAllRead")] = ("Marked all alerts as read.", "Could not mark all alerts as read."),
        [("OptOuts", "List")] = ("Viewed the SMS opt-out list.", "Could not view the SMS opt-out list."),
        [("OptOuts", "Add")] = ("Blocked a number from receiving SMS messages.", "Could not block the number."),
        [("OptOuts", "Import")] = ("Imported SMS opt-outs.", "Could not import SMS opt-outs."),
        [("OptOuts", "Export")] = ("Exported the SMS opt-out list.", "Could not export the SMS opt-out list."),
        [("OptOuts", "Remove")] = ("Removed a number from the SMS opt-out list.", "Could not remove the number from the SMS opt-out list.")
    };

    internal static string Format(string? controller, string? action, int statusCode)
    {
        var succeeded = statusCode < StatusCodes.Status400BadRequest;
        if (controller is not null && action is not null
            && Messages.TryGetValue((controller, action), out var message))
            return succeeded ? message.Success : message.Failure;

        return succeeded ? "Completed an account action." : "Could not complete an account action.";
    }
}
