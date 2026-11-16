using UnityEngine;

namespace RidahFPS.Controllers
{
    public enum GokzPistolType
    {
        Disabled,
        Glock,
        Hkp2000,
        UspS,
        P250,
        Tec9,
        FiveSeven,
        DualBerettas,
        Deagle
    }

    /// <summary>
    /// Unity-side pistol option corresponding to gokz-pistol. The actual
    /// weapon prefab remains project-owned; this component only handles the
    /// mode's selected secondary and safely replaces the optional instance.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Pistol")]
    public sealed class GokzPistol : MonoBehaviour
    {
        [SerializeField] private GokzPistolType selectedPistol;
        [SerializeField] private GameObject[] pistolPrefabs;
        [SerializeField] private Transform weaponMount;

        private GameObject _currentPistol;

        public GokzPistolType SelectedPistol => selectedPistol;

        public void SetPistol(GokzPistolType pistol)
        {
            selectedPistol = pistol;
            Equip();
        }

        public void Equip()
        {
            if (_currentPistol != null)
                Destroy(_currentPistol);
            _currentPistol = null;
            int index = (int)selectedPistol - 1;
            if (index < 0 || pistolPrefabs == null || index >= pistolPrefabs.Length || weaponMount == null)
                return;
            _currentPistol = Instantiate(pistolPrefabs[index], weaponMount);
            _currentPistol.transform.localPosition = Vector3.zero;
            _currentPistol.transform.localRotation = Quaternion.identity;
        }
    }
}
