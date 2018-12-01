using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utility
{
    /// <summary>
    /// Use with void-methods or custom ones via lambda-functions, 
    /// syntax: () => { foo }
    /// </summary>
    public static IEnumerator DelayedAction(float time, System.Action callback)
    {
        //Debug.Log("Action delay : started");
        yield return new WaitForSecondsRealtime(time);
        //Debug.Log("Action delay : ended");
        callback();
        yield break;
    }

    public static IEnumerator WaitForObject(object obj, System.Action callback)
    {
        while (obj == null)
        {
            yield return null;
        }
        callback();
        yield break;
    }

    public static string LogArray(System.Array array)
    {
        string msg = "";
        foreach (var item in array)
        {
            msg += "\n" + item;
        }
        msg += "\n";
        return msg;
    }
}
