namespace SISLAB.SharedKernel.Authorization;

/// <summary>
/// Permission-code catalogue for the Notifications bounded context.
/// See <see cref="InventoryPermissions"/> for the <c>&lt;Controller&gt;.&lt;Action&gt;</c> convention.
/// </summary>
public static class NotificationsPermissions
{
    /// <summary>Write permissions on <c>NotificationsController</c> (prefix <c>Notifications</c>).</summary>
    public static class Notifications
    {
        /// <summary>Mark a notification as read (POST). Any operating member may act on their alerts.</summary>
        public const string MarkAsRead = "Notifications.MarkAsRead";
    }

    /// <summary>Every Notifications write permission.</summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>
    {
        Notifications.MarkAsRead
    };
}
