using Oculus.Interaction.Input;
using System.Collections.Generic;
using UnityEngine;

public class HandItemHandler : MonoBehaviour
{

    [SerializeField] GameObject item;

    private bool _isShowing = false;

    public Vector3 offset;

    [SerializeField] List<GameObject> weapons;

    private int _index = -1;


    public int ShowItem()
    {
        if (_isShowing) return _index;
        _isShowing = true;
        ++_index;
        if (_index == weapons.Count) _index = 0;

        for(int i = 0; i < weapons.Count; i++)
        {
            if(i == _index) weapons[i].SetActive(true);
            else weapons[i].SetActive(false);
        }

        item.SetActive(true);
        return _index;
    }

    public void UpdateItem(int index)
    {
        item.SetActive(index != -1);
        for (int i = 0; i < weapons.Count; i++)
        {
            if (i == index) weapons[i].SetActive(true);
            else weapons[i].SetActive(false);
        }
    }

    public void hideItem()
    {
        if (!_isShowing) return;
        _isShowing = false;

        item.SetActive(false);  
    }

    public void UpdatePose(Pose fingeTip)
    {
        item.transform.localPosition = fingeTip.position;
        item.transform.localRotation = fingeTip.rotation;
        item.transform.eulerAngles += offset;
    }

}
