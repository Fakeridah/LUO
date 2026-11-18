using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomImagePop : MonoBehaviour
{
    public GameObject image;
    public int imageTotalNum = 100;
    public AnimationCurve curve;
    public Vector2 canvasXY = new Vector2(960, 540);
    Vector2 randomPos = new Vector2(0, 0);
    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < imageTotalNum; i++)
        {
            GameObject obj = Instantiate(image, transform);
            obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(Random.Range(-canvasXY.x, canvasXY.x), Random.Range(-canvasXY.y, canvasXY.y));
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
