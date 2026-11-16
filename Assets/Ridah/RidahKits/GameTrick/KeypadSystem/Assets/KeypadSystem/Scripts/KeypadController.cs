using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GameplayIngredients;
using GameplayIngredients.Events;

[ RequireComponent(typeof(AudioSource))]
public class KeypadController : EventBase
{
    public string password;
    public int passwordLimit;
    public Text passwordText;
    [Header("Audio")]
    AudioSource audioSource;
    public AudioClip correctSound;
    public AudioClip wrongSound;
    [Space(10)]
    public Callable[] eventsTrue;
    public Callable[] eventsFalse;

    private void Start()
    {
        passwordText.text = "";
        audioSource = GetComponent<AudioSource>();
    }

    public void PasswordEntry(string number)
    {
        if (number == "Clear")
        {
            Clear();
            return;
        }
        else if(number == "Enter")
        {
            Enter();
            return;
        }

        int length = passwordText.text.ToString().Length;
        if(length<passwordLimit)
        {
            passwordText.text = passwordText.text + number;
        }
    }

    public void Clear()
    {
        passwordText.text = "";
        passwordText.color = Color.white;
    }

    private void Enter()
    {
        if (passwordText.text == password)
        {
            Callable.Call(eventsTrue, gameObject);

            if (audioSource != null)
                audioSource.PlayOneShot(correctSound);

            passwordText.color = Color.green;
            StartCoroutine(waitAndClear());
        }
        else
        {
            Callable.Call(eventsFalse, gameObject);

            if (audioSource != null)
                audioSource.PlayOneShot(wrongSound);

            passwordText.color = Color.red;
            StartCoroutine(waitAndClear());
        }
    }

    IEnumerator waitAndClear()
    {
        yield return new WaitForSeconds(0.75f);
        Clear();
    }


}
