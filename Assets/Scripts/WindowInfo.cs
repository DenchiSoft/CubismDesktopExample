using UnityEngine;
using System;

/// <summary>
/// This class represents one window with all relevant information.
/// </summary>
public class WindowInfo {
	public string m_Title { get; set; }
	public IntPtr m_hWnd  { get; set; }
	public Vector2 m_TopLeft { get; set; }
	public Vector2 m_TopRight { get; set; }
	public Vector2 m_BottomLeft { get; set; }
	public Vector2 m_BottomRight { get; set; }
	public bool windowStillActive { get; set; }

	public WindowInfo(IntPtr p) {
		m_hWnd = p;
	}
}
