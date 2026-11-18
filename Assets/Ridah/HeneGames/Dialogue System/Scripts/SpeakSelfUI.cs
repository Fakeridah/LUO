using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HeneGames.DialogueSystem
{
    public class SpeakSelfUI : MonoBehaviour
    {
        #region Singleton

        public static SpeakSelfUI instance { get; private set; }

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

        #endregion
        [HideInInspector]
        public DialogSpeakSelf currentDialogueManager;

        [Header("References")]
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private GameObject dialogueWindow;

        [Header("Settings")]
        [SerializeField] private bool animateText = true;

        [Range(0.1f, 1f)]
        [SerializeField] private float textAnimationSpeed = 0.5f;

        [Header("Next sentence input")]
        public KeyCode actionInput = KeyCode.Space;

        [SerializeField] internal GameObject playerCanvas;

        [HideInInspector]
        public bool manual=true;
        private void Start()
        {
            //Hide dialogue and interaction UI at start
            dialogueWindow.SetActive(false);
        }
        private void Update()
        {
            //Next dialogue input
            if (Input.GetKeyDown(actionInput)&& manual)
            {
                NextSentence();
            }
        }
        public void NextSentence()
        {
            //Continue only if we have dialogue
            if (currentDialogueManager == null)
                return;

            //Tell the current dialogue manager to display the next sentence. This function also gives information if we are at the last sentence
            currentDialogueManager.NextSentence(out bool lastSentence);

            //If last sentence remove current dialogue manager
            if (lastSentence)
            {
                currentDialogueManager = null;
            }
        }

        public void StartDialogue(DialogSpeakSelf _dialogueManager)
        {
            //Store dialogue manager
            currentDialogueManager = _dialogueManager;

            //Start displaying dialogue
            currentDialogueManager.StartDialogue();
        }

        public void ShowSentence(string _message)
        {
            StopAllCoroutines();

            dialogueWindow.SetActive(true);

            if (animateText)
            {
                StartCoroutine(WriteTextToTextmesh(_message, messageText));
            }
            else
            {
                messageText.text = _message;
            }
        }

        public void ClearText()
        {
            dialogueWindow.SetActive(false);
        }

        IEnumerator WriteTextToTextmesh(string _text, TextMeshProUGUI _textMeshObject)
        {
            _textMeshObject.text = "";
            char[] _letters = _text.ToCharArray();

            float _speed = 1f - textAnimationSpeed;

            foreach (char _letter in _letters)
            {
                _textMeshObject.text += _letter;
                yield return new WaitForSeconds(0.1f * _speed);
            }
        }
    }
}