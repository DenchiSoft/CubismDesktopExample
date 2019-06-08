using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Text;
using System.Linq;

/// <summary>
/// This class is used to get information about all windows and keep them updated.
/// </summary>
public class WindowPosManager : MonoBehaviour {
	/// <summary>
	/// The tray icon. Needed to check if application is shutting down.
	/// </summary>
	public System.Windows.Forms.Tray tray;

	/// <summary>
	/// List of all windows.
	/// </summary>
	/// <value>The windows.</value>
	public List<WindowInfo> windows { get; private set; }

	// ----------------------------- Various WinAPI functions ----------------------------------
	protected delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam); 
	[DllImport("user32.dll", CharSet = CharSet.Unicode)] 
	protected static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount); 
	[DllImport("user32.dll", CharSet = CharSet.Unicode)] 
	protected static extern int GetWindowTextLength(IntPtr hWnd); 
	[DllImport("user32.dll")] 
	protected static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam); 
	[DllImport("user32.dll")] 
	protected static extern bool IsWindowVisible(IntPtr hWnd); 
	[DllImport("user32.dll")]  
	[return: MarshalAs(UnmanagedType.Bool)]  
	static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);  

	[StructLayout(LayoutKind.Sequential)]  
	public struct RECT  
	{
		public int Left;        // x position of upper-left corner  
		public int Top;         // y position of upper-left corner  
		public int Right;       // x position of lower-right corner  
		public int Bottom;      // y position of lower-right corner  
	}
	// -----------------------------------------------------------------------------------------

	// Use this for initialization
	void Start () {
		windows = new List<WindowInfo>();
	}
	
	// Update is called once per frame
	void Update () {
		// Don't make any calls when application is shutting down.
		// Otherwise it will crash.
		if (tray.quitClicked())
			return;

		// Update window list and remove inactive (closed).
		windows.ForEach(wnd => wnd.windowStillActive = false);
		EnumWindows(new EnumWindowsProc(UpdateActiveWindows), IntPtr.Zero);
		windows.RemoveAll(wnd => !wnd.windowStillActive);
	}

	// Called for each window. Updates the parameters or adds it to the list if it's new.
	protected bool UpdateActiveWindows(IntPtr hWnd, IntPtr lParam) 
	{ 
		// Check if window is active/visible.
		int size = GetWindowTextLength(hWnd); 
		if (size++ > 0 && IsWindowVisible(hWnd)) 
		{ 
			bool isNewWindow = !windows.Any(x => x.m_hWnd == hWnd);

			if (isNewWindow) {
				WindowInfo w = new WindowInfo(hWnd);
				windows.Add(w);
				GetWindowInfo(w, size, true);
			} else {
				WindowInfo w = windows.Single(x => x.m_hWnd == hWnd);
				GetWindowInfo(w, 0, false);
			}
		}

		return true;
	} 

	// Update window information of a given window.
	// If param "all" is set to true, all information will be updated.
	// Otherwise, only the position is updated.
	private void GetWindowInfo(WindowInfo w, int size, bool all) {
		RECT r;
		GetWindowRect(w.m_hWnd, out r);

		if (all) {
			StringBuilder sb = new StringBuilder(size); 
			GetWindowText(w.m_hWnd, sb, size); 
			w.m_Title = sb.ToString();
		}

		w.m_TopLeft = new Vector2(r.Left, r.Top);
		w.m_TopRight = new Vector2(r.Right, r.Top);
		w.m_BottomLeft = new Vector2(r.Left, r.Bottom);
		w.m_TopRight = new Vector2(r.Right, r.Bottom);

		w.windowStillActive = true;
	}
}
