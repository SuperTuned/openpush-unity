import Foundation
import UIKit
import UserNotifications
import OpenPush
import OpenPushCore

// The Unity package's bridge over the native OpenPush iOS SDK, compiled inside UnityFramework.
// C# calls @_cdecl functions with UTF-8 strings; every native callback reaches C# through one
// registered C function (kind, json), always on the main queue. Returned strings are strdup'd and
// freed by IL2CPP.

public typealias OpenPushUnityEventCallback = @convention(c) (UnsafePointer<CChar>, UnsafePointer<CChar>) -> Void

/// The one route to C#. The callback and the pre-registration queue are touched only on the main
/// queue, so a replay on registration and a live event can never overtake each other.
final class OpenPushUnitySink: @unchecked Sendable {
    static let shared = OpenPushUnitySink()
    private let lock = NSLock()
    private var callback: OpenPushUnityEventCallback?
    private var pending: [(String, String)] = []
    private let pendingCap = 50

    var hasCallback: Bool { lock.lock(); defer { lock.unlock() }; return callback != nil }

    func set(_ cb: OpenPushUnityEventCallback?) {
        lock.lock(); callback = cb; lock.unlock()
        guard cb != nil else { return }
        DispatchQueue.main.async { [self] in
            // Replay in arrival order, before any event emitted after this point.
            while true {
                lock.lock()
                let current = callback
                let next = pending.isEmpty || current == nil ? nil : pending.removeFirst()
                lock.unlock()
                guard let cb = current, let (kind, json) = next else { return }
                kind.withCString { k in json.withCString { j in cb(k, j) } }
            }
        }
    }

    /// Events raised before C# registers are queued (newest 50) and replayed on registration.
    func emit(_ kind: String, _ json: String) {
        DispatchQueue.main.async { [self] in
            lock.lock()
            let cb = pending.isEmpty ? callback : nil
            if cb == nil {
                if pending.count == pendingCap { pending.removeFirst() }
                pending.append((kind, json))
            }
            lock.unlock()
            guard let cb else { return }
            kind.withCString { k in json.withCString { j in cb(k, j) } }
        }
    }

    func result(_ correlationId: Int64, _ payload: [String: Any]) {
        emit("result", OpenPushUnityJSON.string(["correlation_id": NSNumber(value: correlationId), "payload": payload]))
    }
}

enum OpenPushUnityJSON {
    static func string(_ object: [String: Any]) -> String {
        guard JSONSerialization.isValidJSONObject(object),
              let data = try? JSONSerialization.data(withJSONObject: object),
              let text = String(data: data, encoding: .utf8) else {
            // Never silent: C# would otherwise receive an empty event with no hint why.
            OpenPush.onLog("\u{2717} bridge payload could not be serialised to JSON (keys: " +
                           object.keys.sorted().joined(separator: ", ") + ") \u{2014} C# receives {}")
            return "{}"
        }
        return text
    }

