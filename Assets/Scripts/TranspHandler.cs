using System;
using System.Runtime.InteropServices;
using UnityEngine;

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
	private const int  LWA_ALPHA         =  0x00000002;
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
	private Vector2 postLastFrame;

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

	// This has the position of all windows.
	public WindowPosManager windowPosManager;

	// Distance of ground indicator rect points to left bottom of window (in pixel).
	private Vector2 groundIndicatorTopLeft;
	private Vector2 groundIndicatorTopRight;

	// Minimal distance to lock onto a window (in pixel).
	private const int windowLockMinDist = 17;

	// Window the character is currently locked to. NULL if none.
	private WindowInfo window;

	// Position of locked window in last frame. Top right is not used.
	private Vector2 windowTopLeftLastFrame;
	// private Vector2 windowTopRightLastFrame;

	// Ground renderer. Only needed to get sprite extents.
	public SpriteRenderer groundIndicatorRenderer;

	// Tray icon.
	public System.Windows.Forms.Tray tray;

	// Distance the character has been moved. Gets added up each frame and reset every 2 seconds.
	private float distanceTraveled = 0;

	// This indicated fast movement, meaning the character would be dizzy.
	private bool dizzy = false;

	// Movement speed of character, which triggers dizzy state.
	private float xSpeed = 0;

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

		// Check traveled distance every 2 seconds.
		InvokeRepeating("CheckDistance", 0, 2);
	}

	void Update ()
	{
		if (tray.quitClicked()) {
			CancelInvoke("CheckDistance");
			return;
		}
		
		// Calculate ground indicator top line position.
		Vector3 min = mainCamera.WorldToScreenPoint(groundIndicatorRenderer.bounds.min);
		Vector3 max = mainCamera.WorldToScreenPoint(groundIndicatorRenderer.bounds.max);

		groundIndicatorTopLeft = new Vector2(min.x, max.y);
		groundIndicatorTopRight = new Vector2(max.x, max.y);

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

		// If the character is locked to a window, check if it has moved.
		if (!startDrag && window != null) {
			MoveWithWindow();
		}

		// Clamp size multiplier between min/max.
		multiplier = Mathf.Clamp(multiplier, multLower, multUpper);
	}
		
	// Sets window to pass through clicks.
	private void SetInactive() {
		distanceTraveled += Vector2.Distance(postLastFrame, pos);
		xSpeed = postLastFrame.x - pos.x;
		postLastFrame = pos;
		#if !UNITY_EDITOR
		SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
		SetWindowLong (hwnd, GWL_EXSTYLE, WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_TRANSPARENT); 
		SetLayeredWindowAttributes (hwnd, 0, 255, LWA_ALPHA);
		SetWindowPos(hwnd, HWND_TOPMOST, (int) pos.x, (int) pos.y, (int) (fWidth * multiplier), (int) (fHeight * multiplier), SWP_FRAMECHANGED | SWP_SHOWWINDOW);
		#endif
	}

	// Sets window to consume clicks.
	private void SetActive() {
		distanceTraveled += Vector2.Distance(postLastFrame, pos);
		xSpeed = postLastFrame.x - pos.x;
		postLastFrame = pos;
		#if !UNITY_EDITOR
		SetWindowLong (hwnd, GWL_EXSTYLE, WS_EX_TOOLWINDOW | ~((WS_EX_LAYERED) | (WS_EX_TRANSPARENT)));
		SetWindowPos(hwnd, HWND_TOPMOST,  (int) pos.x, (int) pos.y, (int) (fWidth * multiplier), (int) (fHeight * multiplier), SWP_FRAMECHANGED | SWP_SHOWWINDOW);
		#endif
	}

	// Checks traveled distance every 2 seconds and sets character
	// to dizzy state if it's over a certain limit.
	private void CheckDistance() {
		if (distanceTraveled > 2850) {
			dizzy = true;
		} else {
			dizzy = false;
		}

		distanceTraveled = 0;
	}

	/// <summary>
	/// Returns movement speed in X direction.
	/// </summary>
	/// <returns>The X direction speed.</returns>
	public float getXSpeed() {
		return xSpeed;
	}

	// Set new window center position to mouse pointer.
	private void MoveToCursor() {
		pos = GetCursorPosition();

		// Move to center.
		pos.x -= fWidth  * multiplier / 2;
		pos.y -= fHeight * multiplier / 2;

		// Reser locked window.
		window = null;

		// Check if a window is close enough to dock.
		foreach (WindowInfo w in windowPosManager.windows) {
			int yDist = (int) (pos.y + fHeight * multiplier - groundIndicatorTopLeft.y - w.m_TopLeft.y);
			int lDist = (int) (pos.x + groundIndicatorTopLeft.x - w.m_TopLeft.x);
			int rDist = (int) (pos.x + groundIndicatorTopRight.x - w.m_TopRight.x);

			if (Mathf.Abs(yDist) < windowLockMinDist && lDist > 0 && rDist < 0) {
				pos.y -= yDist;
				window = w;
				windowTopLeftLastFrame = w.m_TopLeft;
				//windowTopRightLastFrame = w.m_TopRight;
			}
		}

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

	// Move the character along with the window if the character is locked to a window.
	private void MoveWithWindow() {
		Vector2 topLeftDistance = windowTopLeftLastFrame - window.m_TopLeft;
		if (topLeftDistance.sqrMagnitude > 0.0001f) {
			pos -= topLeftDistance;
			windowTopLeftLastFrame = window.m_TopLeft;

			SetInactive();
		}
	}

	/// <summary>
	/// Returns true if character should be dizzy.
	/// </summary>
	/// <returns><c>true</c>, if dizzy was gotten, <c>false</c> otherwise.</returns>
	public bool getDizzy() {
		return dizzy;
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
