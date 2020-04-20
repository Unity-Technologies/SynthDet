using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Syncity.Sensors
{
	[CustomPropertyDrawer(typeof(GPSPosition))]
	public class GPSPositionPropertyDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) 
		{
			var latitude = property.FindPropertyRelative (nameof(GPSPosition.latitude));
			var longitude = property.FindPropertyRelative (nameof(GPSPosition.longitude));
			var height = property.FindPropertyRelative (nameof(GPSPosition.height));

			EditorGUI.BeginProperty(position, label, property);

			var labelPosition = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

			float itemHeight = EditorGUI.GetPropertyHeight (property, label, true) + EditorGUIUtility.standardVerticalSpacing;
			
			EditorGUI.PropertyField(
				new Rect(labelPosition.x, labelPosition.y + 0f * itemHeight, position.width - labelPosition.x, itemHeight),
				latitude, new GUIContent(nameof(GPSPosition.latitude)));
			EditorGUI.PropertyField(
				new Rect(labelPosition.x, labelPosition.y + 1f * itemHeight, position.width - labelPosition.x, itemHeight),
				longitude, new GUIContent(nameof(GPSPosition.longitude)));
			EditorGUI.PropertyField(
				new Rect(labelPosition.x, labelPosition.y + 2f * itemHeight, position.width - labelPosition.x, itemHeight),
				height, new GUIContent(nameof(GPSPosition.height)));
			if(GUILayout.Button("Open in Maps"))
			{
				var url =
					"http://www.google.com/maps/place/" +
					$"{latitude.floatValue.ToString(CultureInfo.InvariantCulture)}," +
					$"{longitude.floatValue.ToString(CultureInfo.InvariantCulture)}";
				Application.OpenURL(url);
			}
			
			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float itemHeight = EditorGUI.GetPropertyHeight (property, label, true) + EditorGUIUtility.standardVerticalSpacing;
			return itemHeight * 3;
		}
	}
}