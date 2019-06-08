using UnityEngine;

/// <summary>
/// Game quit handler.
/// </summary>
public class GameQuitHandler : MonoBehaviour {
	// The tray icon for polling if the game was quit.
	public System.Windows.Forms.Tray tray;

	private bool quitting = false;

	void Update () {
		// Check if quit was clicked in the tray, end game if so.
		if (!quitting && tray.quitClicked()) {
			quitting = true;

			// Quitting the game on the same frame for some reason crashes the application.
			Invoke("QuitGame", 1);
		}
	}

	// Wait a bit and quit game.
	private void QuitGame() {
		#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
		#else
		UnityEngine.Application.Quit();
		#endif
	}
}
