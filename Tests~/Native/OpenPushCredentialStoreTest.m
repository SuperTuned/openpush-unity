// Drives Runtime/Plugins/iOS/OpenPushCredentialStore.m against an in-memory Keychain: the credential
// is written in place (never deleted first), and an item an earlier build wrote with another
// accessibility class keeps its value while its class moves to this-device-only.
#import <Foundation/Foundation.h>
#import <Security/Security.h>
#import <stdlib.h>

typedef struct {
    OSStatus (*copyMatching)(CFDictionaryRef query, CFTypeRef *result);
    OSStatus (*add)(CFDictionaryRef attributes, CFTypeRef *result);
    OSStatus (*update)(CFDictionaryRef query, CFDictionaryRef changes);
    OSStatus (*remove)(CFDictionaryRef query);
} OpenPushKeychainCalls;

void _openpush_credential_set_keychain(OpenPushKeychainCalls calls);
bool _openpush_credential_save(const char *service, const char *accessGroup, const char *value);
char *_openpush_credential_load(const char *service, const char *accessGroup);
void _openpush_credential_wipe(const char *service, const char *accessGroup);

static NSMutableDictionary *item;   // the one generic-password item, or nil
static int adds, updates, removes;
static OSStatus updateFailure;      // non-zero: the next update fails with it

static BOOL Matches(NSDictionary *query) {
    if (item == nil) return NO;
    for (id key in @[(__bridge id)kSecClass, (__bridge id)kSecAttrService, (__bridge id)kSecAttrAccount,
                     (__bridge id)kSecAttrAccessGroup]) {
        if (query[key] != nil && ![query[key] isEqual:item[key]]) return NO;
    }
    return YES;
}

static OSStatus FakeCopy(CFDictionaryRef query, CFTypeRef *result) {
    NSDictionary *q = (__bridge NSDictionary *)query;
    if (!Matches(q)) return errSecItemNotFound;
    NSMutableDictionary *found = [NSMutableDictionary dictionary];
    if ([q[(__bridge id)kSecReturnData] boolValue]) found[(__bridge id)kSecValueData] = item[(__bridge id)kSecValueData];
    if ([q[(__bridge id)kSecReturnAttributes] boolValue]) found[(__bridge id)kSecAttrAccessible] = item[(__bridge id)kSecAttrAccessible];
    // Without ReturnAttributes the Keychain hands back the bare data, as the real one does.
    *result = [q[(__bridge id)kSecReturnAttributes] boolValue]
        ? (__bridge_retained CFTypeRef)found : (__bridge_retained CFTypeRef)item[(__bridge id)kSecValueData];
    return errSecSuccess;
}

static OSStatus FakeAdd(CFDictionaryRef attributes, CFTypeRef *result) {
    (void)result;
    adds++;
    if (item != nil) return errSecDuplicateItem;
    item = [(__bridge NSDictionary *)attributes mutableCopy];
    return errSecSuccess;
}

static OSStatus FakeUpdate(CFDictionaryRef query, CFDictionaryRef changes) {
    updates++;
    if (updateFailure != 0) { OSStatus failure = updateFailure; updateFailure = 0; return failure; }
    if (!Matches((__bridge NSDictionary *)query)) return errSecItemNotFound;
    [item addEntriesFromDictionary:(__bridge NSDictionary *)changes];
    return errSecSuccess;
}

static OSStatus FakeRemove(CFDictionaryRef query) {
    removes++;
    if (!Matches((__bridge NSDictionary *)query)) return errSecItemNotFound;
    item = nil;
    return errSecSuccess;
}

static void Reset(void) { item = nil; adds = updates = removes = 0; updateFailure = 0; }

static void Check(BOOL condition, NSString *message) {
    if (!condition) { fprintf(stderr, "✗ %s\n", message.UTF8String); exit(1); }
}

static NSString *Load(const char *service) {
    char *value = _openpush_credential_load(service, "");
    NSString *text = value ? [NSString stringWithUTF8String:value] : nil;
    free(value);
    return text;
}

static NSString *Accessibility(void) { return item[(__bridge id)kSecAttrAccessible]; }
static NSString *ThisDeviceOnly(void) { return (__bridge NSString *)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly; }

