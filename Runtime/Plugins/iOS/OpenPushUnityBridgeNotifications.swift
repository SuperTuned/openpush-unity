import Foundation
import UIKit
import UserNotifications
import OpenPush
import OpenPushCore

// The notifications half of the Unity bridge: delegate-captured taps and foreground arrivals,
// host-forwarded payloads (IOpenPushNotifications.Process*), permission, in-app messages and
// Live Activities.

public typealias OpenPushUnityPresenter = @convention(c) (Int64, UInt64) -> Void

/// Foreground events the C# layer still has to answer (or may still display later).
final class OpenPushUnityForegroundDecisions: @unchecked Sendable {
    static let shared = OpenPushUnityForegroundDecisions()
    private let lock = NSLock()
    private var events: [String: (OpenPush.ForegroundEvent, Date)] = [:]
    private var next: Int64 = 0
    private let cap = 20
    private let lifetime: TimeInterval = 30

    func hold(_ event: OpenPush.ForegroundEvent) -> String {
        lock.lock(); defer { lock.unlock() }
        let now = Date()
        events = events.filter { now.timeIntervalSince($0.value.1) < lifetime }
        while events.count >= cap, let oldest = events.min(by: { $0.value.1 < $1.value.1 })?.key { events.removeValue(forKey: oldest) }
        next += 1
        let id = "fg-\(next)"
        events[id] = (event, now)
        return id
    }

    func take(_ id: String) -> OpenPush.ForegroundEvent? {
        lock.lock(); defer { lock.unlock() }
        return events.removeValue(forKey: id)?.0
    }
}

/// The .mm delegate hook's presenter: it holds the willPresent completion handlers by id.
final class OpenPushUnityPresenterBox: @unchecked Sendable {
    static let shared = OpenPushUnityPresenterBox()
    private let lock = NSLock()
    private var presenter: OpenPushUnityPresenter?
    func set(_ p: OpenPushUnityPresenter?) { lock.lock(); presenter = p; lock.unlock() }
    func present(_ id: Int64, _ options: UNNotificationPresentationOptions) {
        lock.lock(); let p = presenter; lock.unlock()
        p?(id, UInt64(options.rawValue))
    }
}

private let sink = OpenPushUnitySink.shared
private let decisions = OpenPushUnityForegroundDecisions.shared
private let presenterBox = OpenPushUnityPresenterBox.shared

enum OpenPushUnityNotificationHooks {
    static func install() {
        _ = OpenPush.Notifications.addPermissionObserver { granted in
            sink.emit("permission", OpenPushUnityJSON.string(["granted": granted]))
        }
        // Only the delegate hook reaches this listener (host-forwarded payloads never call the
        // native processForeground). Once C# listens, the answer is held for its decision (the
        // native SDK keeps the delegate completion open for up to 25 s). Before that nothing is
        // emitted: the native default (display) stands, and a foreground event is never queued
        // for a late replay.
        _ = OpenPush.Notifications.addForegroundLifecycleListener { event in
            guard sink.hasCallback else { return }
            event.preventDefault()
            sink.emit("foreground", OpenPushUnityJSON.string([
                "message_id": OpenPushUnityJSON.null(event.messageId),
                "notification": OpenPushUnityJSON.notification(event.notification),
                "data": OpenPushUnityJSON.data(event.notification),
                "completion_id": decisions.hold(event),
            ]))
        }
        _ = OpenPush.Notifications.addClickListener { click in
            emitClick(messageId: click.messageId, actionId: click.actionId, notification: click.notification,
                      actionURL: click.actionURL,
                      launchURLOpened: OpenPushUnityBridgeState.shared.launchURLWasOpened(
                          OpenPushUnityLaunchURL.key(click.messageId, click.actionId)))
        }
        _ = OpenPush.InAppMessages.addLifecycleListener { event in
            sink.emit("iam_lifecycle", OpenPushUnityJSON.string(["message_id": event.messageId, "phase": event.phase]))
        }
        _ = OpenPush.InAppMessages.addClickListener { event in
            sink.emit("iam_click", OpenPushUnityJSON.string([
                "message_id": event.messageId, "action_id": OpenPushUnityJSON.null(event.actionId),
                "url": OpenPushUnityJSON.null(event.url), "url_target": OpenPushUnityJSON.null(event.urlTarget),
                "close": event.closingMessage, "first_click": event.firstClick,
                "kind": OpenPushUnityJSON.null(inAppClickKind(event)),
            ]))
        }
    }

    /// The native click model carries url, actionId and closingMessage only: prompts, tags and
    /// outcomes run inside the native in-app module. So the kind is url > custom > close, and
    /// anything else is left for C# to derive ("click").
    static func inAppClickKind(_ event: OpenPush.InAppMessageClickEvent) -> String? {
        if let url = event.url, !url.isEmpty { return "url" }
        if let action = event.actionId, !action.isEmpty { return "custom" }
        return event.closingMessage ? "close" : nil
    }

