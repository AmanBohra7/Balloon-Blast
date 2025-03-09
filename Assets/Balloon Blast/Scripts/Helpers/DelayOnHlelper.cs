using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DelayOnHlelper : MonoBehaviour
{
    [SerializeField] List<GameObject> balloons;

    private void Start()
    {
        StartCoroutine(WaitAndExecute(2f, () =>
        {
            foreach (GameObject item in balloons)
            {
                item.SetActive(true);
            }
        }));
    }

    IEnumerator WaitAndExecute(float delay,Action callback)
    {
        yield return delay;
        callback?.Invoke(); 
    }
}
