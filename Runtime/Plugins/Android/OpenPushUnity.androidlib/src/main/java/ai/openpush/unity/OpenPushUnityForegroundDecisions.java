package ai.openpush.unity;

import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicLong;

/**
 * Pending foreground decisions. The native SDK asks its foreground listeners synchronously on the
 * FCM worker thread; the adapter opens a decision, hands it to C# (which answers from the Unity
 * main thread), and waits a bounded time. Nothing ever blocks the Unity thread.
 */
final class OpenPushUnityForegroundDecisions implements OpenPushUnityCompletion {
    private static final class Pending {
        final CountDownLatch latch = new CountDownLatch(1);
        volatile boolean display = true;
    }

    private final ConcurrentHashMap<String, Pending> pending = new ConcurrentHashMap<>();
    private final AtomicLong next = new AtomicLong();

    String open() {
        String id = "fg-" + next.incrementAndGet();
        pending.put(id, new Pending());
        return id;
    }

    @Override
    public void complete(String id, boolean display) {
        if (id == null) return;
        Pending decision = pending.get(id);
        if (decision == null) return;
        synchronized (decision) {
            if (decision.latch.getCount() == 0) return;
            decision.display = display;
            decision.latch.countDown();
        }
    }

    /** Returns the decision, or true (display) when C# did not answer within the timeout. */
    boolean await(String id, long timeoutMs) {
        Pending decision = pending.get(id);
        if (decision == null) return true;
        try {
            decision.latch.await(timeoutMs, TimeUnit.MILLISECONDS);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        } finally {
            pending.remove(id);
        }
        synchronized (decision) {
            return decision.display;
        }
    }

    int pendingCount() {
        return pending.size();
    }
}
