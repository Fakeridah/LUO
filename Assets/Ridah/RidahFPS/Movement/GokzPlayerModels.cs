using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Local model visibility counterpart of gokz-playermodels. Model sets
    /// are optional; the component never replaces the project's existing
    /// character model unless a set is explicitly assigned.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Player Models")]
    public sealed class GokzPlayerModels : MonoBehaviour
    {
        [SerializeField] private GameObject[] modelSets;
        [SerializeField] private int selectedModel;

        public int SelectedModel => selectedModel;

        public void SetModel(int index)
        {
            if (modelSets == null || modelSets.Length == 0)
                return;
            selectedModel = Mathf.Clamp(index, 0, modelSets.Length - 1);
            for (int i = 0; i < modelSets.Length; i++)
            {
                if (modelSets[i] != null)
                    modelSets[i].SetActive(i == selectedModel);
            }
        }
    }
}
