using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ObjectPoolManager))]
[CanEditMultipleObjects]
public class ObjectPoolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var __ref = (ObjectPoolManager)target;

        if (GUILayout.Button("Generate enum"))
        {
            __ref.GenerateEnumClass();
        }
    }
}

/// TODO: Dictionary display