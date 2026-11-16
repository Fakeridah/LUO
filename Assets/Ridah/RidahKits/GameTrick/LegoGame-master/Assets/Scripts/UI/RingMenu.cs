using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GameplayIngredients;
using GameplayIngredients.Events;
public class RingMenu : EventBase
{
    public static RingMenu instance { get; private set; }

    public int SegmentCount = 8;
    [HideInInspector]
    public List<RingElement> Elements;
    public RingCakePiece RingCakePiecePrefab;
    public float GapWidthDegree = 1f;
    protected RingCakePiece[] Pieces;
    protected RingMenu Parent;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        initPiece();
    }

    private void Update()
    {
        var stepLength = 360f / SegmentCount;
        var mouseAngle = NormalizeAngle(Vector3.SignedAngle(Vector3.up, Input.mousePosition - transform.position, Vector3.forward) - stepLength / 2f);
        var activeElement = (int)(mouseAngle / stepLength);

        for (int i = 0; i < Elements.Count; i++)
        {
            if(i == activeElement)
                Pieces[i].CakePiece.color = new Color(1f, 1f, 1f, 0.75f);
            else if(i< SegmentCount)
                Pieces[i].CakePiece.color = new Color(1f, 1f, 1f, 0.5f);
        }

        if (Input.GetMouseButtonDown(0)&& activeElement< Elements.Count)
        {
            if (Elements[activeElement].NextRing != null)
            {
                var newSubRing = Instantiate(gameObject, transform.parent).GetComponent<RingMenu>();
                newSubRing.Parent = this;
                for (var j = 0; j < newSubRing.transform.childCount; j++)
                    Destroy(newSubRing.transform.GetChild(j).gameObject);
                newSubRing.Elements = Elements[activeElement].NextRing.Elements;
            }
            else
            {
                if(Elements[activeElement].trigger)
                Callable.Call(Elements[activeElement].events);
            }
            gameObject.SetActive(false);
        }
    }

    private float NormalizeAngle(float a) => (-a + 360f) % 360f;
    bool doOnce = true;
    public void initPiece()
    {
        var stepLength = 360f / SegmentCount;
        var iconDist = Vector3.Distance(RingCakePiecePrefab.Icon.transform.position, RingCakePiecePrefab.CakePiece.transform.position);

        //Position it
        if (doOnce)
        Pieces = new RingCakePiece[SegmentCount];
        for (int i = 0; i < SegmentCount; i++)
        {
            if (doOnce)
            {
                Pieces[i] = Instantiate(RingCakePiecePrefab, transform);
                //set root element
                Pieces[i].transform.localPosition = Vector3.zero;
                Pieces[i].transform.localRotation = Quaternion.identity;

                //set cake piece
                Pieces[i].CakePiece.fillAmount = 1f / SegmentCount - GapWidthDegree / 360f;
                Pieces[i].CakePiece.transform.localPosition = Vector3.zero;
                Pieces[i].CakePiece.transform.localRotation = Quaternion.Euler(0, 0, -stepLength / 2f + GapWidthDegree / 2f - i * stepLength);
                Pieces[i].CakePiece.color = new Color(1f, 1f, 1f, 0.5f);
            }

            //set icon
            Pieces[i].Icon.sprite = null;
            Pieces[i].Icon.transform.localPosition = Pieces[i].CakePiece.transform.localPosition + Quaternion.AngleAxis(-i * stepLength, Vector3.forward) * Vector3.up * iconDist;
            if (i < Elements.Count)
                Pieces[i].Icon.sprite = Elements[i].Icon;

        }
        doOnce = false;
    }
}