    static func object(_ text: String?) -> [String: Any] {
        guard let text, let data = text.data(using: .utf8),
              let parsed = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return [:] }
        return parsed
    }

    static func strings(_ text: String?) -> [String: String] {
        var map: [String: String] = [:]
        for (key, value) in object(text) where !(value is NSNull) { map[key] = "\(value)" }
        return map
    }

    static func list(_ text: String?) -> [String] {
        guard let text, let data = text.data(using: .utf8),
              let parsed = try? JSONSerialization.jsonObject(with: data) as? [Any] else { return [] }
        return parsed.compactMap { $0 as? String }
    }

    static func null(_ value: Any?) -> Any { value ?? NSNull() }

    static func subscription(_ state: OpenPush.PushSubscriptionState?) -> Any {
        guard let state else { return NSNull() }
        return [
            "id": null(state.id), "token": null(state.token), "opted_in": state.optedIn,
            "status": state.status.map { NSNumber(value: $0) } ?? NSNull(),
        ] as [String: Any]
    }

    /// A top-level payload value as text: strings as-is, numbers and booleans spelled out.
    static func text(_ n: OpenPush.Notification, _ key: String) -> String? {
        switch n.rawPayload[key] {
        case .string(let value)?: return value
        case .number(let value)?: return value == value.rounded() && abs(value) < 1e15 ? String(Int64(value)) : String(value)
        case .bool(let value)?: return value ? "true" : "false"
        default: return nil
        }
    }

    /// Every field NotificationWire reads, under its snake_case wire name. The op_* keys are the
    /// same ones OpenPushNotification.From reads; aps-derived fields come from the native model.
    static func notification(_ n: OpenPush.Notification) -> [String: Any] {
        let ttl = text(n, "op_ttl").flatMap { Int($0) }
        let system = text(n, "op_system_message").map { $0 != "0" && $0.lowercased() != "false" } ?? false
        var android: [String: String] = [:]
        for key in n.rawPayload.keys where key.hasPrefix("op_android_") && key.count > "op_android_".count {
            if let value = text(n, key) { android[String(key.dropFirst("op_android_".count))] = value }
        }
        return [
            "message_id": null(n.messageId), "title": null(n.title), "body": null(n.body),
            "image_url": null(n.imageURL?.absoluteString), "launch_url": null(n.launchURL?.absoluteString),
            "collapse_id": null(text(n, "op_collapse_id") ?? n.threadId),
            "priority": null(text(n, "op_priority")),
            "action_category": null(text(n, "op_action_category") ?? n.category),
            "app_id": null(text(n, "op_app")),
            "ttl_seconds": ttl.map { NSNumber(value: $0) } ?? NSNull(),
            "system_message": system,
            "actions": n.actions.map { ["id": $0.id, "label": $0.label, "icon": $0.icon, "system_icon": $0.systemIcon,
                                    "url": null($0.url?.absoluteString)] as [String: Any] },
            "android": android,
            "data": data(n),
        ]
    }

    /// The whole userInfo with nesting intact (C# flattens nested values to JSON text).
    static func data(_ n: OpenPush.Notification) -> [String: Any] { n.rawPayload.mapValues { $0.anyValue } }

    static func identity(_ result: OpenPush.IdentityResult) -> [String: Any] {
        switch result {
        case .success: return ["success": true, "identity_rejected_note": NSNull(), "error": NSNull()]
        case .identityRejected(let note): return ["success": false, "identity_rejected_note": note, "error": NSNull()]
        case .failed(let message): return ["success": false, "identity_rejected_note": NSNull(), "error": message]
        @unknown default: return ["success": false, "identity_rejected_note": NSNull(), "error": "unknown identity result"]
        }
    }
}

func openPushString(_ pointer: UnsafePointer<CChar>?) -> String? { pointer.map { String(cString: $0) } }
func openPushNonEmpty(_ pointer: UnsafePointer<CChar>?) -> String? { openPushString(pointer).flatMap { $0.isEmpty ? nil : $0 } }
func openPushDup(_ value: String?) -> UnsafeMutablePointer<CChar>? { value.flatMap { strdup($0) } }

/// State shared by the bridge's entry points, guarded by one lock.
final class OpenPushUnityBridgeState: @unchecked Sendable {
    static let shared = OpenPushUnityBridgeState()
    private let lock = NSLock()
    private var credential: String?
    private var heldToken: Data?
    private var hooksInstalled = false

    var currentCredential: String? { lock.lock(); defer { lock.unlock() }; return credential }
    func setCredential(_ value: String?) { lock.lock(); credential = value; lock.unlock() }

    /// A token that arrives before initialize (the permission alert can delay C# boot) is held.
    func holdToken(_ token: Data) { lock.lock(); heldToken = token; lock.unlock() }
    func takeHeldToken() -> Data? { lock.lock(); defer { heldToken = nil; lock.unlock() }; return heldToken }

    /// Delegate-captured taps and deliveries that arrived before initialize (cold start); replayed
    /// into the native SDK right after it, so their clicked/received receipts are not dropped.
    let heldResponses = OpenPushUnityHeldQueue<UNNotificationResponse>(cap: 10)
    let heldDeliveries = OpenPushUnityHeldQueue<[AnyHashable: Any]>(cap: 10)

