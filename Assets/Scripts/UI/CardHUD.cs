using TMPro;
using UnityEngine;

namespace GrimoireOfTheVoid.UI
{
    [DisallowMultipleComponent]
    public sealed class CardHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text cardNameText;
        [SerializeField] private TMP_Text cardDescText;

        private void OnEnable()
        {
            CardDraw.OnCardInfo += OnCardInfo;
        }

        private void OnDisable()
        {
            CardDraw.OnCardInfo -= OnCardInfo;
        }

        private void OnCardInfo(string name, string desc)
        {
            if (cardNameText != null) cardNameText.text = name ?? "";
            if (cardDescText != null) cardDescText.text = desc ?? "";
        }
    }
}

