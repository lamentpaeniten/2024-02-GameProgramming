using UnityEngine;



public class Creature : MonoBehaviour {

	void Update() {
		if (transform.hasChanged) {
			transform.GetChild(0).position = CameraManager.GetPixelated(transform.position);
			transform.hasChanged = false;
		}
	}
	
}
