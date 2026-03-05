using TMPro;
using UnityEngine;

public class DifficultyDescriptionUpdater : MonoBehaviour
{
    public TMP_Dropdown difficultyDropdown;
    public TextMeshProUGUI descriptionText;

    void Start()
    {
        UpdateDescription(difficultyDropdown.value);
        difficultyDropdown.onValueChanged.AddListener(UpdateDescription);
    }

    void UpdateDescription(int index)
    {
        switch (index)
        {
            case 0: // Easy
                descriptionText.text = "Easy: Resources last longer and enemies appear less frequently.";
                break;

            case 1: // Normal
                descriptionText.text = "Normal: Balanced gameplay with moderate resource decay and enemy activity.";
                break;

            case 2: // Hard
                descriptionText.text = "Hard: Resources deplete quickly and enemies are much more aggressive.";
                break;
        }
    }
}