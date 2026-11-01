#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import "AppDelegateListener.h"

// Unity 2022.3's UnityAppController still posts these notifications, but its AppDelegateListener
// protocol no longer forwards the remote-notification selectors, so they are observed directly
// (the same route Unity's own mobile notifications package uses). Everything goes to the Swift
// bridge, which hands it to the native SDK; a token arriving before initialize is held there.
extern "C" void openpush_unity_did_register_token(const void *bytes, long length);
extern "C" void openpush_unity_did_fail_to_register(const char *message);
extern "C" void openpush_unity_did_receive_remote_notification(const void *userInfo);

@interface OpenPushUnityAppDelegateListener : NSObject <AppDelegateListener>
@end

static OpenPushUnityAppDelegateListener *OpenPushSharedListener;

@implementation OpenPushUnityAppDelegateListener

+ (void)load {
    OpenPushSharedListener = [OpenPushUnityAppDelegateListener new];
    UnityRegisterAppDelegateListener(OpenPushSharedListener);
    NSNotificationCenter *center = [NSNotificationCenter defaultCenter];
    [center addObserver:OpenPushSharedListener selector:@selector(didRegisterForRemoteNotifications:)
                   name:kUnityDidRegisterForRemoteNotificationsWithDeviceToken object:nil];
    [center addObserver:OpenPushSharedListener selector:@selector(didFailToRegisterForRemoteNotifications:)
                   name:kUnityDidFailToRegisterForRemoteNotificationsWithError object:nil];
    [center addObserver:OpenPushSharedListener selector:@selector(didReceiveRemoteNotification:)
                   name:kUnityDidReceiveRemoteNotification object:nil];
}

- (void)didRegisterForRemoteNotifications:(NSNotification *)notification {
    // Unity passes the token NSData as userInfo.
    id token = notification.userInfo;
    if ([token isKindOfClass:[NSData class]])
        openpush_unity_did_register_token(((NSData *)token).bytes, (long)((NSData *)token).length);
}

- (void)didFailToRegisterForRemoteNotifications:(NSNotification *)notification {
    // Unity passes the NSError as userInfo.
    id error = notification.userInfo;
    NSString *message = [error isKindOfClass:[NSError class]] ? ((NSError *)error).localizedDescription : [error description];
    openpush_unity_did_fail_to_register((message ?: @"unknown").UTF8String);
}

- (void)didReceiveRemoteNotification:(NSNotification *)notification {
    NSDictionary *userInfo = notification.userInfo;
    if ([userInfo isKindOfClass:[NSDictionary class]])
        openpush_unity_did_receive_remote_notification((__bridge const void *)userInfo);
}

@end
