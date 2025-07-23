using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class ObjectPoolManager : MonoBehaviour
{
    public enum PoolType
    {
        ParticleSystem = 0,
        Gameobjects = 1,
        SoundFX = 2,
    }

    [SerializeField] bool addToDontDestroyOnLoad = false;
    GameObject emptyHolder;

    static GameObject particaleSystemEmpty;
    static GameObject gameobjectsEmpty;
    static GameObject soundFXEmpty;

    static Dictionary<GameObject, ObjectPool<GameObject>> objectPools;
    static Dictionary<GameObject, GameObject> cloneToPrefabMap;

    public static PoolType PoolingType;
    void Awake()
    {
        objectPools = new();
        cloneToPrefabMap = new();

        SetupEmpties();
    }

    void SetupEmpties()
    {
        emptyHolder = new("Objects Pools");

        particaleSystemEmpty = new("Particle Effects");
        particaleSystemEmpty.transform.SetParent(emptyHolder.transform);

        gameobjectsEmpty = new("GameObjects");
        gameobjectsEmpty.transform.SetParent(emptyHolder.transform);

        soundFXEmpty = new("Sound FX");
        soundFXEmpty.transform.SetParent(emptyHolder.transform);

        if (addToDontDestroyOnLoad)
        {
            DontDestroyOnLoad(particaleSystemEmpty.transform.root);
        }
    }

    static void CreatePool(GameObject prefab, Vector3 pos, Quaternion rot, PoolType poolType = PoolType.Gameobjects)
    {
        ObjectPool<GameObject> pool = new(
            createFunc: () => CreateObject(prefab, pos, rot, poolType),
            actionOnGet: OnGetObject,
            actionOnRelease: OnReleaseObject,
            actionOnDestroy: OnDestroyObject
        );

        objectPools.Add(prefab, pool);
    }
    static void CreatePool(GameObject prefab, Transform parent, Quaternion rot, PoolType poolType = PoolType.Gameobjects)
    {
        ObjectPool<GameObject> pool = new(
            createFunc: () => CreateObject(prefab, parent, rot, poolType),
            actionOnGet: OnGetObject,
            actionOnRelease: OnReleaseObject,
            actionOnDestroy: OnDestroyObject
        );

        objectPools.Add(prefab, pool);
    }

    static GameObject CreateObject(GameObject prefab, Vector3 pos, Quaternion rot, PoolType poolType = PoolType.Gameobjects)
    {
        prefab.SetActive(false);
        GameObject obj = Instantiate(prefab, pos, rot);
        obj.SetActive(true);

        GameObject parentObject = SetParentObject(poolType);
        obj.transform.SetParent(parentObject.transform);

        return obj;
    }
    static GameObject CreateObject(GameObject prefab, Transform parent, Quaternion rot, PoolType poolType = PoolType.Gameobjects)
    {
        prefab.SetActive(false);
        GameObject obj = Instantiate(prefab, parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = rot;
        obj.transform.localScale = Vector3.one;

        obj.SetActive(true);

        return obj;
    }

    public static T SpawnObject<T>(T typePrefab, Vector3 spawnPos, Quaternion spawnRot, PoolType poolType = PoolType.Gameobjects) where T : Component
    {
        return SpawnObject<T>(typePrefab.gameObject, spawnPos, spawnRot, poolType);
    }
    public static T SpawnObject<T>(T typePrefab, Transform parent, Quaternion spawnRot, PoolType poolType = PoolType.Gameobjects) where T : Component
    {
        return SpawnObject<T>(typePrefab.gameObject, parent, spawnRot, poolType);
    }

    public static GameObject SpawnObject(GameObject objectToSpawn, Vector3 spawnPos, Quaternion spawnRot, PoolType poolType = PoolType.Gameobjects)
    {
        return SpawnObject<GameObject>(objectToSpawn, spawnPos, spawnRot, poolType);
    }
    public static GameObject SpawnObject(GameObject objectToSpawn, Transform parent, Quaternion spawnRot, PoolType poolType = PoolType.Gameobjects)
    {
        return SpawnObject<GameObject>(objectToSpawn, parent, spawnRot, poolType);
    }

    static T SpawnObject<T>(GameObject objectToSpawn, Vector3 spawnPos, Quaternion spawnRot, PoolType poolType = PoolType.Gameobjects) where T : Object
    {
        if (!objectPools.ContainsKey(objectToSpawn))
        {
            CreatePool(objectToSpawn, spawnPos, spawnRot, poolType);
        }

        GameObject obj = objectPools[objectToSpawn].Get();
        if (obj == null) return null;

        if (!cloneToPrefabMap.ContainsKey(obj))
        {
            cloneToPrefabMap.Add(obj, objectToSpawn);
        }

        obj.transform.position = spawnPos;
        obj.transform.rotation = spawnRot;
        obj.SetActive(true);

        if (typeof(T) == typeof(GameObject))
        {
            return obj as T;
        }

        if (obj.TryGetComponent(out T component))
        {
            return component;
        }

        Debug.LogError($"Object {objectToSpawn.name} doesn't have component of type {typeof(T)}");
        return null;
    }
    static T SpawnObject<T>(GameObject objectToSpawn, Transform parent, Quaternion spawnRot, PoolType poolType = PoolType.Gameobjects) where T : Object
    {
        if (!objectPools.ContainsKey(objectToSpawn))
        {
            CreatePool(objectToSpawn, parent, spawnRot, poolType);
        }

        GameObject obj = objectPools[objectToSpawn].Get();
        if (obj == null) return null;

        if (!cloneToPrefabMap.ContainsKey(obj))
        {
            cloneToPrefabMap.Add(obj, objectToSpawn);
        }

        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = spawnRot;

        obj.SetActive(true);

        if (typeof(T) == typeof(GameObject))
        {
            return obj as T;
        }

        if (obj.TryGetComponent(out T component))
        {
            return component;
        }

        Debug.LogError($"Object {objectToSpawn.name} doesn't have component of type {typeof(T)}");
        return null;
    }

    public static void ReturnObjectToPool(GameObject obj, PoolType poolType = PoolType.Gameobjects)
    {
        if (cloneToPrefabMap.TryGetValue(obj, out GameObject prefab))
        {
            GameObject parentObject = SetParentObject(poolType);
            if (obj.transform.parent != parentObject.transform)
            {
                obj.transform.SetParent(parentObject.transform);
            }

            if (objectPools.TryGetValue(prefab, out ObjectPool<GameObject> pool))
            {
                pool.Release(obj);
            }
        }
        else
        {
            Debug.LogWarning($"Trying to return an object that is not pooled: {obj.name}");
        }
    }

    static GameObject SetParentObject(PoolType poolType)
    {
        switch (poolType)
        {
            case PoolType.ParticleSystem:
                return particaleSystemEmpty;
            case PoolType.Gameobjects:
                return gameobjectsEmpty;
            case PoolType.SoundFX:
                return soundFXEmpty;
            default:
                return null;
        }
    }
    static void OnGetObject(GameObject obj) { }
    static void OnReleaseObject(GameObject obj)
    {
        obj.SetActive(false);
    }
    static void OnDestroyObject(GameObject obj)
    {
        if (cloneToPrefabMap.ContainsKey(obj))
        {
            cloneToPrefabMap.Remove(obj);
        }
    }

}