    /// Taps whose launch URL the bridge opened, keyed "messageId|actionId", so the click that
    /// reaches C# (possibly much later, after initialize) can say so. Bounded like the held queues.
    private var openedLaunchURLs: [String] = []
    func markLaunchURLOpened(_ key: String) {
        lock.lock(); defer { lock.unlock() }
        openedLaunchURLs.append(key)
        if openedLaunchURLs.count > 32 { openedLaunchURLs.removeFirst() }
    }
    func launchURLWasOpened(_ key: String) -> Bool {
        lock.lock(); defer { lock.unlock() }; return openedLaunchURLs.contains(key)
    }

    func installHooksOnce() -> Bool {
        lock.lock(); defer { lock.unlock() }
        if hooksInstalled { return false }
        hooksInstalled = true
        return true
    }
}

private let sink = OpenPushUnitySink.shared
private let state = OpenPushUnityBridgeState.shared

@_cdecl("openpush_unity_set_event_callback")
public func openpush_unity_set_event_callback(_ cb: OpenPushUnityEventCallback?) { sink.set(cb) }

@_cdecl("openpush_unity_sdk_version")
public func openpush_unity_sdk_version() -> UnsafeMutablePointer<CChar>? { strdup(OpenPush.sdkVersion) }

@_cdecl("openpush_unity_initialize")
public func openpush_unity_initialize(_ appId: UnsafePointer<CChar>?, _ sdkKey: UnsafePointer<CChar>?,
                                      _ serverUrl: UnsafePointer<CChar>?, _ appGroup: UnsafePointer<CChar>?,
                                      _ wrapperVersion: UnsafePointer<CChar>?, _ credential: UnsafePointer<CChar>?,
                                      _ provisional: Bool) {
    guard let appId = openPushNonEmpty(appId), let sdkKey = openPushNonEmpty(sdkKey) else {
        sink.emit("registration_failed", OpenPushUnityJSON.string(["message": "\u{2717} initialize refused \u{2014} empty app id or SDK key"]))
        return
    }
    state.setCredential(openPushString(credential))
    if state.installHooksOnce() {
        OpenPush.onLog = { message in sink.emit("log", OpenPushUnityJSON.string(["message": message])) }
        OpenPush.onRegistered = { sink.emit("registered", "{}") }
        OpenPush.onRegistrationFailed = { message in sink.emit("registration_failed", OpenPushUnityJSON.string(["message": message])) }
        OpenPush.onIdentityVerificationRequired = { externalId in
            sink.emit("identity_verification_required", OpenPushUnityJSON.string(["external_id": externalId]))
        }
        _ = OpenPush.PushSubscription.addChangeObserver { change in
            sink.emit("push_subscription", OpenPushUnityJSON.string([
                "previous": OpenPushUnityJSON.subscription(change.previous),
                "current": OpenPushUnityJSON.subscription(change.current),
            ]))
        }
        OpenPushUnityNotificationHooks.install()
    }
    // The bridge opens launch URLs itself, from the tap (OpenPushUnityLaunchURL, governed by
    // OpenPushSettings.OpenLaunchUrls through the Info.plist key), so the native opener (on
    // by default) is turned off before the native SDK initializes: with both on, one tap would open
    // its URL twice. Tests~/Native/verify-ios-launch-url-ownership.sh holds this.
    OpenPush.Notifications.openLaunchURLs = false
    // The wrapper must be set before initialize (OpenPushInitialize.swift).
    let version = openPushNonEmpty(wrapperVersion) ?? "unknown"
    OpenPush.wrapper = .init(name: "unity", version: version)
    OpenPush.initialize(appId: appId, sdkKey: sdkKey,
                        serverURL: openPushNonEmpty(serverUrl) ?? OpenPush.defaultServerURL,
                        appGroup: openPushNonEmpty(appGroup),
                        credential: { state.currentCredential },
                        provisional: provisional)
    if OpenPush.isInitialized, let token = state.takeHeldToken() {
        OpenPush.didRegisterForRemoteNotifications(deviceToken: token)
    }
    openPushReplayHeldNotifications()
}

