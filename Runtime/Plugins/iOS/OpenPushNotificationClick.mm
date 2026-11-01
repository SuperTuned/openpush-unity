#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <UserNotifications/UserNotifications.h>
#import <objc/runtime.h>

// Taps are handed to the Swift bridge, which forwards them to the native SDK.
extern "C" void openpush_unity_process_click(const void *response);

static NSMutableDictionary<NSString *, NSValue *> *OpenPushOriginalHandlers(void) {
    static NSMutableDictionary<NSString *, NSValue *> *values;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{ values = [NSMutableDictionary dictionary]; });
    return values;
}

static NSMutableSet<NSString *> *OpenPushConsumedMessageIds(void) {
    static NSMutableSet<NSString *> *values;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{ values = [NSMutableSet set]; });
    return values;
}

static NSString *OpenPushClassKey(Class cls) {
    return NSStringFromClass(cls);
}

// Hands a tap to the native SDK, once per message: the fallback delegate and an instrumented host
// delegate can both see the same response. The native SDK parses it, posts the clicked receipt and
// raises the click listener (held for a listener that registers later: cold start).
static void OpenPushRecordResponse(UNNotificationResponse *response) {
    if (response == nil ||
        [response.actionIdentifier isEqualToString:UNNotificationDismissActionIdentifier]) return;
    id rawMessageId = response.notification.request.content.userInfo[@"op_message_id"];
    NSString *messageId = rawMessageId == nil ? nil : [rawMessageId description];
    if (messageId.length == 0) return;
    @synchronized (OpenPushConsumedMessageIds()) {
        NSString *key = [messageId stringByAppendingFormat:@"|%@", response.actionIdentifier ?: @""];
        if ([OpenPushConsumedMessageIds() containsObject:key]) return;
        if (OpenPushConsumedMessageIds().count > 32) [OpenPushConsumedMessageIds() removeAllObjects];
        [OpenPushConsumedMessageIds() addObject:key];
    }
    openpush_unity_process_click((__bridge const void *)response);
}

typedef void (*OpenPushResponseHandler)(id, SEL, UNUserNotificationCenter *,
                                        UNNotificationResponse *, void (^)(void));

// Foreground capture lives in OpenPushNotificationForeground.mm, but the
// setDelegate: swizzle below is the package's only one: a second exchange on the
// same selector would undo this one. Both hooks are therefore installed from
// here, including on the fallback delegate, which deliberately implements
// neither: it is instrumented like any other delegate.
extern "C" void OpenPushInstrumentForegroundDelegate(id delegate);

@interface OpenPushFallbackNotificationDelegate : NSObject <UNUserNotificationCenterDelegate>
@end

@implementation OpenPushFallbackNotificationDelegate

- (void)userNotificationCenter:(UNUserNotificationCenter *)center
 didReceiveNotificationResponse:(UNNotificationResponse *)response
          withCompletionHandler:(void (^)(void))completionHandler {
    OpenPushRecordResponse(response);
    id appDelegate = UIApplication.sharedApplication.delegate;
    SEL selector = _cmd;
    if (appDelegate != nil && appDelegate != self &&
        [appDelegate respondsToSelector:selector]) {
        OpenPushResponseHandler handler =
            (OpenPushResponseHandler)[appDelegate methodForSelector:selector];
        handler(appDelegate, selector, center, response, completionHandler);
    } else if (completionHandler != nil) {
        completionHandler();
    }
}

@end


static OpenPushFallbackNotificationDelegate *OpenPushFallbackDelegate(void) {
    static OpenPushFallbackNotificationDelegate *delegate;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{ delegate = [OpenPushFallbackNotificationDelegate new]; });
    return delegate;
}

static void OpenPushDidReceiveResponse(id self, SEL command,
                                       UNUserNotificationCenter *center,
                                       UNNotificationResponse *response,
                                       void (^completionHandler)(void)) {
    OpenPushRecordResponse(response);

    NSValue *stored = nil;
    @synchronized (OpenPushOriginalHandlers()) {
        Class cls = object_getClass(self);
        while (cls != Nil && stored == nil) {
            stored = OpenPushOriginalHandlers()[OpenPushClassKey(cls)];
            cls = class_getSuperclass(cls);
        }
    }
    OpenPushResponseHandler original =
        stored == nil ? NULL : (OpenPushResponseHandler)[stored pointerValue];
    if (original != NULL) original(self, command, center, response, completionHandler);
    else if (completionHandler != nil) completionHandler();
}

static void OpenPushInstrumentNotificationDelegate(id delegate) {
    if (delegate == nil) return;
    Class cls = object_getClass(delegate);
    NSString *key = OpenPushClassKey(cls);
    SEL selector = @selector(userNotificationCenter:didReceiveNotificationResponse:
                            withCompletionHandler:);
    Method method = class_getInstanceMethod(cls, selector);
    IMP current = method == NULL ? NULL : method_getImplementation(method);
    // Our own hook must never be stored as somebody's "original". The fallback
    // delegate below still implements this selector itself — it forwards the
    // response to the app delegate, which the hook cannot do — so its own IMP is
    // stored and OpenPushRecordResponse runs twice for one tap. That is harmless
    // and deliberate: OpenPushConsumedMessageIds already exists because the OS
    // itself can deliver a response more than once, and it collapses the pair.
    if (current == (IMP)OpenPushDidReceiveResponse) return;

    @synchronized (OpenPushOriginalHandlers()) {
        if (OpenPushOriginalHandlers()[key] != nil) return;
        if (current != NULL)
            OpenPushOriginalHandlers()[key] =
                [NSValue valueWithPointer:reinterpret_cast<const void *>(current)];
        else
            OpenPushOriginalHandlers()[key] = [NSValue valueWithPointer:NULL];
    }

    const char *types = method == NULL ? "v@:@@@?" : method_getTypeEncoding(method);
    if (!class_addMethod(cls, selector, (IMP)OpenPushDidReceiveResponse, types))
        class_replaceMethod(cls, selector, (IMP)OpenPushDidReceiveResponse, types);
}

@interface UNUserNotificationCenter (OpenPushNotificationClick)
- (void)openpush_setDelegate:(id<UNUserNotificationCenterDelegate>)delegate;
@end

@implementation UNUserNotificationCenter (OpenPushNotificationClick)

+ (void)load {
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        Method original = class_getInstanceMethod(self, @selector(setDelegate:));
        Method replacement = class_getInstanceMethod(self, @selector(openpush_setDelegate:));
        method_exchangeImplementations(original, replacement);
        UNUserNotificationCenter *center = [UNUserNotificationCenter currentNotificationCenter];
        if (center.delegate != nil) {
            OpenPushInstrumentNotificationDelegate(center.delegate);
            OpenPushInstrumentForegroundDelegate(center.delegate);
        } else {
            // After exchange this spelling calls Apple's original setter. Keep a
            // strong fallback only until a provider installs its own delegate.
            [center openpush_setDelegate:OpenPushFallbackDelegate()];
            // Instrumented like any other delegate, and only here: the fallback
            // implements no willPresent of its own, so class_addMethod succeeds,
            // the stored original is NULL, and the hook completes with the OS
            // default options. A fallback that implemented the selector itself
            // would be stored as its own "original" and deliver twice.
            OpenPushInstrumentForegroundDelegate(OpenPushFallbackDelegate());
        }
    });
}

- (void)openpush_setDelegate:(id<UNUserNotificationCenterDelegate>)delegate {
    OpenPushInstrumentNotificationDelegate(delegate);
    OpenPushInstrumentForegroundDelegate(delegate);
    [self openpush_setDelegate:delegate];
}

@end
