using UnityEngine;

namespace System.Windows.Forms {
    /// <summary>
    /// This class adds an icon to the Windows tray when start is called.
    /// Needs Windows.Forms and System.Drawing DLLs
    /// </summary>
    public class Tray : MonoBehaviour {

		/// <summary>
		/// The mouseover string of the tray icon.
		/// </summary>
		public string iconTitle;

		/// <summary>
		/// Tray icon. May contain transparency.
		/// </summary>
		public Texture2D iconTexture;

		// Gets set when the user quits the game using the tray icon.
		// If this bool is set, the game will be quit in the next update loop.
		// This hack is needed because the game crashes when you try to quit it in the tray icon click callback for some reason.
		private bool quitOnNextFrame = false;

		// Converted icon.
		private NotifyIcon  trayIcon;

		// Context menu.
		private ContextMenu trayMenu;

		// Was hand turned on/off using tray menu?
		private bool showHand;

		// Use this for initialization
		void Start () {
			// Create a simple tray menu.
			trayMenu = new ContextMenu();
			trayMenu.MenuItems.Add("Exit", OnExit);
			trayMenu.MenuItems.Add("Show/Hide Hand", OnHand);

			// Create a tray icon.
			trayIcon = new NotifyIcon();
			trayIcon.Text = iconTitle;
			trayIcon.Icon = ConvertTextureToIcon(iconTexture);

			// Add menu to tray icon and show it.
			trayIcon.ContextMenu = trayMenu;
			trayIcon.Visible     = true;

			showHand = true;
		}
			
		// This callback is called when the user selects "Exit" on the tray icon.
		private void OnExit(object sender, EventArgs e)
		{
			// Remove icon.
			trayIcon.Dispose();
			quitOnNextFrame = true;
		}

		// This callback is called when the user selects "Show/Hide Hand" on the tray icon.
		private void OnHand(object sender, EventArgs e)
		{
			showHand = !showHand;
		}

		// Function to convert Unity Texture2D to Icon by Orion_78
		// See https://answers.unity.com/questions/1314410/make-application-close-to-the-tray.html
		private System.Drawing.Icon ConvertTextureToIcon(Texture2D iconTexture)
		{
			System.IO.MemoryStream memStream = new System.IO.MemoryStream(iconTexture.EncodeToPNG());
			memStream.Seek(0, System.IO.SeekOrigin.Begin);
			System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(memStream);
			return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
		}
			
		/// <summary>
		/// Returns true when user has quit the game using the tray icon.
		/// </summary>
		public bool quitClicked() {
			return quitOnNextFrame;
		}

		/// <summary>
		/// Returns true when hand should be visible.
		/// </summary>
		public bool handVisible() {
			return showHand;
		}
	}
}