/// Replays what the delegate hooks captured before initialize: deliveries (received receipt), then
/// taps (clicked receipt + the click listener, installed above). Each item is replayed once.
func openPushReplayHeldNotifications() {
    guard OpenPush.isInitialized else { return }
    for payload in state.heldDeliveries.drain() { _ = OpenPush.didReceiveRemoteNotification(userInfo: payload) }
    for response in state.heldResponses.drain() { OpenPush.Notifications.processClick(response) }
}

@_cdecl("openpush_unity_set_credential")
public func openpush_unity_set_credential(_ credential: UnsafePointer<CChar>?) { state.setCredential(openPushString(credential)) }

@_cdecl("openpush_unity_is_initialized")
public func openpush_unity_is_initialized() -> Bool { OpenPush.isInitialized }

@_cdecl("openpush_unity_current_token")
public func openpush_unity_current_token() -> UnsafeMutablePointer<CChar>? { openPushDup(OpenPush.currentToken) }

/// A provider token from C#. An external id is attached only through login: with one, the native
/// two-argument form logs a new id in first (no auth hash), which is what `OpenPush.Register(token, id)`
/// has always meant in C#; without one, the token registers under the current login.
@_cdecl("openpush_unity_register")
public func openpush_unity_register(_ token: UnsafePointer<CChar>?, _ externalId: UnsafePointer<CChar>?) {
    guard let token = openPushNonEmpty(token) else { return }
    guard let externalId = openPushNonEmpty(externalId) else { OpenPush.register(token: token); return }
    (OpenPush.self as OpenPushUnityRegisterWithExternalID.Type).register(token: token, externalID: externalId)
}

/// The native `register(token:externalID:)` is deprecated for hosts, which call `login` and
/// `register(token:)`. C#'s two-argument Register still maps to it, so it is reached through this
/// conformance: the bridge keeps building without a deprecation warning in every exported project.
protocol OpenPushUnityRegisterWithExternalID {
    static func register(token: String, externalID: String?)
}

extension OpenPush: OpenPushUnityRegisterWithExternalID {}

@_cdecl("openpush_unity_receipt")
public func openpush_unity_receipt(_ type: UnsafePointer<CChar>?, _ messageId: UnsafePointer<CChar>?, _ correlationId: Int64) {
    guard let type = openPushNonEmpty(type) else { sink.result(correlationId, [:]); return }
    OpenPush.receipt(type, messageID: openPushNonEmpty(messageId)) { sink.result(correlationId, [:]) }
}

@_cdecl("openpush_unity_track_event")
public func openpush_unity_track_event(_ name: UnsafePointer<CChar>?, _ propertiesJson: UnsafePointer<CChar>?) {
    guard let name = openPushNonEmpty(name) else { return }
    OpenPush.trackEvent(name, properties: OpenPushUnityJSON.object(openPushString(propertiesJson)))
}

@_cdecl("openpush_unity_login")
public func openpush_unity_login(_ externalId: UnsafePointer<CChar>?, _ authHash: UnsafePointer<CChar>?, _ correlationId: Int64) {
    OpenPush.login(externalId: openPushString(externalId) ?? "", authHash: openPushNonEmpty(authHash)) { result in
        sink.result(correlationId, OpenPushUnityJSON.identity(result))
    }
}

@_cdecl("openpush_unity_logout")
public func openpush_unity_logout(_ correlationId: Int64) {
    OpenPush.logout { result in sink.result(correlationId, OpenPushUnityJSON.identity(result)) }
}

@_cdecl("openpush_unity_on_app_foregrounded")
public func openpush_unity_on_app_foregrounded() { OpenPush.Notifications.onAppForegrounded() }

@_cdecl("openpush_unity_did_register_token")
public func openpush_unity_did_register_token(_ bytes: UnsafeRawPointer?, _ length: Int) {
    guard let bytes, length > 0 else { return }
    let token = Data(bytes: bytes, count: length)
    if OpenPush.isInitialized { OpenPush.didRegisterForRemoteNotifications(deviceToken: token); return }
    state.holdToken(token)
    // initialize may have finished between the check and the hold; whoever takes the token applies it.
    if OpenPush.isInitialized, let held = state.takeHeldToken() {
        OpenPush.didRegisterForRemoteNotifications(deviceToken: held)
    }
}

