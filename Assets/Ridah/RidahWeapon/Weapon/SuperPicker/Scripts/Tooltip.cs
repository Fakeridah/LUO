using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Tooltip : MonoBehaviour
{
    public GameObject tooptipText;
    public GameObject cursorDefault;
    public GameObject cursorHand;
    public GameObject handGrabbed;
    public GameObject cursorFinger;
    public GameObject fingerGrabbed;

    private Camera cameraFPS;
    private SuperEnginal superEnginal;
    private bool haveOutline = false;
    private GameObject target;
    void Start() {
        superEnginal = GetComponent<SuperEnginal>();
        cameraFPS = GetComponent<SuperEnginal>().cameraFPS;
    }

    void Update() {
        if (superEnginal.target != null && superEnginal.target.gameObject.activeInHierarchy) {
            TooltipCleanUp();
            cursorDefault.GetComponent<CanvasGroup>().alpha =0;
            SuperProp sp = superEnginal.target.GetComponent<SuperProp>();
            if (sp != null ) {
                if (sp.outline&&superEnginal.target.GetComponent<Outline>()) {
                    Outline OutLine = superEnginal.target.GetComponent<Outline>();
                    OutLine.OutlineColor = sp.holdColor;
                    OutLine.enabled = true;
                }
            }
            if (superEnginal.target.tag != "Clicker")
            {
                handGrabbed.GetComponent<CanvasGroup>().alpha = 1;
                handGrabbed.SetActive(true);
            }
            else fingerGrabbed.GetComponent<CanvasGroup>().alpha = 1;
        } else
        {
            TooltipCleanUp();
            RaycastHit hit;
            if (Physics.Raycast(cameraFPS.transform.position, cameraFPS.transform.forward, out hit, superEnginal.maxScaleDistance,superEnginal.raycastMask))
            {
                if (1<<hit.transform.gameObject.layer == LayerMask.GetMask(superEnginal.targetMask[0]))
                {
                    target = hit.transform.gameObject;
                    SuperProp sp = target.GetComponent<SuperProp>();
                    if (sp != null)//change!!!!!!
                    {
                        Outline OutLine = target.GetComponent<Outline>();
                        haveOutline = sp.outline && OutLine != null;
                        if (haveOutline)
                        {
                            OutLine.OutlineColor = sp.pointedColor;
                            OutLine.enabled = true;
                        }
                        cursorDefault.GetComponent<CanvasGroup>().alpha = 0;
                        tooptipText.GetComponent<Text>().text = sp.objectName;
                        tooptipText.GetComponent<CanvasGroup>().alpha = 1;
                        if (target.tag != "Clicker")
                            cursorHand.GetComponent<CanvasGroup>().alpha = 1;
                        else cursorFinger.GetComponent<CanvasGroup>().alpha = 1;

                    }
                }
            }
        }
    }
    void TooltipCleanUp()
    {
        if (target != null && haveOutline)
        {
            target.GetComponent<Outline>().enabled = false;
            haveOutline = false;
        }
        tooptipText.GetComponent<CanvasGroup>().alpha = 0;
        cursorDefault.GetComponent<CanvasGroup>().alpha = 1;
        cursorHand.GetComponent<CanvasGroup>().alpha = 0;
        handGrabbed.GetComponent<CanvasGroup>().alpha = 0;
        handGrabbed.SetActive(false);
        cursorFinger.GetComponent<CanvasGroup>().alpha = 0;
        fingerGrabbed.GetComponent<CanvasGroup>().alpha = 0;
        target = null;
    }
}
