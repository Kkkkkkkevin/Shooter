﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Cartoon FX  - (c) 2012-2016 Jean Moreno

// Spawn System:
// Preload GameObject to reuse them later, avoiding to Instantiate them.
// Very useful for mobile platforms.


//A utiliser avec l'inspector
public class GamePool : MonoBehaviour
{
    static public GameObject GetNextObject(GameObject sourceObj, Vector3 position, Quaternion rotation)
    {
        GameObject o = GetNextObject(sourceObj, true);
        o.transform.position = position;
        o.transform.rotation = rotation;
        return o;
    }

    static public GameObject GetNextObject(GameObject sourceObj, Transform trans)
    {
        GameObject o = GetNextObject(sourceObj, true);
        o.transform.position = trans.position;
        o.transform.rotation = trans.rotation;
        return o;
    }
    /// <summary>
    /// Get the next available preloaded Object.
    /// </summary>
    /// <returns>
    /// The next available preloaded Object.
    /// </returns>
    /// <param name='sourceObj'>
    /// The source Object from which to get a preloaded copy.
    /// </param>
    /// <param name='activateObject'>
    /// Activates the object before returning it.
    /// </param>
    static public GameObject GetNextObject(GameObject sourceObj, bool activateObject = true)
    {
        int uniqueId = sourceObj.GetInstanceID();

        if (!instance.poolCursors.ContainsKey(uniqueId))
        {
            Debug.LogError("[ObjectPpol.GetNextObject()] Object hasn't been preloaded: " + sourceObj.name + " (ID:" + uniqueId + ")\n", instance);
            return null;
        }

        int cursor = instance.poolCursors[uniqueId];
        GameObject returnObj = null;
        if (instance.onlyGetInactiveObjects)
        {
            int loop = cursor;
            while (true)
            {
                returnObj = instance.instantiatedObjects[uniqueId][cursor];
                instance.increasePoolCursor(uniqueId);
                cursor = instance.poolCursors[uniqueId];

                if (returnObj != null && !returnObj.activeSelf)
                    break;

                //complete loop: no active instance available
                if (cursor == loop)
                {
                    if (instance.instantiateIfNeeded)
                    {
                        Debug.Log("[ObjectPpol.GetNextObject()] A new instance has been created for \"" + sourceObj.name + "\" because no active instance were found in the pool.\n", instance);
                        PreloadObject(sourceObj);
                        var list = instance.instantiatedObjects[uniqueId];
                        returnObj = list[list.Count - 1];
                        break;
                    }
                    else
                    {
                        Debug.LogWarning("[ObjectPpol.GetNextObject()] There are no active instances available in the pool for \"" + sourceObj.name + "\"\nYou may need to increase the preloaded object count for this prefab?", instance);
                        return null;
                    }
                }
            }
        }
        else
        {
            returnObj = instance.instantiatedObjects[uniqueId][cursor];
            instance.increasePoolCursor(uniqueId);
        }

        if (activateObject && returnObj != null)
            returnObj.SetActive(true);

        return returnObj;
    }

    /// <summary>
    /// Preloads an object a number of times in the pool.
    /// </summary>
    /// <param name='sourceObj'>
    /// The source Object.
    /// </param>
    /// <param name='poolSize'>
    /// The number of times it will be instantiated in the pool (i.e. the max number of same object that would appear simultaneously in your Scene).
    /// </param>
    static public void PreloadObject(GameObject sourceObj, int poolSize = 1)
    {
        instance.addObjectToPool(sourceObj, poolSize);
    }

    /// <summary>
    /// Unloads all the preloaded objects from a source Object.
    /// </summary>
    /// <param name='sourceObj'>
    /// Source object.
    /// </param>
    static public void UnloadObjects(GameObject sourceObj)
    {
        instance.removeObjectsFromPool(sourceObj);
    }

    /// <summary>
    /// Gets a value indicating whether all objects defined in the Editor are loaded or not.
    /// </summary>
    /// <value>
    /// <c>true</c> if all objects are loaded; otherwise, <c>false</c>.
    /// </value>
    static public bool AllObjectsLoaded
    {
        get
        {
            return instance.allObjectsLoaded;
        }
    }

    // INTERNAL SYSTEM ----------------------------------------------------------------------------------------------------------------------------------------

    static private GamePool instance;

    public GameObject[] objectsToPreload = new GameObject[0];
    public int[] objectsToPreloadTimes = new int[0];
    public bool hideObjectsInHierarchy = false;
    public bool spawnAsChildren = true;
    public bool onlyGetInactiveObjects = false;
    public bool instantiateIfNeeded = false;

    private bool allObjectsLoaded;
    private Dictionary<int, List<GameObject>> instantiatedObjects = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, int> poolCursors = new Dictionary<int, int>();

    private void addObjectToPool(GameObject sourceObject, int number)
    {
        int uniqueId = sourceObject.GetInstanceID();

        //Add new entry if it doesn't exist
        if (!instantiatedObjects.ContainsKey(uniqueId))
        {
            instantiatedObjects.Add(uniqueId, new List<GameObject>());
            poolCursors.Add(uniqueId, 0);
        }

        //Add the new objects
        GameObject newObj;
        for (int i = 0; i < number; i++)
        {
            newObj = (GameObject)Instantiate(sourceObject);
            newObj.SetActive(false);

            instantiatedObjects[uniqueId].Add(newObj);

            if (hideObjectsInHierarchy)
                newObj.hideFlags = HideFlags.HideInHierarchy;

            if (spawnAsChildren)
                newObj.transform.parent = this.transform;
        }
    }

    private void removeObjectsFromPool(GameObject sourceObject)
    {
        int uniqueId = sourceObject.GetInstanceID();

        if (!instantiatedObjects.ContainsKey(uniqueId))
        {
            Debug.LogWarning("[ObjectPool.removeObjectsFromPool()] There aren't any preloaded object for: " + sourceObject.name + " (ID:" + uniqueId + ")\n", this.gameObject);
            return;
        }

        //Destroy all objects
        for (int i = instantiatedObjects[uniqueId].Count - 1; i >= 0; i--)
        {
            GameObject obj = instantiatedObjects[uniqueId][i];
            instantiatedObjects[uniqueId].RemoveAt(i);
            GameObject.Destroy(obj);
        }

        //Remove pool entry
        instantiatedObjects.Remove(uniqueId);
        poolCursors.Remove(uniqueId);
    }

    private void increasePoolCursor(int uniqueId)
    {
        instance.poolCursors[uniqueId]++;
        if (instance.poolCursors[uniqueId] >= instance.instantiatedObjects[uniqueId].Count)
        {
            instance.poolCursors[uniqueId] = 0;
        }
    }

    //--------------------------------

    void Awake()
    {
        if (instance != null)
            Debug.LogWarning("ObjectPool: There should only be one instance of ObjectPool per Scene!\n", this.gameObject);

        instance = this;
    }

    void Start()
    {
        allObjectsLoaded = false;

        for (int i = 0; i < objectsToPreload.Length; i++)
        {
            PreloadObject(objectsToPreload[i], objectsToPreloadTimes[i]);
        }

        allObjectsLoaded = true;
    }
}