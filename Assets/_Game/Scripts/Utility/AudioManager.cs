using UnityEngine;

/// <summary>
/// Singleton that plays game audio clips loaded from Resources/Audio/.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private AudioSource audioSource;
    private AudioClip placeClip;
    private AudioClip perfectClip;
    private AudioClip gameOverClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        audioSource = gameObject.AddComponent<AudioSource>();
        LoadClips();
    }

    /// <summary>
    /// Loads audio clips from Resources/Audio/ folder.
    /// </summary>
    private void LoadClips()
    {
        placeClip = Resources.Load<AudioClip>("Audio/Place");
        perfectClip = Resources.Load<AudioClip>("Audio/Perfect");
        gameOverClip = Resources.Load<AudioClip>("Audio/GameOver");
    }

    /// <summary>
    /// Plays the block placement sound.
    /// </summary>
    public void PlayPlace()
    {
        if (placeClip != null)
            audioSource.PlayOneShot(placeClip);
    }

    /// <summary>
    /// Plays the perfect placement sound.
    /// </summary>
    public void PlayPerfect()
    {
        if (perfectClip != null)
            audioSource.PlayOneShot(perfectClip);
    }

    /// <summary>
    /// Plays the game over sound.
    /// </summary>
    public void PlayGameOver()
    {
        if (gameOverClip != null)
            audioSource.PlayOneShot(gameOverClip);
    }
}
