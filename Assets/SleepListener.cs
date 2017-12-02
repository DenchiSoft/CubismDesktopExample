using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SleepListener : MonoBehaviour {
	public void sleepingActive() {
		GameObject.Find("hand").GetComponent<Hand>().sleepingActive();
	}
}