@_cdecl("openpush_unity_did_fail_to_register")
public func openpush_unity_did_fail_to_register(_ message: UnsafePointer<CChar>?) {
    let text = openPushString(message) ?? "unknown"
    OpenPush.didFailToRegisterForRemoteNotifications(
        error: NSError(domain: "OpenPushUnity", code: 3000, userInfo: [NSLocalizedDescriptionKey: text]))
}

@_cdecl("openpush_unity_did_receive_remote_notification")
public func openpush_unity_did_receive_remote_notification(_ userInfo: UnsafeRawPointer?) {
    guard let userInfo else { return }
    let dictionary = Unmanaged<NSDictionary>.fromOpaque(userInfo).takeUnretainedValue()
    var payload: [AnyHashable: Any] = [:]
    for (key, value) in dictionary { if let key = key as? AnyHashable { payload[key] = value } }
    if OpenPush.isInitialized { _ = OpenPush.didReceiveRemoteNotification(userInfo: payload); return }
    // Before initialize the receipt would be dropped: hold it (initialize may finish in between,
    // so replay once more; whoever drains an item replays it).
    state.heldDeliveries.hold(payload)
    openPushReplayHeldNotifications()
}

/// Earlier builds wrote op_* keys into the App Group; the native SDK mirrors openpush.shared.* on initialize.
@_cdecl("openpush_unity_remove_legacy_group_keys")
public func openpush_unity_remove_legacy_group_keys(_ appGroup: UnsafePointer<CChar>?) {
    guard let group = openPushNonEmpty(appGroup), let defaults = UserDefaults(suiteName: group) else { return }
    guard defaults.string(forKey: OpenPushSharedState.keyPrefix + "appId") != nil else { return }
    for key in ["op_server_url", "op_sdk_key", "op_app_id", "op_token"] { defaults.removeObject(forKey: key) }
}

@_cdecl("openpush_unity_add_tags")
public func openpush_unity_add_tags(_ json: UnsafePointer<CChar>?) { OpenPush.User.addTags(OpenPushUnityJSON.strings(openPushString(json))) }

@_cdecl("openpush_unity_remove_tags")
public func openpush_unity_remove_tags(_ keysJson: UnsafePointer<CChar>?) { OpenPush.User.removeTags(OpenPushUnityJSON.list(openPushString(keysJson))) }

@_cdecl("openpush_unity_get_tags")
public func openpush_unity_get_tags() -> UnsafeMutablePointer<CChar>? { strdup(OpenPushUnityJSON.string(OpenPush.User.getTags())) }

@_cdecl("openpush_unity_add_aliases")
public func openpush_unity_add_aliases(_ json: UnsafePointer<CChar>?) { OpenPush.User.addAliases(OpenPushUnityJSON.strings(openPushString(json))) }

@_cdecl("openpush_unity_remove_aliases")
public func openpush_unity_remove_aliases(_ keysJson: UnsafePointer<CChar>?) { OpenPush.User.removeAliases(OpenPushUnityJSON.list(openPushString(keysJson))) }

@_cdecl("openpush_unity_external_id")
public func openpush_unity_external_id() -> UnsafeMutablePointer<CChar>? { openPushDup(OpenPush.User.externalId) }

@_cdecl("openpush_unity_subscription")
public func openpush_unity_subscription() -> UnsafeMutablePointer<CChar>? {
    let snapshot = OpenPush.User.pushSubscription.snapshot()
    return strdup(OpenPushUnityJSON.string(OpenPushUnityJSON.subscription(snapshot) as? [String: Any] ?? [:]))
}

@_cdecl("openpush_unity_opt_in")
public func openpush_unity_opt_in() { OpenPush.PushSubscription.optIn() }

@_cdecl("openpush_unity_opt_out")
public func openpush_unity_opt_out() { OpenPush.PushSubscription.optOut() }
