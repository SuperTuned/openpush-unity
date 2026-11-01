#import <Foundation/Foundation.h>
#import <Security/Security.h>
#import <string.h>
#import <stdlib.h>

// The four Keychain calls, behind one table so the in-place update and the migration can be
// exercised where a test process has no Keychain (Tests~/Native/verify-ios-credential-store.sh).
typedef struct {
    OSStatus (*copyMatching)(CFDictionaryRef query, CFTypeRef *result);
    OSStatus (*add)(CFDictionaryRef attributes, CFTypeRef *result);
    OSStatus (*update)(CFDictionaryRef query, CFDictionaryRef changes);
    OSStatus (*remove)(CFDictionaryRef query);
} OpenPushKeychainCalls;

static OpenPushKeychainCalls OpenPushKeychain = {
    SecItemCopyMatching, SecItemAdd, SecItemUpdate, SecItemDelete,
};

#ifdef OPENPUSH_CREDENTIAL_STORE_TESTING
void _openpush_credential_set_keychain(OpenPushKeychainCalls calls);
void _openpush_credential_set_keychain(OpenPushKeychainCalls calls) { OpenPushKeychain = calls; }
#endif

// The class every credential is written with: readable once the device has been unlocked after a
// restart (the app can be woken in the background), and never restored onto another device from a
// backup, where it would make two installs share one subscription.
static CFStringRef OpenPushAccessibility(void) {
    return kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly;
}

static NSMutableDictionary *OpenPushBaseQuery(const char *service,
                                              const char *accessGroup) {
    NSMutableDictionary *query = [NSMutableDictionary dictionary];
    query[(__bridge id)kSecClass] = (__bridge id)kSecClassGenericPassword;
    query[(__bridge id)kSecAttrService] =
        [NSString stringWithUTF8String:service ?: ""];
    query[(__bridge id)kSecAttrAccount] = @"device-key";
    if (accessGroup != NULL && strlen(accessGroup) > 0) {
        query[(__bridge id)kSecAttrAccessGroup] =
            [NSString stringWithUTF8String:accessGroup];
    }
    return query;
}

// IL2CPP copies strings returned through a managed `string` P/Invoke and then
// releases the native buffer with free(), so return caller-owned storage.
static char *OpenPushCopyCString(NSString *value) {
    if (value == nil) return NULL;
    const char *utf8 = [value UTF8String];
    if (utf8 == NULL) return NULL;
    char *copy = (char *)malloc(strlen(utf8) + 1);
    if (copy != NULL) strcpy(copy, utf8);
    return copy;
}

// Writes the credential in place. An existing item is updated, never deleted first, so a failed
// write cannot leave the install without its credential (losing it makes the server create a
// second subscription). Only when there is no item yet is one added.
bool _openpush_credential_save(const char *service, const char *accessGroup,
                               const char *value) {
    if (value == NULL) return false;
    // Build the data before touching the keychain. stringWithUTF8String: returns
    // nil for malformed UTF-8, and assigning nil to kSecValueData removes the
    // key instead of setting it: the keychain could then report success for an
    // item holding no credential. This file's contract is fail-closed, so bail
    // out first.
    NSData *data = [[NSString stringWithUTF8String:value]
        dataUsingEncoding:NSUTF8StringEncoding];
    if (data == nil) return false;
    NSMutableDictionary *query = OpenPushBaseQuery(service, accessGroup);
    NSDictionary *changes = @{
        (__bridge id)kSecValueData: data,
        (__bridge id)kSecAttrAccessible: (__bridge id)OpenPushAccessibility(),
    };
    OSStatus status = OpenPushKeychain.update((__bridge CFDictionaryRef)query,
                                              (__bridge CFDictionaryRef)changes);
    if (status != errSecItemNotFound) return status == errSecSuccess;
    query[(__bridge id)kSecValueData] = data;
    query[(__bridge id)kSecAttrAccessible] = (__bridge id)OpenPushAccessibility();
    return OpenPushKeychain.add((__bridge CFDictionaryRef)query, NULL) == errSecSuccess;
}

// Reads the credential. An item written by an earlier build with another accessibility class is
// moved to the current one in place: same item, same value, only the accessibility attribute
// changes. If that update fails the value is still returned, and the move is tried again on the
// next load.
char *_openpush_credential_load(const char *service, const char *accessGroup) {
    NSMutableDictionary *query = OpenPushBaseQuery(service, accessGroup);
    query[(__bridge id)kSecReturnData] = @YES;
    query[(__bridge id)kSecReturnAttributes] = @YES;
    query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
    CFTypeRef result = NULL;
    if (OpenPushKeychain.copyMatching((__bridge CFDictionaryRef)query, &result) != errSecSuccess) {
        return NULL;
    }
    id item = (__bridge_transfer id)result;
    if (![item isKindOfClass:[NSDictionary class]]) return NULL;
    NSData *data = ((NSDictionary *)item)[(__bridge id)kSecValueData];
    if (![data isKindOfClass:[NSData class]]) return NULL;
    NSString *value = [[NSString alloc] initWithData:data
                                            encoding:NSUTF8StringEncoding];
    id accessible = ((NSDictionary *)item)[(__bridge id)kSecAttrAccessible];
    if (value != nil && ![accessible isEqual:(__bridge id)OpenPushAccessibility()]) {
        NSDictionary *changes = @{
            (__bridge id)kSecAttrAccessible: (__bridge id)OpenPushAccessibility(),
        };
        OpenPushKeychain.update(
            (__bridge CFDictionaryRef)OpenPushBaseQuery(service, accessGroup),
            (__bridge CFDictionaryRef)changes);
    }
    return OpenPushCopyCString(value);
}

void _openpush_credential_wipe(const char *service, const char *accessGroup) {
    NSMutableDictionary *query = OpenPushBaseQuery(service, accessGroup);
    OpenPushKeychain.remove((__bridge CFDictionaryRef)query);
}
