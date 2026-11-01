package ai.openpush.unity;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import java.security.KeyStore;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/** AES-GCM credential storage backed by Android Keystore. Fails closed:
 *  when the Keystore is unavailable, save reports false and nothing is
 *  written; credentials are never stored as plaintext. */
public final class OpenPushCredentialStore {
    private OpenPushCredentialStore() {}

    public static boolean save(Context context, String service, String value) {
        SecretKey key = key(service);
        if (key == null || value == null) return false;
        try {
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, key);
            byte[] encrypted = cipher.doFinal(value.getBytes("UTF-8"));
            prefs(context).edit()
                .putString(service + ".data",
                           Base64.encodeToString(encrypted, Base64.NO_WRAP))
                .putString(service + ".iv",
                           Base64.encodeToString(cipher.getIV(), Base64.NO_WRAP))
                .apply();
            return true;
        } catch (Exception error) {
            return false;
        }
    }

    public static String load(Context context, String service) {
        SharedPreferences prefs = prefs(context);
        String data = prefs.getString(service + ".data", null);
        if (data == null) return null;
        String iv = prefs.getString(service + ".iv", null);
        if (iv == null) return null;
        SecretKey key = key(service);
        if (key == null) return null;
        try {
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, key,
                        new GCMParameterSpec(128, Base64.decode(iv, Base64.NO_WRAP)));
            return new String(cipher.doFinal(Base64.decode(data, Base64.NO_WRAP)), "UTF-8");
        } catch (Exception error) {
            return null;
        }
    }

    public static void wipe(Context context, String service) {
        prefs(context).edit()
            .remove(service + ".data")
            .remove(service + ".iv")
            .apply();
    }

    private static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences("openpush.secure", Context.MODE_PRIVATE);
    }

    private static SecretKey key(String service) {
        try {
            KeyStore store = KeyStore.getInstance("AndroidKeyStore");
            store.load(null);
            java.security.Key existing = store.getKey(service, null);
            if (existing instanceof SecretKey) return (SecretKey) existing;
            KeyGenerator generator = KeyGenerator.getInstance(
                KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
            generator.init(new KeyGenParameterSpec.Builder(
                    service,
                    KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .build());
            return generator.generateKey();
        } catch (Exception error) {
            return null;
        }
    }
}
