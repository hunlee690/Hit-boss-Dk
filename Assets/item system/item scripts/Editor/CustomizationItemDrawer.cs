using UnityEditor;
using UnityEngine;

namespace HitBoss.Items.Editor
{
    [CustomPropertyDrawer(typeof(PlayerCustomizationManager.CustomizationItem))]
    public sealed class CustomizationItemDrawer : PropertyDrawer
    {
        static readonly string[] Fields = { "itemName", "equippedByDefault", "objectsToEnable" };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;
            foreach (string field in Fields)
                height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            string name = property.FindPropertyRelative("itemName").stringValue;
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded,
                string.IsNullOrWhiteSpace(name) ? "Unnamed Item" : name, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (string field in Fields)
                {
                    row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                    var child = property.FindPropertyRelative(field);
                    row.height = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(row, child, true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
