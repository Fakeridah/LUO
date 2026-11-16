using UnityEngine;
using UnityEngine.UI;
using RidahFPS.Controllers;
using GameplayIngredients;
using GameplayIngredients.Events;
public class PlayerAim : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    private Transform cam;
    private LZM player;
    private Button button;
    private SliderMagnetControl slider;
    private float slideValue=0;
    private bool buttonLag = false;
    private void Start()
    {
        cam = Camera.main.transform;
        player = LZM.Player; 
    }
    private void Update()
    {
        if (button != null) button.image.color = button.colors.normalColor;
        button = null;
        RaycastHit hit;
        if (Physics.Raycast(cam.position, cam.TransformDirection(Vector3.forward), out hit, Mathf.Infinity, layerMask) || buttonLag)
        {
            //print(hit.transform.name);
            float distance = Vector3.Distance(cam.position, hit.transform.position);

            //KeypadKey
            if (distance <= 3f)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    if (hit.transform.GetComponent<KeypadKey>() != null)
                    {
                        hit.transform.GetComponent<KeypadKey>().SendKey();
                    }
                }
            }

            //RaycastInteract
            if (hit.transform.GetComponent<RaycastInteract>() != null)
            {
                if (distance < hit.transform.GetComponent<RaycastInteract>().pickupDist)
                    hit.transform.GetComponent<RaycastInteract>().trigger = true;
            }
            else if (hit.transform.GetComponentInChildren<RaycastInteract>() != null&&!hit.transform.CompareTag("Enemy"))
            {
                if (distance < hit.transform.GetComponentInChildren<RaycastInteract>().pickupDist)
                    hit.transform.GetComponentInChildren<RaycastInteract>().trigger = true;
            }
            else if (hit.transform.GetComponent<Button>())
            {
                button = hit.transform.GetComponent<Button>();
                button.image.color = button.colors.highlightedColor;
                if (Input.GetMouseButton(0))
                {
                    button.image.color = button.colors.pressedColor;
                }
                if (Input.GetMouseButtonDown(0))
                {
                    button.onClick.Invoke();
                }
            }
            else if (hit.transform.GetComponent<SliderMagnetControl>()|| buttonLag)
            {
                if(hit.transform.GetComponent<SliderMagnetControl>())
                slider = hit.transform.GetComponent<SliderMagnetControl>();
                if (Input.GetMouseButton(0))
                {
                    buttonLag = true;
                    slider.sliderValue +=Input.GetAxisRaw("Mouse X")*Time.deltaTime*2;
                    slider.sliderValue = Mathf.Clamp01(slider.sliderValue);
                    slider.mw.Permeability = slider.sliderValue * .1f;
                    slider.handle.anchorMin = new Vector2(slider.sliderValue, 0);
                    slider.handle.anchorMax = new Vector2(slider.sliderValue, 1);
                    slider.fill.anchorMax = new Vector2(slider.sliderValue * 1.1f, 1.1f);
                }
                if (Input.GetMouseButtonUp(0)) { buttonLag = false; slider = null; }
            }
        }
    }
}

