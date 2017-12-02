using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using UnityEngine.UI;

/// <summary>
/// Moves/Rotates/Animates hand.
/// </summary>
public class Hand : MonoBehaviour {
	// Current mouse positon in window
	private Vector3 mousePosition;

	// Angle between mouse pos and this vector is calculated to figure out
	// how far the hand has to be rotated.
	private Vector2 baseAngle = new Vector2(0, -1);

	// Base rotation of the hand at position "baseAngle"
	private float baseRotation = -90;

	// Animators for hand and character.
	private Animator handAnimator;
	private Animator charAnimator;

	// Transformer of parent of hand game object.
	// This is the one that's moved/rotated.
	private Transform pt;

	// The Live2D Cubism model.
	private CubismModel model;

	// How fast does the hand follow the mouse cursor?
	public const float moveSpeed = 0.1f;

	// Min/max values and duration for fading in the hand when the application starts.
	private const float minimum = 0.0f;
	private const float maximum = 0.84f;
	private const float duration = 5.0f;
	private float startTime;

	// How fast do the eyes follow the mouse pointer?
	private const float eyeXSpeed = 0.1f;
	private const float eyeYSpeed = 0.1f;

	// Cubism parameters for eye positons.
	private CubismParameter eyeX;
	private CubismParameter eyeY;

	// Cubism parameters for eye open state.
	private CubismParameter eyeOpenL;
	private CubismParameter eyeOpenR;

	// Cubism parameter for X/Y face squish state.
	private CubismParameter paramX;
	private CubismParameter paramY;

	// Audio stuff.
	private AudioSource clickAudio;
	public AudioClip squishClip;

	// The hand image. Needed for fading in/out.
	private RawImage img;

	// Is the character currently sleeping? Is set by animation.
	private bool sleeping = false;

	// Some variables for controlling wobble/squish state, amount and direction.
	private float wobbleCounter = 0;
	private float wobbleFinal = 0;
	private float wobbleMagnitude = 0;
	private Vector2 wobbleDir = new Vector2(0, 0);
	private float fadeBack = 0;

	// +/- parameter extents of the wobble.
	private const float wobbleAmount = 29f;

	// Window transparency handler.
	private TranspHandler transpHandler;

	// Use this for initialization
	void Start () {
		// References to stuff.
		handAnimator = GetComponent<Animator>();
		clickAudio = GetComponent<AudioSource>();
		img = GetComponent<RawImage>();
		transpHandler = GameObject.FindObjectOfType<TranspHandler>();
		pt = transform.parent.transform;
		startTime = Time.time;
	
		// Find model.
		model = GameObject.Find("Hideri_L2D").GetComponent<CubismModel>();
		charAnimator = model.gameObject.GetComponent<Animator>();

		// Get parameters we want to directly controll.
		foreach (CubismParameter p in model.Parameters) {
			if (p.Id == "ParamEyeBallX") {
				eyeX = p;
			} else if (p.Id == "ParamEyeBallY") {
				eyeY = p;
			} else if (p.Id == "ParamEyeLOpen") {
				eyeOpenL = p;
			} else if (p.Id == "ParamEyeROpen") {
				eyeOpenR = p;
			} else if (p.Id == "ParamAngleX") {
				paramX = p;
			} else if (p.Id == "ParamAngleY") {
				paramY = p;
			} else {
				//...
			}
		}
	}
	
	// Update is called once per frame
	void Update () {
		// Move hand towards mouse pointer.
		mousePosition = Input.mousePosition;
		mousePosition = Camera.main.ScreenToWorldPoint(mousePosition);
		mousePosition.z = -1;
		pt.position = Vector3.Lerp(pt.position, mousePosition, moveSpeed);

		// Calculate angle to window middle (that's where the character should be).
		float angle = Vector2.Angle(baseAngle, mousePosition);

		// If we're past the middle of the window, we need to invert the angle.
		if (mousePosition.x > baseAngle.x) {
			angle = angle * -1;
		}

		// Rotate hand accordingly.
		pt.localEulerAngles = new Vector3(0, 0, baseRotation - angle);
			
		// Fade in hand sprite.
		if (img.color.a < maximum) {
			float t = (Time.time - startTime) / duration;
			img.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(minimum, maximum, t));
		}

