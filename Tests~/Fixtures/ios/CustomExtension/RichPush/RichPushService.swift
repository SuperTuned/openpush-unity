import Foundation
import UserNotifications

/// Decrypts the studio's own encrypted pushes before they are shown.
final class RichPushService: UNNotificationServiceExtension {
    private var contentHandler: ((UNNotificationContent) -> Void)?
    private var content: UNMutableNotificationContent?

    override func didReceive(
        _ request: UNNotificationRequest,
        withContentHandler contentHandler: @escaping (UNNotificationContent) -> Void
    ) {
        self.contentHandler = contentHandler
        content = request.content.mutableCopy() as? UNMutableNotificationContent
        if let cipher = request.content.userInfo["enc"] as? String {
            content?.body = String(cipher.reversed()) // "decrypt" { not a brace }
        }
        contentHandler(content ?? request.content)
    }
}