    /// The one route a click takes to C#, for a delegate-captured tap and a host-forwarded one alike.
    /// `actionURL` is the tapped button's own link (native `ClickEvent.actionURL`).
    static func emitClick(messageId: String?, actionId: String?, notification: OpenPush.Notification,
                          actionURL: URL? = nil, launchURLOpened: Bool = false) {
        sink.emit("click", OpenPushUnityJSON.string([
            "message_id": OpenPushUnityJSON.null(messageId),
            "action_id": OpenPushUnityJSON.null(actionId),
            "url": OpenPushUnityJSON.null(notification.launchURL?.absoluteString),
            "action_url": OpenPushUnityJSON.null(actionURL?.absoluteString),
            "launch_url_opened": launchURLOpened,
            "notification": OpenPushUnityJSON.notification(notification),
            "data": OpenPushUnityJSON.data(notification),
        ]))
    }
}

/// Opens a tapped push's launch URL from the native tap itself, so a cold start does not hold the
/// URL until Unity has loaded and C# has initialized. A tap on an action button with its own link
/// opens that link instead; a button without one opens the launch URL as any tap does.
/// iOS foregrounds the app on any tap; the short delay lets that transition settle, since an open
/// issued during it can be dropped. The app's Info.plist key `OpenPushSuppressLaunchURLs` (written
/// from OpenPushSettings) turns it off. Any URL that parses is opened, as native iOS and Android do;
/// one iOS has no handler for is logged.
enum OpenPushUnityLaunchURL {
    static let suppressKey = "OpenPushSuppressLaunchURLs"

    static func key(_ messageId: String?, _ actionId: String?) -> String { "\(messageId ?? "")|\(actionId ?? "")" }

    static func openIfEnabled(_ response: UNNotificationResponse) {
        guard Bundle.main.object(forInfoDictionaryKey: suppressKey) as? Bool != true else { return }
        // A resent copy, or a second tap on a message already handled: the native SDK ignores the
        // click, so the URL must not open again either.
        guard !OpenPush.Notifications.isRepeatClick(response) else { return }
        let notification = OpenPush.Notification(userInfo: response.notification.request.content.userInfo)
        let actionId = response.actionIdentifier == UNNotificationDefaultActionIdentifier
            ? nil : response.actionIdentifier
        let buttonURL = actionId.flatMap { id in notification.actions.first { $0.id == id }?.url }
        guard let url = buttonURL ?? notification.launchURL else { return }
        OpenPushUnityBridgeState.shared.markLaunchURLOpened(key(notification.messageId, actionId))
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
            UIApplication.shared.open(url, options: [:]) { opened in
                if !opened { OpenPush.onLog("\u{2022} launch url could not be opened: \(url.absoluteString)") }
            }
        }
    }
}

/// A host-forwarded payload: a flat JSON string map, as the facade sends it.
private func openPushHostPayload(_ json: UnsafePointer<CChar>?) -> [AnyHashable: Any] {
    var payload: [AnyHashable: Any] = [:]
    for (key, value) in OpenPushUnityJSON.object(openPushString(json)) { payload[key] = value }
    return payload
}

private func openPushHostMessageId(_ payload: [AnyHashable: Any], _ what: String) -> String? {
    guard let messageId = OpenPush.messageID(from: payload) else {
        OpenPush.onLog("\u{2022} \(what) payload has no op_message_id \u{2014} not an OpenPush notification, ignored")
        return nil
    }
    return messageId
}

@_cdecl("openpush_unity_set_presenter")
public func openpush_unity_set_presenter(_ presenter: OpenPushUnityPresenter?) { presenterBox.set(presenter) }

/// A tap the .mm delegate hook captured: the native SDK posts the clicked receipt and raises the
/// click listener. A cold-start tap arrives before C# has called initialize, when the native SDK
/// would drop the receipt, so it is held (newest 10) and replayed right after initialize.
@_cdecl("openpush_unity_process_click")
public func openpush_unity_process_click(_ response: UnsafeRawPointer?) {
    guard let response else { return }
    let value = Unmanaged<UNNotificationResponse>.fromOpaque(response).takeUnretainedValue()
    OpenPushUnityLaunchURL.openIfEnabled(value)
    if OpenPush.isInitialized { OpenPush.Notifications.processClick(value); return }
    OpenPushUnityBridgeState.shared.heldResponses.hold(value)
    openPushReplayHeldNotifications()
}

