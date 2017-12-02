using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// This class takes care of window styling (removing borders), window transparency
/// and click event passthrough. For more info, see:
/// https://forum.unity.com/threads/solved-windows-transparent-window-with-opaque-contents-lwa_colorkey.323057/
/// </summary>
public class TranspHandler : MonoBehaviour
{
	/// <summary>
	/// The main camera.
	/// </summary>
	public Camera mainCamera;

	// ----------------------------- Various WinAPI functions ----------------------------------
	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT lpPoint);

	[DllImport("user32.dll")]
	private static extern IntPtr GetActiveWindow();

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("User32.dll")]
	private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll")]
	private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes")]
	private static extern int SetLayeredWindowAttributes(IntPtr hwnd, int crKey, byte bAlpha, int dwFlags);

	[DllImport("user32.dll", EntryPoint = "SetWindowPos")]
	private static extern int SetWindowPos(IntPtr hwnd, int hwndInsertAfter, int x, int y, int cx, int cy, int uFlags);

	[DllImport("Dwmapi.dll")]
	private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);
	// -----------------------------------------------------------------------------------------

	// Window handle.
	private IntPtr hwnd;

	// MARGINS struct for DwmExtendFrameIntoClientArea
	private MARGINS margins;
	private struct MARGINS
	{
		public int cxLeftWidth;
		public int cxRightWidth;
		public int cyTopHeight;
		public int cyBottomHeight;
	}

	// Some WinAPI definitions.
	private const uint WS_POPUP          =  0x80000000;
	private const uint WS_VISIBLE        =  0x10000000;
	private const uint WS_EX_LAYERED     =  0x00080000;
	private const uint WS_EX_TRANSPARENT =  0x00000020;
	private const int  HWND_TOPMOST      = -0x00000001;
	private const int  HWND_TOP          =  0x00000000;
	private const int  GWL_STYLE         = -0x00000010;
	private const int  GWL_EXSTYLE       = -0x00000014;
	private const int  SW_HIDE           =  0x00000000;
	private const int  SW_SHOW           =  0x00000005;
	private const int  LWA_ALPHA		 =  0x00000002;
	private const int  WS_EX_APPWINDOW   =  0x00040000;
	private const int  WS_EX_TOOLWINDOW  =  0x00000080;
	private const int  SWP_FRAMECHANGED  =  0x00000020;
	private const int  SWP_SHOWWINDOW    =  0x00000040;

	// Window width and height.
	private int fWidth;
	private int fHeight;

	// Indicates whether or not mouse pointer is considered "inside" the window.
	// True if a raycast from the main camera through the mouse pointer hits anything.
	// Otherwise the click is passed through to whatever is behing the window.
	private bool inside = true;

	// Window position (top left window corner)
	private Vector2 pos;

	// Window size (width/height) in pixel.
	private Vector2 winSize;

	// True if mouse drag of model has been started.
	private bool startDrag = false;

	// Window size multiplier.
	private float multiplier = 1.0f;

	// How much does one mouse scroll tick affect the window size?
	private float resizeStep = 0.013f;

	// Upper and lower limit for window size.
	// Given in multiples of window size at game start.
	private float multLower = 0.3f;
	private float multUpper = 1.2f;

	// Did the last raycast hit anything?
	private bool raycastHit = false;

	// Sets window to pass through clicks.
	private void SetInactive() {
		#if !UNITY_EDITOR
		SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
		SetWindowLong (hwnd, GWL_EXSTYLE, WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_TRANSPARENT); 
		SetLayeredWindowAttributes (hwnd, 0, 255, LWA_ALPHA);
		SetWindowPos(hwnd, HWND_TOPMOST, (int) pos.x, (int) pos.y, (int) (fWidth * multiplier), (int) (fHeight * multiplier), SWP_FRAMECHANGED | SWP_SHOWWINDOW);
		#endif
	}

	// Sets window to consume clicks.
	private void SetActive() {
		#if !UNITY_EDITOR
		SetWindowLong (hwnd, GWL_EXSTYLE, WS_EX_TOOLWINDOW | ~((WS_EX_LAYERED) | (WS_EX_TRANSPARENT)));
		SetWindowPos(hwnd, HWND_TOPMOST,  (int) pos.x, (int) pos.y, (int) (fWidth * multiplier), (int) (fHeight * multiplier), SWP_FRAMECHANGED | SWP_SHOWWINDOW);
		#endif
	}

	void Start()
	{
		// Make sure the window isn't in fullscreen.
		if (Screen.fullScreen) {
			Screen.fullScreen = false;
		}

		// Offset window from corner a little.
		pos = new Vector2(128, 128);

		fWidth = Screen.width;
		fHeight = Screen.height;

		// Set window style, transparency, position and keep it on top.
		#if !UNITY_EDITOR
		margins = new MARGINS() { cxLeftWidth = -1 };
		hwnd = GetActiveWindow();
		SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
		SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_TOOLWINDOW);
		SetWindowPos(hwnd, HWND_TOPMOST, (int) pos.x, (int) pos.y, fWidth, fHeight, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
		DwmExtendFrameIntoClientArea(hwnd, ref margins);
		Application.runInBackground = true;
		#endif
	}

	void Update ()
	{

		// Shoot a raycast through the mouse position and see if it hits anything (2D colliders).
		RaycastHit2D hit; 
		Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition); 
		hit = Physics2D.Raycast(ray.origin, ray.direction);

		// Check if it hit.
		if (hit != null && hit.collider != null) {
			Debug.Log("2D Ray hit: " + hit.collider.gameObject.name);
			raycastHit = true;
		} else {
			raycastHit = false;
		}

		// If we hit nothing, shoot another raycast for 3D colliders.
		if (!raycastHit) {
			RaycastHit hit2; 
			raycastHit = Physics.Raycast (ray, out hit2, 100.0f);
		}
			
		// Set window to consume or pass through clicks accordingly.
		if (inside && !raycastHit) {
			SetInactive();
		} else if (!inside && raycastHit) {
			SetActive();
		}

		// Indicate whether or not the mouse pointer is over potentially clickable content.
		inside = raycastHit;

		// Check if user started/ended a CTRL-drag
		if (Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftControl)) {
			if (raycastHit) {
				startDrag = true;
			}
		} else if (!Input.GetMouseButton(0) || !Input.GetKey(KeyCode.LeftControl)) {
			startDrag = false;
		}

		// If drag is currently going on, move window to mouse positoon
		if (startDrag) {
			MoveToCursor();
		}

		// If user is scrolling, resize window.
		if (raycastHit && Input.GetKey(KeyCode.LeftControl)) {
			if (multiplier < multUpper && Input.GetAxis("Mouse ScrollWheel") > 0f ) 
			{
				float oldMult = multiplier;
				multiplier += resizeStep;
				MoveToMiddle(oldMult, multiplier);
			} else if (multiplier > multLower && Input.GetAxis("Mouse ScrollWheel") < 0f ) 
			{
				float oldMult = multiplier;
				multiplier -= resizeStep;
				MoveToMiddle(oldMult, multiplier);
			}
		}

		// Clamp size multiplier between min/max.
		multiplier = Mathf.Clamp(multiplier, multLower, multUpper);
	}

	// Set new window center position to mouse pointer.
	private void MoveToCursor() {
		pos = GetCursorPosition();
		pos.x -= (fWidth  * multiplier / 2);
		pos.y -= (fHeight * multiplier / 2);

		// Update window state.
		SetActive();
	}

	// Move window so the middle is still at the same position after resizing.
	private void MoveToMiddle(float oldM, float newM) {
		float xDelta = oldM * fWidth - newM * fWidth;
		float yDelta = oldM * fHeight - newM * fHeight;
	
		pos.x += xDelta / 2.0f;
		pos.y += yDelta / 2.0f;

		// Update window state.
		SetActive();
	}

	/// <summary>
	/// Returns whether or not cursor is over raycastable window content.
	/// </summary>
	/// <returns><c>true</c> if that's the case <c>false</c> otherwise.</returns>
	public bool IsInside() {
		return inside;
	}

	/// <summary>
	/// Gets the cursor position on the screen (not the window) in pixels. Also works with multiple screens.
	/// </summary>
	/// <returns>The cursor position on the screen(s).</returns>
	private static Vector2 GetCursorPosition()
	{
		POINT lpPoint;
		GetCursorPos(out lpPoint);
		return new Vector2(lpPoint.X, lpPoint.Y);
	}

	/// <summary>
	/// Struct representing a point.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int X;
		public int Y;
	}
}
