#import <Foundation/Foundation.h>
#import <UserNotifications/UserNotifications.h>
#import <objc/runtime.h>
#include <stdint.h>

// Foreground delivery, the companion of OpenPushNotificationClick.mm. The click
// file owns the single setDelegate: swizzle for the whole package — a second
// exchange on the same selector would undo the first — and calls the entry
// point below so this file can instrument the willPresent half of whichever
// delegate the host installs. An OpenPush notification is handed to the native
// SDK through the Swift bridge, which answers through OpenPushPresentForeground;
// anything else goes to the host's own willPresent, keyed by delegate class.
extern "C" void OpenPushInstrumentForegroundDelegate(id delegate);

typedef void (*OpenPushWillPresentHandler)(id, SEL, UNUserNotificationCenter *,
                                           UNNotification *,
                                           void (^)(UNNotificationPresentationOptions));

extern "C" void openpush_unity_process_foreground(const void *notification, int64_t presentationId);
typedef void (*OpenPushUnityPresenter)(int64_t presentationId, uint64_t options);
extern "C" void openpush_unity_set_presenter(OpenPushUnityPresenter presenter);

static NSMutableDictionary<NSString *, NSValue *> *OpenPushOriginalPresenters(void) {
    static NSMutableDictionary<NSString *, NSValue *> *values;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{ values = [NSMutableDictionary dictionary]; });
    return values;
}

static NSMutableDictionary<NSNumber *, id> *OpenPushPendingPresentations(void) {
    static NSMutableDictionary<NSNumber *, id> *values;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{ values = [NSMutableDictionary dictionary]; });
    return values;
}
static int64_t OpenPushNextPresentationId = 0;

static UNNotificationPresentationOptions OpenPushDefaultPresentationOptions(void) {
    return UNNotificationPresentationOptionBanner | UNNotificationPresentationOptionList |
           UNNotificationPresentationOptionSound;
}

// Called by the Swift bridge with the native SDK's final answer (after the C# handlers ran).
static void OpenPushPresentForeground(int64_t presentationId, uint64_t options) {
    void (^handler)(UNNotificationPresentationOptions) = nil;
    @synchronized (OpenPushPendingPresentations()) {
        handler = OpenPushPendingPresentations()[@(presentationId)];
        [OpenPushPendingPresentations() removeObjectForKey:@(presentationId)];
    }
    if (handler != nil) handler((UNNotificationPresentationOptions)options);
}

@interface OpenPushForegroundPresenterRegistration : NSObject
@end
@implementation OpenPushForegroundPresenterRegistration
+ (void)load { openpush_unity_set_presenter(&OpenPushPresentForeground); }
@end

static void OpenPushWillPresentNotification(
    id self, SEL command, UNUserNotificationCenter *center, UNNotification *notification,
    void (^completionHandler)(UNNotificationPresentationOptions)) {
    id rawMessageId = notification.request.content.userInfo[@"op_message_id"];
    NSString *messageId = rawMessageId == nil ? nil : [rawMessageId description];
    if (messageId.length == 0) {
        // Not ours: the host's own willPresent decides, or the OS default when it has none.
        NSValue *stored = nil;
        @synchronized (OpenPushOriginalPresenters()) {
            Class cls = object_getClass(self);
            while (cls != Nil && stored == nil) {
                stored = OpenPushOriginalPresenters()[NSStringFromClass(cls)];
                cls = class_getSuperclass(cls);
            }
        }
        OpenPushWillPresentHandler original =
            stored == nil ? NULL : (OpenPushWillPresentHandler)[stored pointerValue];
        if (original != NULL) original(self, command, center, notification, completionHandler);
        else if (completionHandler != nil) completionHandler(OpenPushDefaultPresentationOptions());
        return;
    }
    // Ours: the native SDK decides (listeners, suppression, deferred display, confirmed receipt).
    int64_t presentationId;
    @synchronized (OpenPushPendingPresentations()) {
        presentationId = ++OpenPushNextPresentationId;
        void (^handler)(UNNotificationPresentationOptions) =
            completionHandler ?: ^(__unused UNNotificationPresentationOptions options) {};
        OpenPushPendingPresentations()[@(presentationId)] = [handler copy];
    }
    openpush_unity_process_foreground((__bridge const void *)notification, presentationId);
}

extern "C" void OpenPushInstrumentForegroundDelegate(id delegate) {
    if (delegate == nil) return;
    Class cls = object_getClass(delegate);
    NSString *key = NSStringFromClass(cls);
    SEL selector = @selector(userNotificationCenter:willPresentNotification:
                            withCompletionHandler:);
    Method method = class_getInstanceMethod(cls, selector);
    IMP current = method == NULL ? NULL : method_getImplementation(method);
    // Our own hook must never be stored as somebody's "original": that is what
    // turns one arrival into two. class_getInstanceMethod walks the superclass
    // chain, so this also covers a host class that inherits an instrumented one.
    if (current == (IMP)OpenPushWillPresentNotification) return;

    @synchronized (OpenPushOriginalPresenters()) {
        if (OpenPushOriginalPresenters()[key] != nil) return;
        if (current != NULL)
            OpenPushOriginalPresenters()[key] =
                [NSValue valueWithPointer:reinterpret_cast<const void *>(current)];
        else
            OpenPushOriginalPresenters()[key] = [NSValue valueWithPointer:NULL];
    }

    const char *types = method == NULL ? "v@:@@@?" : method_getTypeEncoding(method);
    if (!class_addMethod(cls, selector, (IMP)OpenPushWillPresentNotification, types))
        class_replaceMethod(cls, selector, (IMP)OpenPushWillPresentNotification, types);
}
