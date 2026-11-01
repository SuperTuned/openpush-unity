import Foundation

/// Native objects a delegate hook captured before OpenPush.initialize ran (a cold-start tap, a
/// delivery to a launching app). The native SDK drops a receipt while it has no configuration, so
/// the bridge holds them, newest `cap` kept, and replays them right after initialize. Foundation
/// only, so Tests~/Native/verify-swift-sources-compile.sh can run it on the host.
final class OpenPushUnityHeldQueue<Element>: @unchecked Sendable {
    private let lock = NSLock()
    private var items: [Element] = []
    private let cap: Int

    init(cap: Int) { self.cap = cap }

    func hold(_ item: Element) {
        lock.lock(); defer { lock.unlock() }
        items.append(item)
        if items.count > cap { items.removeFirst(items.count - cap) }
    }

    /// Everything held, oldest first; each item is returned to exactly one caller.
    func drain() -> [Element] {
        lock.lock(); defer { items = []; lock.unlock() }
        return items
    }
}
