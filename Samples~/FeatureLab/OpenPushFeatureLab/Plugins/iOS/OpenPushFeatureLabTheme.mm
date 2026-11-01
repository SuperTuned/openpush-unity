#import <UIKit/UIKit.h>

extern "C" bool OpenPushFeatureLabSystemThemeIsDark(void) {
    if (@available(iOS 12.0, *)) {
        UIWindow *window = UIApplication.sharedApplication.windows.firstObject;
        UITraitCollection *traits = window ? window.traitCollection : UIScreen.mainScreen.traitCollection;
        return traits.userInterfaceStyle == UIUserInterfaceStyleDark;
    }
    return false;
}
