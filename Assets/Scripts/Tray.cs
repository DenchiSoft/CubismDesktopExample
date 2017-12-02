﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Windows.Forms;
using System.Drawing;

/// <summary>
/// This class adds an icon to the Windows tray when start is called.
/// Needs Windows.Forms and System.Drawing DLLs
/// </summary>
namespace System.Windows.Forms {
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

		// Use this for initialization
		void Start () {
			// Create a simple tray menu.
			trayMenu = new ContextMenu();
			trayMenu.MenuItems.Add("Exit", OnExit);

			// Create a tray icon.
			trayIcon = new NotifyIcon();
			trayIcon.Text = iconTitle;
			trayIcon.Icon = ConvertTextureToIcon(iconTexture);

			// Add menu to tray icon and show it.
			trayIcon.ContextMenu = trayMenu;
			trayIcon.Visible     = true;
		}
		
		// Update is called once per frame
		void Update () {

			// If user has quit the game using the tray icon, quit it in this frame.
			if (quitOnNextFrame) {
				quitOnNextFrame = false;

				#if UNITY_EDITOR
				UnityEditor.EditorApplication.isPlaying = false;
				#else
				UnityEngine.Application.Quit();
				#endif
			}
		}
			
		// This callback is called when the user selects "Exit" on the tray icon.
		private void OnExit(object sender, EventArgs e)
		{
			// Remove icon.
			trayIcon.Dispose();
			quitOnNextFrame = true;
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
	}
}