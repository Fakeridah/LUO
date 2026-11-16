using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine.Pool;

namespace Ridah.Decal
{
    public class ProjectionPool: Printer
    {
        public ObjectPool<DecalContorller> decalPool;
        public DecalContorller decalContorller;
        public void start()
        {
            decalPool = new ObjectPool<DecalContorller>(CreatDecal, OnTakeDecalFromPool, OnReturnDecalToPool, OnDestroyDecal, false, 500, 1000);
        }

        private DecalContorller CreatDecal()
        {
            DecalContorller decal = Instantiate(decalContorller);
            decal.SetPool(decalPool);
            return decal;
        }
        private void OnTakeDecalFromPool(DecalContorller decal)
        {
            decal.gameObject.SetActive(true); 
        }
        private void OnReturnDecalToPool(DecalContorller decal)
        {
            decal.gameObject.SetActive(false);
        }
        private void OnDestroyDecal(DecalContorller decal)
        {
            Destroy(decal.gameObject);
        }
    }
}