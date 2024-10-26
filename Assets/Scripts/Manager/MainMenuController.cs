using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public GameObject optionsPopup;      // Reference to the options popup panel
    public Toggle musicToggle;           // Reference to the music toggle
    public AudioSource backgroundMusic;  // Reference to the AudioSource for background music

    private void Start()
    {
        // Load the saved music setting and apply it
        bool isMusicOn = PlayerPrefs.GetInt("MusicEnabled", 1) == 1;
        musicToggle.isOn = isMusicOn;
        ToggleMusic(isMusicOn);  // Apply the saved setting to the AudioSource
    }

    // Method for starting the game
    public void PlayGame()
    {
        SceneManager.LoadScene("GameScene");
    }

    // Show the options popup
    public void ShowOptions()
    {
        optionsPopup.SetActive(true);
    }

    // Close the options popup
    public void CloseOptions()
    {
        optionsPopup.SetActive(false);
    }

    // Toggle music on/off
    public void ToggleMusic(bool isMusicOn)
    {
        if (isMusicOn)
        {
            backgroundMusic.Play();
        }
        else
        {
            backgroundMusic.Pause();
        }

        // Save the setting
        PlayerPrefs.SetInt("MusicEnabled", isMusicOn ? 1 : 0);
    }

    // Quit the game
    public void QuitGame()
    {
        Application.Quit();
    }
}
