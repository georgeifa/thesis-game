using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The results screen shown when the last soldier is lost: a failure headline,
/// the run's stats, and the two things a player wants next — retry or quit out.
///
/// ── TIME SCALE ──────────────────────────────────────────────────────────
/// The screen freezes the game with Time.timeScale = 0 rather than disabling
/// systems one by one. That stops enemies, coroutines on WaitForSeconds and
/// animations in a single line, and it cannot leave something running by
/// omission — which is the failure mode the enemy spawn contract exists to
/// prevent on the other side.
///
/// Anything that must keep working while frozen (UI animation, a sound) has to
/// use unscaled time. Both buttons restore the time scale before loading,
/// because a scene loaded at timeScale 0 opens frozen and looks broken.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    #region Inspector

    [Header("Root")]
    [Tooltip("The panel to show. Kept as a child so this component can live on " +
             "an always-active object and still hide the UI.")]
    [SerializeField] private GameObject panel;

    [Header("Headline")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Shown as the headline. 'MISSION FAILED' reads better than 'YOU " +
             "DIED' when the run ends because reinforcements ran out rather " +
             "than because one soldier fell.")]
    [SerializeField] private string titleMessage = "MISSION FAILED";

    [Header("Stats")]
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text shotsFiredText;
    [SerializeField] private TMP_Text accuracyText;
    [SerializeField] private TMP_Text damageDealtText;
    [SerializeField] private TMP_Text timeText;

    [Header("Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;

    [Header("Scenes")]
    [Tooltip("Scene loaded by the menu button. Must be in Build Settings — if " +
             "it isn't, the button logs an error rather than silently doing nothing.")]
    [SerializeField] private string menuSceneName = "MainMenu";

    [Header("References")]
    [Tooltip("Left empty, falls back to GameManager.Instance.")]
    [SerializeField] private GameManager gameManager;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);

        if (retryButton != null) retryButton.onClick.AddListener(Retry);
        if (menuButton  != null) menuButton.onClick.AddListener(ReturnToMenu);
    }

    private void Start()
    {
        if (gameManager == null) gameManager = GameManager.Instance;

        if (gameManager == null)
        {
            Debug.LogError($"{name}: no GameManager — the results screen will never show.");
            return;
        }

        gameManager.OnGameOver += Show;
    }

    private void OnDestroy()
    {
        if (gameManager != null) gameManager.OnGameOver -= Show;

        if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
        if (menuButton  != null) menuButton.onClick.RemoveListener(ReturnToMenu);

        // Defensive: if this object is destroyed while the game is frozen —
        // a scene load from somewhere else — the next scene must not inherit
        // a zero time scale.
        Time.timeScale = 1f;
    }

    #endregion

    #region Show

    private void Show()
    {
        RunStats stats = RunStats.Instance;
        if (stats != null) stats.StopTimer();

        Populate(stats);

        if (panel != null) panel.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Last, so the frame that reveals the panel has already been drawn.
        Time.timeScale = 0f;
    }

    private void Populate(RunStats stats)
    {
        if (titleText != null) titleText.text = titleMessage;

        if (stats == null)
        {
            Debug.LogWarning($"{name}: no RunStats in the scene — stats will be blank.");
            return;
        }

        SetText(killsText,        stats.EnemiesKilled.ToString());
        SetText(shotsFiredText,   stats.ShotsFired.ToString());
        SetText(accuracyText,     stats.AccuracyText);
        SetText(damageDealtText,  stats.DamageDealt.ToString());
        SetText(timeText,         stats.DurationText);

        // soldiersLost is deliberately not shown: on a failure screen it is
        // always startingLives. It belongs on a mission-complete screen.
    }

    private static void SetText(TMP_Text field, string value)
    {
        if (field != null) field.text = value;
    }

    #endregion

    #region Buttons

    /// <summary>
    /// Reloads the current scene. A full reload rather than a soft reset,
    /// deliberately: every pool, manager and enemy starts from nothing, so a
    /// retry cannot inherit state the way a recycled enemy could.
    /// </summary>
    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;

        if (Application.CanStreamedLevelBeLoaded(menuSceneName))
        {
            SceneManager.LoadScene(menuSceneName);
            return;
        }

        Debug.LogError($"{name}: scene '{menuSceneName}' is not in Build Settings " +
                       "(File > Build Profiles > Scene List). Staying on the results screen.");
    }

    #endregion
}