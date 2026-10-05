using TMPro;
using UnityEngine;

public class Loading : MonoBehaviour
{

    public UITweenerController controller;

    [SerializeField] private TMP_Text _bodyText;

    private void Start()
    {
        if (controller == null)
        {
            Debug.LogError($"UI Tweener Controller is not assigned to {this.name}. Kindly assign the reference.");
            return;
        }
    }

    public void Show() => controller.Show();
    public void Close() => controller.SetInactive();
    public void SetText(string text) => _bodyText.text = text;
}