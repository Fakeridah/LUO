using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class LaserPool : MonoBehaviour
{
    public ObjectPool<GameObject> laserPool;
    public GameObject laserPrefab;

    public void start()
    {
        laserPool = new ObjectPool<GameObject>(CreatLaser, OnTakeDecalFromPool, OnReturnDecalToPool, OnDestroyDecal, false, 500, 1000);
    }

    private GameObject CreatLaser()
    {
        GameObject laser = Instantiate(laserPrefab);
        return laser;
    }
    private void OnTakeDecalFromPool(GameObject laser)
    {
        laser.gameObject.SetActive(true);
    }
    private void OnReturnDecalToPool(GameObject laser)
    {
        laser.gameObject.SetActive(false);
    }
    private void OnDestroyDecal(GameObject laser)
    {
        Destroy(laser.gameObject);
    }
}
