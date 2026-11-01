using OpenPushSDK.Core;
using UnityEditor;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Shows <see cref="OpenPushAndroidPushDelivery"/> as Automatic or Forward only. HostOwned is the same value
    /// under a second name, kept so existing settings and code compile; listing it would offer one choice twice.
    /// </summary>
    [CustomPropertyDrawer(typeof(OpenPushAndroidPushDelivery))]
    internal sealed class OpenPushAndroidPushDeliveryDrawer : PropertyDrawer
    {
        private static readonly GUIContent[] Options = { new GUIContent("Automatic"), new GUIContent("Forward") };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var current = property.intValue == (int)OpenPushAndroidPushDelivery.Forward ? 1 : 0;
                EditorGUI.BeginChangeCheck();
                var chosen = EditorGUI.Popup(position, label, current, Options);
                if (EditorGUI.EndChangeCheck())
                    property.intValue = chosen == 1 ? (int)OpenPushAndroidPushDelivery.Forward : (int)OpenPushAndroidPushDelivery.Automatic;
            }
        }
    }
}
