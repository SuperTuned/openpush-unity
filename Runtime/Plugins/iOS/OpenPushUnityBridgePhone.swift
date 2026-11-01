import Foundation
import OpenPush

// The phone half of the Unity bridge: SMS and WhatsApp numbers on the user (fire-and-forget, as
// tags are) and phone codes (one "result" event per call, carrying the verification or the error).
// C# hands over numbers it has already normalized to E.164; the native SDK checks them again.

/// The verifications the native SDK returned, by id. An OpenPushVerification carries the full
/// number that check and resend need and cannot be rebuilt from its public fields, so the bridge
/// keeps each one here, in memory only, and C# names it by id. Never persisted, as natively.
final class OpenPushUnityVerifications: @unchecked Sendable {
    static let shared = OpenPushUnityVerifications()
    private let lock = NSLock()
    private var held: [String: OpenPushVerification] = [:]

    func hold(_ verification: OpenPushVerification) { lock.lock(); held[verification.id] = verification; lock.unlock() }
    func find(_ id: String) -> OpenPushVerification? { lock.lock(); defer { lock.unlock() }; return held[id] }
}

/// The result payloads OpenPushVerifyWire.Read (C#) reads.
enum OpenPushUnityVerifyJSON {
    static func verification(_ v: OpenPushVerification) -> [String: Any] {
        let expiresAt: Any = v.expiresAt.map { NSNumber(value: Int64(($0.timeIntervalSince1970 * 1000).rounded())) } ?? NSNull()
        return ["verification": ["id": v.id, "masked_number": v.maskedNumber, "channel": v.channel.rawValue,
                                 "status": v.status.rawValue, "attempts": v.attempts, "expires_at": expiresAt] as [String: Any]]
    }

    static func error(code: String, message: String) -> [String: Any] {
        ["error": ["code": code, "message": message]]
    }

    static func error(_ error: Error) -> [String: Any] {
        guard let refusal = error as? OpenPushVerifyError else { return Self.error(code: "network", message: "\(error)") }
        return Self.error(code: refusal.code.rawValue, message: refusal.message)
    }

    static var unknown: [String: Any] { error(code: "refused", message: "unknown verification — start a new one") }
}

/// Runs one phone-code call and answers C# once: the verification (now held for check and resend)
/// or the error.
private func openPushVerify(_ correlationId: Int64,
                            _ call: @escaping @Sendable () async throws -> OpenPushVerification) {
    Task {
        do {
            let verification = try await call()
            OpenPushUnityVerifications.shared.hold(verification)
            OpenPushUnitySink.shared.result(correlationId, OpenPushUnityVerifyJSON.verification(verification))
        } catch {
            OpenPushUnitySink.shared.result(correlationId, OpenPushUnityVerifyJSON.error(error))
        }
    }
}

// The native writes return a discardable Bool (false for a number that is not E.164). C# applies
// the same server rule first and only hands over numbers that pass, so the Bool is not sent back.
@_cdecl("openpush_unity_add_sms")
public func openpush_unity_add_sms(_ number: UnsafePointer<CChar>?) { OpenPush.User.addSms(openPushString(number) ?? "") }

@_cdecl("openpush_unity_remove_sms")
public func openpush_unity_remove_sms(_ number: UnsafePointer<CChar>?) { OpenPush.User.removeSms(openPushString(number) ?? "") }

@_cdecl("openpush_unity_add_whatsapp")
public func openpush_unity_add_whatsapp(_ number: UnsafePointer<CChar>?) { OpenPush.User.addWhatsApp(openPushString(number) ?? "") }

@_cdecl("openpush_unity_remove_whatsapp")
public func openpush_unity_remove_whatsapp(_ number: UnsafePointer<CChar>?) { OpenPush.User.removeWhatsApp(openPushString(number) ?? "") }

@_cdecl("openpush_unity_add_email")
public func openpush_unity_add_email(_ address: UnsafePointer<CChar>?) { OpenPush.User.addEmail(openPushString(address) ?? "") }

@_cdecl("openpush_unity_remove_email")
public func openpush_unity_remove_email(_ address: UnsafePointer<CChar>?) { OpenPush.User.removeEmail(openPushString(address) ?? "") }

@_cdecl("openpush_unity_verify_start")
public func openpush_unity_verify_start(_ phone: UnsafePointer<CChar>?, _ channel: UnsafePointer<CChar>?,
                                        _ locale: UnsafePointer<CChar>?, _ correlationId: Int64) {
    let number = openPushString(phone) ?? ""
    let via = openPushString(channel).flatMap(OpenPushVerifyChannel.init(rawValue:)) ?? .auto
    let language = openPushNonEmpty(locale)
    openPushVerify(correlationId) { try await OpenPush.Verify.start(number, channel: via, locale: language) }
}

@_cdecl("openpush_unity_verify_check")
public func openpush_unity_verify_check(_ verificationId: UnsafePointer<CChar>?, _ code: UnsafePointer<CChar>?,
                                        _ correlationId: Int64) {
    guard let verification = OpenPushUnityVerifications.shared.find(openPushString(verificationId) ?? "") else {
        OpenPushUnitySink.shared.result(correlationId, OpenPushUnityVerifyJSON.unknown)
        return
    }
    let typed = openPushString(code) ?? ""
    openPushVerify(correlationId) { try await OpenPush.Verify.check(verification, code: typed) }
}

@_cdecl("openpush_unity_verify_resend")
public func openpush_unity_verify_resend(_ verificationId: UnsafePointer<CChar>?, _ channel: UnsafePointer<CChar>?,
                                         _ correlationId: Int64) {
    guard let verification = OpenPushUnityVerifications.shared.find(openPushString(verificationId) ?? "") else {
        OpenPushUnitySink.shared.result(correlationId, OpenPushUnityVerifyJSON.unknown)
        return
    }
    let via = openPushString(channel).flatMap(OpenPushVerifyChannel.init(rawValue:)) ?? verification.channel
    openPushVerify(correlationId) { try await OpenPush.Verify.resend(verification, channel: via) }
}
