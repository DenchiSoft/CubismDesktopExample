using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using UnityEngine.UI;

/// <summary>
/// Moves/Rotates/Animates hand.
/// Also does some character animation control stuff.
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

	private CubismParameter nnaa;
	private CubismParameter angry;
	private CubismParameter eyeSpin;
	private CubismParameter moveX;

	// Audio stuff.
	public AudioClip squishClip;

	// The hand image. Needed for fading in/out.
	private RawImage img;

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

	// Click counter. Is reset every 2 seconds.
	private int clickCounter = 0;

	// Is the character annoyed?  Happens when you click a lot.
	private bool annoyed = false;
	private bool prevAnnoyed = false;

	// Tray icon.
	public System.Windows.Forms.Tray tray;

	// Animation weights.
	private float angryAmount = 0;
	private float spinEyeAmount = 0;
	private float nnaaaAmount = 0;

	// Use this for initialization
	void Start () {
		// References to stuff.
		handAnimator = GetComponent<Animator>();
		img = GetComponent<RawImage>();
		transpHandler = GameObject.FindObjectOfType<TranspHandler>();
		pt = transform.parent.transform;
		startTime = Time.time;
	
		// Find model.
		model = GameObject.Find("Nanachi_L2D").GetComponent<CubismModel>();
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
			} else if (p.Id == "ParamNnaaa") {
				nnaa = p;
			} else if (p.Id == "ParamAngry") {
				angry = p;
			} else if (p.Id == "ParamSpinEyes") {
				eyeSpin = p;
			} else if (p.Id == "ParamMoveX") {
				moveX = p;
			} else {
				//...
			}
		}

		// Start click counter.
		InvokeRepeating("CheckClickCount", 0, 2);
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
		if (tray.handVisible() && img.color.a < maximum) {
			float t = (Time.time - startTime) / duration;
			img.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(minimum, maximum, t));
		} else {
			if (tray.handVisible()) {
				img.color = new Color(1f, 1f, 1f, maximum);
			} else {
				img.color = new Color(1f, 1f, 1f, 0f);
			}
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

				// Stat wobble.
				startWobble(mousePosition);

				// Increase click counter.
				clickCounter++;
			}
		} else if (Input.GetKey(KeyCode.LeftControl)) {
			// While CTRL is pressed, remove hand.
			img.color = new Color(1f, 1f, 1f, 0f);
		}
	}

	// Check if character was clicked enough to annoy them every 2 seconds.
	private void CheckClickCount() {
		if (!annoyed && clickCounter > 8) {
			annoyed = true;
		} else if (annoyed && clickCounter <= 8) {
			annoyed = false;
		}

		clickCounter = 0;
	}

	/// <summary>
	/// Return annoyed state of character.
	/// </summary>
	public bool getAnnoyed() {
		return annoyed;
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

		// Time "Nnaaa" and "angry sign" animation.
		if (annoyed) {
			if (angryAmount <= 1) {
				angryAmount += 0.073f;
			}

			if (!prevAnnoyed) {
				nnaaaAmount = 0f;
			}

			nnaaaAmount += 0.0073f;
		} else {
			if (angryAmount >= 0) {
				angryAmount -= 0.025f;
			}

			nnaaaAmount += 0.007f;
		}

		if (angryAmount <= 0) {
			nnaaaAmount = 0;
		}

		prevAnnoyed = annoyed;

		// Check if dizzy animation should be played.
		if (transpHandler.getDizzy()) {
			if (spinEyeAmount <= 1) {
				spinEyeAmount += 0.07f;
			}
		} else {
			if (spinEyeAmount >= 0) {
				spinEyeAmount -= 0.025f;
			}
		}

		// Set new parameter values.
		eyeSpin.BlendToValue(CubismParameterBlendMode.Override, spinEyeAmount);
		angry.BlendToValue(CubismParameterBlendMode.Override, angryAmount);
		nnaa.BlendToValue(CubismParameterBlendMode.Override, nnaaaAmount);

		if (wobbleMagnitude > 0.01f) {
			float tempWobble = wobbleMagnitude * wobbleDir.x * (-1.71f);
			tempWobble = Mathf.Clamp(tempWobble, -1, 1);
			moveX.BlendToValue(CubismParameterBlendMode.Override, tempWobble);
		} else {
			float xSpeed = transpHandler.getXSpeed() / 27f;
			xSpeed = Mathf.Clamp(xSpeed, -1, 1);
			moveX.BlendToValue(CubismParameterBlendMode.Override, -xSpeed);
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
}
