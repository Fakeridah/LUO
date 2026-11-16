using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Pool;

namespace Ridah.Decal
{
    public class Printer : MonoBehaviour
    {
       
        // MonoBehaviours must be assigned from a scene/prefab, never constructed with new.
        public DecalContorller prints;
       
        public LayerMask[] printLayers;
        
        public string[] printTags;

        public string[] TagExculded;

        public PrintSelection printMethod;

        private ProjectionPool pool;

        #region Properties
       
        public PrintParent parent;
        
        public float frequencyTime;
      
        public float frequencyDistance;

        [SerializeField]
        [Tooltip("didn't implement")]
        protected PrinterOverlap[] overlaps = new PrinterOverlap[0];
        #endregion

        private float timeSincePrint = Mathf.Infinity;
        private Vector3 lastPrintPos = Vector3.zero;
        public void init()
        {
            pool = new ProjectionPool() ;
            pool.decalContorller = prints; //Maybe other way better
            pool.start();
        }
        private void Update()
        {
            timeSincePrint += Time.deltaTime;
        }

        public void Print(Vector3 Position, Quaternion Rotation, Transform Surface, int Layer = 0)
        {
            //Projection Check
            if (prints == null || prints.decals.Length < 1)
            {
                Debug.LogError("No Projections to print. Please set at least one projection to print.");
                return;
            }

            //Frequency Check
            if (timeSincePrint >= frequencyTime && Vector3.Distance(Position, lastPrintPos) >= frequencyDistance)
            {
                //Print using print method
                switch (printMethod)
                {
                    case PrintSelection.Layer:
                        if (printLayers == null || printLayers.Length == 0)
                        {
                            PrintProjection(prints.decals[0], Position, Rotation, Surface);
                        }
                        else
                        {
                            //If the layer mask contains the hit layer, print the decal associated with it.
                            for (int i = 0; i < printLayers.Length; i++)
                            {
                                if (printLayers[i] == (printLayers[i] | (1 << Layer))) PrintProjection(prints.decals[i], Position, Rotation, Surface);
                            }
                        }
                        break;
                    case PrintSelection.Tag:
                        if (printTags == null || printTags.Length == 0)
                        {
                            PrintProjection(prints.decals[0], Position, Rotation, Surface);
                        }
                        else
                        {
                            bool printed = false;
                            //If the surface is of the tag, print the decal associated with it.
                            for (int i = 0; i < printTags.Length; i++)
                            {
                                if (printTags[i] == Surface.tag)
                                {
                                    PrintProjection(prints.decals[i], Position, Rotation, Surface);
                                    printed = true;
                                }
                            }

                            //If the surface has no relevant tag, print the default print.
                            if (!printed)
                            {
                                PrintProjection(prints.decals[0], Position, Rotation, Surface);
                            }
                        }
                        break;
                    case PrintSelection.Random:
                        //Generate an int between one and the prints length
                        int index = Random.Range(0, prints.decals.Length);
                        //Print the projection at that index
                        PrintProjection(prints.decals[index], Position, Rotation, Surface);
                        break;
                    case PrintSelection.All:
                        //Print each projection once
                        foreach (DecalProjector projection in prints.decals)
                        {
                            PrintProjection(projection, Position, Rotation, Surface);
                        }
                        break;
                    case PrintSelection.TagExcluded:
                        if (TagExculded == null || TagExculded.Length == 0)
                        {
                            PrintProjection(prints.decals[0], Position, Rotation, Surface);
                        }
                        else
                        {
                            bool print = true;
                            //If the surface is of the tag, print the decal associated with it.
                            for (int i = 0; i < TagExculded.Length; i++)
                            {
                                if (TagExculded[i] == Surface.tag)
                                {
                                    print = false;
                                }
                            }
                            if (print)
                            {
                                PrintProjection(prints.decals[0], Position, Rotation, Surface);
                            }
                        }
                        break;
                }

                //Destroy on print
                //if (destroyOnPrint) Destroy(gameObject);

                //Cache frequency data
                timeSincePrint = 0;
                lastPrintPos = Position;
            }
        }
        private void PrintProjection(DecalProjector Projection, Vector3 Position, Quaternion Rotation, Transform Surface)
        { 
            if (Projection != null)
            {
                DecalProjector proj = pool.decalPool.Get().decals[0];

                //Set Transform Data
                proj.transform.position = Position;
                proj.transform.rotation = Rotation;

                //Set Parent
                if (parent == PrintParent.Surface)
                {
                    //Create a sub parent
                    //Fixes Non-Uniform scaling and is generally cleaner
                    Transform subParent = null;
                    foreach (Transform child in Surface)
                    {
                        if (child.name == "Projections") subParent = child;
                    }
                    if (subParent == null)
                    {
                        subParent = new GameObject("Projections").transform;
                        subParent.SetParent(Surface);
                    }
                    proj.transform.SetParent(subParent);
                }
            }
        }
    }
    //Overlap
    [System.Serializable]
    public struct PrinterOverlap
    {
        public int poolId;
        public float intersectionStrength;
    }

    //Parent type selection
    public enum PrintParent { Default, Surface }
    //Print selection method
    public enum PrintSelection { All, Random, Layer, Tag, TagExcluded }
}