// The item an earlier build of this store left: written with delete-then-add, AfterFirstUnlock.
static void SeedEarlierBuildItem(NSString *service, NSString *value) {
    item = [@{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: service,
        (__bridge id)kSecAttrAccount: @"device-key",
        (__bridge id)kSecValueData: [value dataUsingEncoding:NSUTF8StringEncoding],
        (__bridge id)kSecAttrAccessible: (__bridge id)kSecAttrAccessibleAfterFirstUnlock,
    } mutableCopy];
}

int main(void) {
    @autoreleasepool {
        _openpush_credential_set_keychain((OpenPushKeychainCalls){ FakeCopy, FakeAdd, FakeUpdate, FakeRemove });
        const char *service = "com.example.game.openpush.device";

        Reset();
        Check(_openpush_credential_save(service, "", "cred-1"), @"a first save succeeds");
        Check(adds == 1 && removes == 0, @"a first save adds the item and deletes nothing");
        Check([Accessibility() isEqualToString:ThisDeviceOnly()], @"a new item is this-device-only");
        Check([Load(service) isEqualToString:@"cred-1"], @"the saved credential loads");

        Check(_openpush_credential_save(service, "", "cred-2"), @"a second save succeeds");
        Check(adds == 1 && removes == 0, @"a second save updates in place: no delete, no add");
        Check([Load(service) isEqualToString:@"cred-2"], @"the updated credential loads");

        // Upgrade: the earlier build's item survives and moves to this-device-only.
        Reset();
        SeedEarlierBuildItem(@"com.example.game.openpush.device", @"device-credential");
        Check([Load(service) isEqualToString:@"device-credential"], @"the earlier build's credential still loads");
        Check(updates == 1 && adds == 0 && removes == 0, @"the migration is one in-place update");
        Check([Accessibility() isEqualToString:ThisDeviceOnly()], @"the earlier item is now this-device-only");
        Check([Load(service) isEqualToString:@"device-credential"] && updates == 1, @"a migrated item is not updated again");

        // A failed migration still returns the credential, and is retried on the next load.
        Reset();
        SeedEarlierBuildItem(@"com.example.game.openpush.device", @"device-credential");
        updateFailure = errSecInteractionNotAllowed;
        Check([Load(service) isEqualToString:@"device-credential"], @"a failed migration still returns the credential");
        Check(![Accessibility() isEqualToString:ThisDeviceOnly()], @"the failed migration left the item as it was");
        Check([Load(service) isEqualToString:@"device-credential"], @"the next load still returns it");
        Check([Accessibility() isEqualToString:ThisDeviceOnly()], @"and moves it then");

        // A save over an earlier item rewrites value and class in place.
        Reset();
        SeedEarlierBuildItem(@"com.example.game.openpush.device", @"device-credential");
        Check(_openpush_credential_save(service, "", "rotated"), @"saving over an earlier item succeeds");
        Check(adds == 0 && removes == 0, @"saving over an earlier item never deletes it");
        Check([Accessibility() isEqualToString:ThisDeviceOnly()], @"and moves it to this-device-only");

        // A failing write keeps the old credential: nothing is deleted first.
        Reset();
        SeedEarlierBuildItem(@"com.example.game.openpush.device", @"device-credential");
        updateFailure = errSecInteractionNotAllowed;
        Check(!_openpush_credential_save(service, "", "new"), @"a failed write reports failure");
        Check(removes == 0 && adds == 0, @"a failed write deletes and adds nothing");
        Check([Load(service) isEqualToString:@"device-credential"], @"the old credential survives a failed write");

        // Fail closed on malformed UTF-8, before touching the Keychain.
        Reset();
        updates = 0;
        Check(!_openpush_credential_save(service, "", "\xff\xfe"), @"malformed UTF-8 is refused");
        Check(updates == 0 && adds == 0, @"malformed UTF-8 never reaches the Keychain");

        // Nothing stored, and wipe.
        Reset();
        Check(Load(service) == nil, @"nothing stored loads as nothing");
        Check(_openpush_credential_save(service, "", "cred"), @"save before wipe");
        _openpush_credential_wipe(service, "");
        Check(item == nil && Load(service) == nil, @"wipe removes the item");
    }
    printf("✓ the credential store updates in place and moves earlier items to this-device-only without losing them\n");
    return 0;
}