/// A foreground arrival the .mm delegate hook captured; the answer goes back through the presenter.
@_cdecl("openpush_unity_process_foreground")
public func openpush_unity_process_foreground(_ notification: UnsafeRawPointer?, _ presentationId: Int64) {
    guard let notification else { presenterBox.present(presentationId, [.banner, .list, .sound]); return }
    let value = Unmanaged<UNNotification>.fromOpaque(notification).takeUnretainedValue()
    let initialized = OpenPush.isInitialized
    OpenPush.Notifications.processForeground(value) { options in presenterBox.present(presentationId, options) }
    // Before initialize the presentation is still answered now, but the received receipt would be
    // dropped: hold the payload and replay it as a delivery after initialize.
    if !initialized {
        OpenPushUnityBridgeState.shared.heldDeliveries.hold(value.request.content.userInfo)
        openPushReplayHeldNotifications()
    }
}

/// IOpenPushNotifications.ProcessForeground: the facade already ran the C# handlers, so this posts
/// the received receipt only. No foreground event, no completion id, no confirmed receipt.
@_cdecl("openpush_unity_process_foreground_data")
public func openpush_unity_process_foreground_data(_ userInfoJson: UnsafePointer<CChar>?) {
    guard let messageId = openPushHostMessageId(openPushHostPayload(userInfoJson), "forwarded foreground") else { return }
    OpenPush.receipt("received", messageID: messageId)
}

/// IOpenPushNotifications.ProcessClick: posts the clicked receipt and raises the click exactly once,
/// through the same emitter a captured tap uses. The native click listener is not involved, so the
/// click cannot reach C# twice.
@_cdecl("openpush_unity_process_click_data")
public func openpush_unity_process_click_data(_ userInfoJson: UnsafePointer<CChar>?) {
    let payload = openPushHostPayload(userInfoJson)
    guard let messageId = openPushHostMessageId(payload, "forwarded click") else { return }
    // Shares the native click history, so a resent copy or a repeat forward is raised only once.
    guard OpenPush.Notifications.recordClick(userInfo: payload) else {
        OpenPush.onLog("\u{2022} forwarded click \(messageId) ignored \u{2014} this message was already handled")
        return
    }
    OpenPush.receipt("clicked", messageID: messageId)
    OpenPushUnityNotificationHooks.emitClick(messageId: messageId, actionId: nil,
                                             notification: OpenPush.Notification(userInfo: payload))
}

/// IOpenPushNotifications.ProcessDisplayed: posts the confirmed receipt only.
@_cdecl("openpush_unity_process_displayed_data")
public func openpush_unity_process_displayed_data(_ userInfoJson: UnsafePointer<CChar>?) {
    guard let messageId = openPushHostMessageId(openPushHostPayload(userInfoJson), "forwarded display") else { return }
    OpenPush.receipt("confirmed", messageID: messageId)
}

/// C#'s answer to a held foreground event. display: false leaves it suppressed (the native SDK
/// resolves it as suppressed when its 25 s window ends) and keeps it for a later display.
@_cdecl("openpush_unity_complete_foreground")
public func openpush_unity_complete_foreground(_ completionId: UnsafePointer<CChar>?, _ display: Bool) {
    guard display, let id = openPushNonEmpty(completionId) else { return }
    decisions.take(id)?.display()
}

@_cdecl("openpush_unity_display_foreground")
public func openpush_unity_display_foreground(_ completionId: UnsafePointer<CChar>?) {
    guard let id = openPushNonEmpty(completionId) else { return }
    decisions.take(id)?.display()
}

@_cdecl("openpush_unity_permission")
public func openpush_unity_permission() -> Bool { OpenPush.Notifications.permission }

@_cdecl("openpush_unity_can_request_permission")
public func openpush_unity_can_request_permission() -> Bool { OpenPush.Notifications.canRequestPermission }

@_cdecl("openpush_unity_request_permission")
public func openpush_unity_request_permission(_ fallbackToSettings: Bool, _ correlationId: Int64) {
    OpenPush.Notifications.requestPermission(fallbackToSettings: fallbackToSettings, completion: { granted in
        sink.result(correlationId, ["granted": granted])
    })
}

@_cdecl("openpush_unity_request_provisional_permission")
public func openpush_unity_request_provisional_permission(_ correlationId: Int64) {
    OpenPush.Notifications.requestProvisionalPermission(completion: { granted in sink.result(correlationId, ["granted": granted]) })
}

@_cdecl("openpush_unity_clear_all")
public func openpush_unity_clear_all() { OpenPush.Notifications.clearAll() }

@_cdecl("openpush_unity_set_badge_count")
public func openpush_unity_set_badge_count(_ count: Int32) { OpenPush.Notifications.setBadgeCount(Int(max(0, count))) }

@_cdecl("openpush_unity_iam_set_paused")
public func openpush_unity_iam_set_paused(_ paused: Bool) { OpenPush.InAppMessages.paused = paused }

