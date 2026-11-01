#import <Foundation/Foundation.h>
#import <UserNotifications/UserNotifications.h>

// Clears notifications this app has already delivered. UNUserNotificationCenter
// only ever reports and removes this app's own notifications, so there is no way
// for these calls to reach another app's notification centre entries. Pending
// (scheduled) local notifications are deliberately left alone: the host owns
// those, and OpenPush never schedules any.

static NSString *OpenPushMessageIdOf(UNNotification *notification) {
    id raw = notification.request.content.userInfo[@"op_message_id"];
    return raw == nil ? nil : [raw description];
}

extern "C" {

void _openpush_clear_all_notifications(void) {
    // The notification centre is a main-thread-affine UI surface on some iOS
    // releases, and Unity calls this from the scripting thread.
    dispatch_async(dispatch_get_main_queue(), ^{
        [[UNUserNotificationCenter currentNotificationCenter] removeAllDeliveredNotifications];
    });
}

void _openpush_remove_notification(const char *messageId) {
    if (messageId == NULL) return;
    NSString *wanted = [NSString stringWithUTF8String:messageId];
    if (wanted.length == 0) return;

    UNUserNotificationCenter *center = [UNUserNotificationCenter currentNotificationCenter];
    [center getDeliveredNotificationsWithCompletionHandler:^(
        NSArray<UNNotification *> *delivered) {
        NSMutableArray<NSString *> *identifiers = [NSMutableArray array];
        for (UNNotification *notification in delivered) {
            NSString *messageIdentifier = OpenPushMessageIdOf(notification);
            if (messageIdentifier != nil && [messageIdentifier isEqualToString:wanted])
                [identifiers addObject:notification.request.identifier];
        }
        if (identifiers.count == 0) return;
        // The completion handler runs on a private queue.
        dispatch_async(dispatch_get_main_queue(), ^{
            [center removeDeliveredNotificationsWithIdentifiers:identifiers];
        });
    }];
}

}