		// Check for mouse down event without left CTRL pressed.
		if (Input.GetMouseButtonDown(0) && !Input.GetKey(KeyCode.LeftControl)) {
			// Check if the mouse pointer is over the clickable (raycast hit) area.
			if (transpHandler.IsInside()) {
				// Play click animation and sound.
				AudioSource squishAudio = gameObject.AddComponent<AudioSource>();
				squishAudio.playOnAwake = false;
				squishAudio.clip = squishClip;
				squishAudio.volume = 0.52f;
				squishAudio.loop = false;
				squishAudio.Play();
				StartCoroutine(RemoveAS(squishAudio));

				squishAudio.Play();
				handAnimator.SetTrigger("clicking");
				charAnimator.SetTrigger("poke");

				// Wake up character.
				sleeping = false;

				// Stat wobble.
				startWobble(mousePosition);
			}
		} else if (Input.GetKey(KeyCode.LeftControl)) {
			// While CTRL is pressed, remove hand.
			img.color = new Color(1f, 1f, 1f, 0f);
		}
	}

	// Removes squish audio source after it's no longer needed.
	// Needed because we spawn a new audio source for every click.
	private IEnumerator RemoveAS(AudioSource audioS){
		yield return new WaitForSeconds(2);
		Destroy(audioS);
	}
		
	// Called by unity each frame after Update/Animations
	void LateUpdate() {
		// Move eyes towards mouse pointer.
		eyeX.BlendToValue(CubismParameterBlendMode.Override, pt.position.x * eyeXSpeed);
		eyeY.BlendToValue(CubismParameterBlendMode.Override, pt.position.y * eyeYSpeed);

		// Check if wobble is still going on.
		if (wobbleMagnitude > 0.01f) {
			// If so, slowly open closed eyes and set X/Y wobble position.
			paramX.BlendToValue(CubismParameterBlendMode.Override, -wobbleAmount * wobbleDir.x * (wobbleFinal + 1) / 2.0f);
			paramY.BlendToValue(CubismParameterBlendMode.Override, -wobbleAmount * wobbleDir.y * (wobbleFinal + 1) / 2.0f);

			eyeOpenL.BlendToValue(CubismParameterBlendMode.Override, 1 - wobbleMagnitude - 0.5f);
			eyeOpenR.BlendToValue(CubismParameterBlendMode.Override, 1 - wobbleMagnitude - 0.5f);
		} else {
			if (fadeBack > 0.01) {
				// Otherwise slowly fade back to the values given by the animation.
				float x = Mathf.Lerp(paramX.Value, -wobbleAmount * wobbleDir.x * (wobbleFinal + 1) / 2.0f, fadeBack);
				float y = Mathf.Lerp(paramY.Value, -wobbleAmount * wobbleDir.y * (wobbleFinal + 1) / 2.0f, fadeBack);

				float eyeL = Mathf.Lerp(eyeOpenL.Value, 0.5f, fadeBack);
				float eyeR = Mathf.Lerp(eyeOpenR.Value, 0.5f, fadeBack);

				paramX.BlendToValue(CubismParameterBlendMode.Override, x);
				paramY.BlendToValue(CubismParameterBlendMode.Override, y);

				eyeOpenL.BlendToValue(CubismParameterBlendMode.Override, eyeL);
				eyeOpenR.BlendToValue(CubismParameterBlendMode.Override, eyeR);

				fadeBack -= 0.05f;
			} else {
				fadeBack = 0;
			}
		}
	}

	// Character has been clicked, start wobble.
	private void startWobble(Vector2 mousePos) {
		// Wobble starts in the direction opposite of the mouse pointer.
		wobbleDir = mousePos.normalized;

		// Reset wobble state.
		wobbleCounter = 0;
		wobbleMagnitude = 1;
		wobbleFinal = 0;
		fadeBack = 1;
	}

	// Called by Unity at a fixed framerate.
	void FixedUpdate()
	{
		if (wobbleMagnitude >= 0) {
			// Wobble in a cosine pattern but reduce magnitude each fixedupdate step
			// so it kind of fades out.
			wobbleFinal = Mathf.Cos(wobbleCounter) * wobbleMagnitude;
			wobbleCounter+= 0.328f;
			wobbleMagnitude -= 0.025f;
		}
	}

	// Called about 1 second into the sleeping animation by the animation itself (animation event).
	// The sleeping animation autoplays after the awake animation unless the character was clicked.
	public void sleepingActive() {
		sleeping = true;
	}
}
