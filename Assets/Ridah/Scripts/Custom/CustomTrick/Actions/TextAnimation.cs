using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
public class TextAnimation : MonoBehaviour
{
    public enum Action
    {
        Size,
        LineSpace
    }
    public Vector2 variation = Vector2.zero;
    public Action action = Action.Size;
    public float speed = 25f;
    Text text;
    float nowTime=0;
    // Start is called before the first frame update
    void Start()
    { 
        text = GetComponent<Text>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        switch (action)
        {
            default:
            case Action.Size:
                LerpSize();
                break;
            case Action.LineSpace:
                LerpLineSpace();
                break;
        }
    }

    private void LerpSize()
    {
        text.fontSize = (int)Mathf.Lerp(variation.x, variation.y, (Time.time - nowTime)* speed*Time.deltaTime);
    }
    private void LerpLineSpace()
    {
        text.lineSpacing = Mathf.Lerp(variation.x, variation.y, (Time.time - nowTime) * speed * Time.deltaTime);
    }
    private void OnEnable()
    {
        nowTime = Time.time;
    }
    private void OnDisable()
    {
        text.fontSize = 1;
    }
    bool isSize() { return action == Action.Size; }
}
