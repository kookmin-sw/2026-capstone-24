using UnityEngine;
using UnityEngine.UI;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Tab Panel Controller")]
    public class TabPanelController : MonoBehaviour
    {
        [SerializeField] Button[] tabButtons;
        [SerializeField] GameObject[] tabPanels;
        [SerializeField] int defaultTab = 0;

        void Start()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int idx = i;
                if (tabButtons[i] != null)
                    tabButtons[i].onClick.AddListener(() => SelectTab(idx));
            }
            SelectTab(defaultTab);
        }

        public void SelectTab(int index)
        {
            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i] != null)
                    tabPanels[i].SetActive(i == index);
            }
        }
    }
}
