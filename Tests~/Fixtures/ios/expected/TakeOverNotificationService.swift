// OpenPush took over this notification service extension (TakeOver). The source it replaced is in .openpush-backup/.
import OpenPushNSE
import UserNotifications

final class NotificationService: UNNotificationServiceExtension {
    private let openPush = OpenPushNotificationServiceHandler(options: {
        var options = OpenPushNotificationServiceHandler.Options()
        options.appGroup = "group.com.example.game.shared"
        return options
    }())

    override func didReceive(
        _ request: UNNotificationRequest,
        withContentHandler contentHandler: @escaping (UNNotificationContent) -> Void
    ) {
        openPush.didReceive(request, withContentHandler: contentHandler)
    }

    override func serviceExtensionTimeWillExpire() {
        openPush.serviceExtensionTimeWillExpire()
    }
}
