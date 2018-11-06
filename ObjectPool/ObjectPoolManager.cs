using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
#endif

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance;

    public bool DontDestroyOnLoad = false;
    public bool StorePoolUnderSeparateObject = false;

    /// <summary>
    /// Root for all pooled objects
    /// </summary>
    public Transform Root;

    public int MaxObjectsPooled = 100;  /// TODO: Find optimal values

    public List<ObjectType> Keys;
    public List<GameObject> Values;

    [Header("Object types for enum generator")]
    public string[] EnumTypes = new string[] { "None" };

    private void Awake()
    {
        if (DontDestroyOnLoad)
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else UnityEngine.Object.DestroyImmediate(this);
        }
        else Instance = this;

        Root = StorePoolUnderSeparateObject ? new GameObject("ObjectPoolRoot").transform : gameObject.transform;

        /// Assigning prefabs
        for (int i = 0; i < Mathf.Min(Keys.Count, Values.Count); i++)
        {
            ObjectPool.ObjectTypePrefabs.Add(Keys[i], Values[i]);
        }

        /// Initializing lists
        System.Array types = System.Enum.GetValues(typeof(ObjectType));
        for (int i = 0; i < types.Length; i++)
        {
            ObjectPool.Objects.Add((ObjectType)types[i], new List<GameObject>());
        }
    }

    private void Update()
    {
        int counter = 0;
        while (Root.childCount > MaxObjectsPooled && counter < 100)
        {
            Destroy(Root.GetChild(0).gameObject);
            counter++;
        }
    }

    public void GenerateEnumClass()
    {
#if UNITY_EDITOR
        if (EnumTypes.Length <= 0) return;

        /// Generating code
        string code = string.Empty;
        code += "public enum ObjectType\n{\n";

        for (int i = 0; i < EnumTypes.Length; i++)
        {
            code += "\t" + EnumTypes[i];
            if (i < EnumTypes.Length - 1) code += ",";
            code += "\n";
        }

        code += "}";

        /// Writing code
        File.WriteAllText("Assets/Scripts/ObjectTypeEnum.cs", code);
        UnityEditor.AssetDatabase.Refresh();
#else
        Debug.LogWarning("Method cannot be called at runtime, only in editor");
#endif
    }
}

public static class ObjectPool
{
    public static Dictionary<ObjectType, GameObject> ObjectTypePrefabs = new Dictionary<ObjectType, GameObject>();
    public static Dictionary<ObjectType, List<GameObject>> Objects = new Dictionary<ObjectType, List<GameObject>>();

    /// <summary>
    /// Returns object of requested type.
    /// Prefabs should be registered using ObjectPoolManager
    /// </summary>
    public static GameObject GetObject(ObjectType type)
    {
        /// Ignore 'None' type
        if (type == ObjectType.None) return null;

        /// Instantiate first object in list
        if (Objects[type].Count == 0)
            return Instantiate(type);

        /// Get inactive object
        var obj = Objects[type].FirstOrDefault(x => x != null && x.activeSelf == false);
        if (obj == null)
        {
            /// Check if type is registered
            if (!ObjectTypePrefabs.ContainsKey(type))
                throw new System.Exception(
                    "Prefab for type " + System.Enum.GetName(type) + " not registered.\n" +
                    "Prefabs should be registered using ObjectPoolManager, before application runs");

            obj = Instantiate(type);
            obj.name = obj.name + " " + Objects[type].Count;
        }
        obj.SetActive(true);
        return obj;
    }

    /// <summary>
    /// Instantiates object and adds it to corresponding list
    /// </summary>
    /// <returns>New gameobject</returns>
    public static GameObject Instantiate(ObjectType type)
    {
        var obj = GameObject.Instantiate(ObjectTypePrefabs[type]);
        Objects[type].Add(obj);
        return obj;
    }

    public static void RemoveObject(GameObject go)
    {
        go.transform.SetParent(ObjectPoolManager.Instance.Root);
        go.SetActive(false);
    }
}

#region In progress

//public static class ObjectPoolExtension
//{
//    /// Extension for GameObject's fast removal
//    public static void Destroy(this GameObject go)
//    {
//        foreach (var type in ObjectPool.ObjectTypePrefabs)
//        {
//            if (go == type.Value)
//            {
//                Debug.Log("Hiding object using ObjectPool");
//                ObjectPool.RemoveObject(go);
//                return;
//            }
//        }

//        /// If not found among prefabs
//        Debug.Log("Destroying object without ObjectPool");
//        Destroy(go);
//    }
//}

#endregion