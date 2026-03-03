using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(AudioManager.GameSound))]
public class GameSoundDrawer : PropertyDrawer
{
     public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Replace "Element X" with the sound name enum value
        var soundName = property.FindPropertyRelative("soundName");
        label.text = soundName.enumNames[soundName.enumValueIndex];

        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
