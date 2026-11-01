import Foundation
import UserNotifications
import OpenPushNSE

/// Decrypts the studio's own encrypted pushes before they are shown.
final class RichPushService: UNNotificationServiceExtension {
    private let openPush = OpenPushNotificationServiceHandler(options: {
        var options = OpenPushNotificationServiceHandler.Options()
        options.appGroup = "group.com.example.game.shared"
        return options
    }())

    override func serviceExtensionTimeWillExpire() {
        if openPush.serviceExtensionTimeWillExpireIfHandling() { return }
        super.serviceExtensionTimeWillExpire()
    }

    private var contentHandler: ((UNNotificationContent) -> Void)?
    private var content: UNMutableNotificationContent?

    override func didReceive(
        _ request: UNNotificationRequest,
        withContentHandler contentHandler: @escaping (UNNotificationContent) -> Void
    ) {
        if openPush.handleIfOpenPush(request, withContentHandler: contentHandler) { return }
        self.contentHandler = contentHandler
        content = request.content.mutableCopy() as? UNMutableNotificationContent
        if let cipher = request.content.userInfo["enc"] as? String {
            content?.body = String(cipher.reversed()) // "decrypt" { not a brace }
        }
        contentHandler(content ?? request.content)
    }
}
