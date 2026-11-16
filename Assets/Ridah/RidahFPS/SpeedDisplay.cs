using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RidahFPS.Controllers;
public class SpeedDisplay : MonoBehaviour
{
    [SerializeField]
     GameObject surfPlayer;
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        GetComponent<Text>().text = surfPlayer.GetComponent<LZM>().MoveData.Velocity.magnitude.ToString();
        print(surfPlayer.GetComponent<LZM>().MoveData.Velocity.magnitude.ToString());
    }
}
