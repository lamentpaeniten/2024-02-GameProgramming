using System;



// ====================================================================================================
// Animation Type
// ====================================================================================================

[Serializable]
public enum AnimationType {
	Idle,
	Moving,
	Attacking,
	Dead,
}



// ====================================================================================================
// Creature Type
// ====================================================================================================

[Serializable]
public enum CreatureType {
	None,
	Player,
}



// ====================================================================================================
// Particle Type
// ====================================================================================================

[Serializable]
public enum ParticleType {
	None,
}