@_cdecl("openpush_unity_iam_paused")
public func openpush_unity_iam_paused() -> Bool { OpenPush.InAppMessages.paused }

@_cdecl("openpush_unity_iam_add_triggers")
public func openpush_unity_iam_add_triggers(_ json: UnsafePointer<CChar>?) {
    OpenPush.InAppMessages.addTriggers(OpenPushUnityJSON.object(openPushString(json)).filter { !($0.value is NSNull) })
}

@_cdecl("openpush_unity_iam_remove_triggers")
public func openpush_unity_iam_remove_triggers(_ keysJson: UnsafePointer<CChar>?) {
    OpenPush.InAppMessages.removeTriggers(OpenPushUnityJSON.list(openPushString(keysJson)))
}

@_cdecl("openpush_unity_iam_clear_triggers")
public func openpush_unity_iam_clear_triggers() { OpenPush.InAppMessages.clearTriggers() }

/// The iOS SDK has no public fetch: the native foreground refresh fetches when one is due.
@_cdecl("openpush_unity_iam_fetch")
public func openpush_unity_iam_fetch() { OpenPush.Notifications.onAppForegrounded() }

@_cdecl("openpush_unity_la_setup_default")
public func openpush_unity_la_setup_default(_ pushToStart: Bool, _ pushToUpdate: Bool) {
    if #available(iOS 16.1, *) {
        OpenPush.LiveActivities.setupDefault(options: .init(enablePushToStart: pushToStart, enablePushToUpdate: pushToUpdate))
    } else {
        sink.emit("log", OpenPushUnityJSON.string(["message": "\u{26a0} Live Activities need iOS 16.1"]))
    }
}

@_cdecl("openpush_unity_la_start_default")
public func openpush_unity_la_start_default(_ activityId: UnsafePointer<CChar>?, _ attributesJson: UnsafePointer<CChar>?,
                                            _ contentJson: UnsafePointer<CChar>?) {
    guard let activityId = openPushNonEmpty(activityId) else { return }
    if #available(iOS 16.1, *) {
        OpenPush.LiveActivities.startDefault(activityId,
                                             attributes: OpenPushUnityJSON.object(openPushString(attributesJson)),
                                             content: OpenPushUnityJSON.object(openPushString(contentJson)))
    } else {
        sink.emit("log", OpenPushUnityJSON.string(["message": "\u{26a0} Live Activities need iOS 16.1"]))
    }
}

@_cdecl("openpush_unity_la_enter")
public func openpush_unity_la_enter(_ activityId: UnsafePointer<CChar>?, _ token: UnsafePointer<CChar>?,
                                    _ activityType: UnsafePointer<CChar>?, _ correlationId: Int64) {
    let id = openPushString(activityId) ?? "", value = openPushString(token) ?? ""
    let done: @Sendable (Bool) -> Void = { ok in sink.result(correlationId, ["ok": ok]) }
    if let type = openPushNonEmpty(activityType) {
        OpenPush.LiveActivities.enter(id, withToken: value, activityType: type, completion: done)
    } else {
        OpenPush.LiveActivities.enter(id, withToken: value, completion: done)
    }
}

@_cdecl("openpush_unity_la_exit")
public func openpush_unity_la_exit(_ activityId: UnsafePointer<CChar>?, _ correlationId: Int64) {
    OpenPush.LiveActivities.exit(openPushString(activityId) ?? "") { ok in sink.result(correlationId, ["ok": ok]) }
}

@_cdecl("openpush_unity_la_set_push_to_start_token")
public func openpush_unity_la_set_push_to_start_token(_ activityType: UnsafePointer<CChar>?, _ token: UnsafePointer<CChar>?,
                                                      _ correlationId: Int64) {
    OpenPush.LiveActivities.setPushToStartToken(activityType: openPushString(activityType) ?? "",
                                                token: openPushString(token) ?? "") { ok in sink.result(correlationId, ["ok": ok]) }
}

@_cdecl("openpush_unity_la_remove_push_to_start_token")
public func openpush_unity_la_remove_push_to_start_token(_ activityType: UnsafePointer<CChar>?, _ correlationId: Int64) {
    guard #available(iOS 17.2, *) else {
        sink.emit("log", OpenPushUnityJSON.string(["message": "\u{26a0} push-to-start tokens need iOS 17.2"]))
        sink.result(correlationId, ["ok": false])
        return
    }
    do {
        try OpenPush.LiveActivities.removePushToStartToken(openPushString(activityType) ?? "")
        sink.result(correlationId, ["ok": true])
    } catch {
        sink.emit("log", OpenPushUnityJSON.string(["message": "\u{2717} removePushToStartToken failed: \(error)"]))
        sink.result(correlationId, ["ok": false])
    }
}
