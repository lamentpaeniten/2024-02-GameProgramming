using UnityEngine;
using Unity.Collections;

using System;
using System.Runtime.InteropServices;

#if UNITY_EDITOR
	using UnityEditor;
	using static UnityEditor.EditorGUILayout;
#endif



[Serializable] struct CreatureData {
	public Vector3    position;
	public Quaternion rotation;
	public Vector3    scale;

	public Vector2 tiling;
	public Vector2 offset;
	public Vector3 color;
	public float   emission;
	public float   alpha;
}

[Serializable] struct ParticleData {
	public Vector3    position;
	public Quaternion rotation;
	public Vector3    scale;

	public Vector2 tiling;
	public Vector2 offset;
	public Vector3 color;
	public float   emission;
	public float   alpha;
}

[Serializable] struct ShadowOnlyData {
	public Vector3 position;
	public Vector4 rotation;
	public Vector3 scale;
}



// ====================================================================================================
// Draw Manager Editor
// ====================================================================================================

#if UNITY_EDITOR
	[CustomEditor(typeof(DrawManager)), CanEditMultipleObjects]
	public class DrawManagerEditor : ExtendedEditor {

		SerializedProperty m_SphereMesh;
		SerializedProperty m_ShadowOnlyMaterial;
		SerializedProperty m_QuadMesh;
		SerializedProperty m_CreatureMaterial;
		SerializedProperty m_ParticleMaterial;
		SerializedProperty m_CreatureAtlasMap;
		SerializedProperty m_ParticleAtlasMap;

		void OnEnable() {
			m_SphereMesh         = serializedObject.FindProperty("m_SphereMesh");
			m_ShadowOnlyMaterial = serializedObject.FindProperty("m_ShadowOnlyMaterial");
			m_QuadMesh           = serializedObject.FindProperty("m_QuadMesh");
			m_CreatureMaterial   = serializedObject.FindProperty("m_CreatureMaterial");
			m_ParticleMaterial   = serializedObject.FindProperty("m_ParticleMaterial");
			m_CreatureAtlasMap   = serializedObject.FindProperty("m_CreatureAtlasMap");
			m_ParticleAtlasMap   = serializedObject.FindProperty("m_ParticleAtlasMap");
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();
			Undo.RecordObject(target, "Change Draw Manager Properties");

			LabelField("Material", EditorStyles.boldLabel);
			PropertyField(m_QuadMesh);
			PropertyField(m_CreatureMaterial);
			PropertyField(m_ParticleMaterial);
			PropertyField(m_CreatureAtlasMap);
			PropertyField(m_ParticleAtlasMap);
			Space();

			LabelField("Shadow", EditorStyles.boldLabel);
			PropertyField(m_SphereMesh);
			PropertyField(m_ShadowOnlyMaterial);
			Space();

			serializedObject.ApplyModifiedProperties();
			if (GUI.changed) EditorUtility.SetDirty(target);
		}
	}
#endif



// ====================================================================================================
// Draw Manager
// ====================================================================================================

public class DrawManager : MonoSingleton<DrawManager> {

	// Fields

	[SerializeField] Mesh       m_QuadMesh;
	[SerializeField] Material   m_CreatureMaterial;
	[SerializeField] Material   m_ParticleMaterial;
	[SerializeField] AtlasMapSO m_CreatureAtlasMap;
	[SerializeField] AtlasMapSO m_ParticleAtlasMap;

	[SerializeField] Mesh     m_SphereMesh;
	[SerializeField] Material m_ShadowOnlyMaterial;



	// Methods

	float GetYaw(Quaternion quaternion) {
		float y = 0.0f + 2.0f * (quaternion.y * quaternion.w + quaternion.x * quaternion.z);
		float x = 1.0f - 2.0f * (quaternion.y * quaternion.y + quaternion.z * quaternion.z);
		return Mathf.Atan2(y, x) * Mathf.Rad2Deg;
	}

	void GetDirection(float relativeYaw, int numDirections, out int direction, out bool xFlip) {
		xFlip = false;
		int i = 0;
		int yaw = (int)Mathf.Repeat(relativeYaw / 360f * 256f, 256 - 1);
		switch (numDirections) {
			case  1: i = (yaw +  0) / 128; if (0 < i) { i =  1 - i; xFlip = true; } break;
			case  2: i = (yaw +  0) / 128;                                          break;
			case  3: i = (yaw + 32) /  64; if (2 < i) { i =  4 - i; xFlip = true; } break;
			case  4: i = (yaw + 32) /  64;                                          break;
			case  5:
			case  6:
			case  7: i = (yaw + 16) /  32; if (4 < i) { i =  8 - i; xFlip = true; } break;
			case  8: i = (yaw + 16) /  32;                                          break;
			case  9:
			case 10:
			case 11:
			case 12:
			case 13:
			case 14:
			case 15: i = (yaw +  8) /  16; if (8 < i) { i = 16 - i; xFlip = true; } break;
			case 16: i = (yaw +  8) /  16;                                          break;
		}
		direction = i;
	}

	int GetIndex(int count, float value, Func<int, int> func) {
		int m = 0;
		int l = 0;
		int r = count - 1;
		while (l <= r) {
			m = (l + r) / 2;
			if      (value < func(m - 1)) r = m - 1;
			else if (func(m + 0) < value) l = m + 1;
			else break;
		}
		return m;
	}



	HashMap<int, int>          creatureSizeMap = new HashMap<int, int>();
	HashMap<int, int>          particleSizeMap = new HashMap<int, int>();
	HashMap<int, CreatureData> creatureDataMap = new HashMap<int, CreatureData>();
	HashMap<int, ParticleData> particleDataMap = new HashMap<int, ParticleData>();

	int GetCreatureSize(
		CreatureType  creatureType  = (CreatureType )(-1),
		AnimationType animationType = (AnimationType)(-1),
		int           direction     = -1,
		int           index         = -1
	) => creatureSizeMap.TryGetValue(
		((((int)creatureType  + 1) & 0xff) << 24) |
		((((int)animationType + 1) & 0xff) << 16) |
		(((     direction     + 1) & 0xff) <<  8) |
		(((     index         + 1) & 0xff) <<  0),
		out int count) ? count : 0;
	
	CreatureData GetCreatureData(
		CreatureType  creatureType,
		AnimationType animationType,
		int           direction,
		int           index
	) => creatureDataMap.TryGetValue(
		((((int)creatureType  + 1) & 0xff) << 24) |
		((((int)animationType + 1) & 0xff) << 16) |
		(((     direction     + 1) & 0xff) <<  8) |
		(((     index         + 1) & 0xff) <<  0),
		out CreatureData data) ? data : new CreatureData();

	int GetParticleSize(
		ParticleType particleType = (ParticleType)(-1),
		int          index        = -1
	) => particleSizeMap.TryGetValue(
		((((int)particleType + 1) & 0xff) << 24) |
		(((     index        + 1) & 0xff) << 16),
		out int count) ? count : 0;
	
	ParticleData GetParticleData(
		ParticleType particleType,
		int          index
	) => particleDataMap.TryGetValue(
		((((int)particleType + 1) & 0xff) << 24) |
		(((     index        + 1) & 0xff) << 16),
		out ParticleData data) ? data : new ParticleData();



	void LoadCreatureMap() {
		float pixelPerUnit = UIManager.Instance.PixelPerUnit;
		creatureSizeMap.Clear();
		creatureDataMap.Clear();
		if (m_CreatureAtlasMap) foreach (var pair in m_CreatureAtlasMap.AtlasMap) {
			// CreatureType_AnimationType_Direction_Index_Duration
			string[] split = pair.Key.Split('_');
			if (split.Length != 5) continue;

			int[] value = new int[5];
			value[0] = (int)Enum.Parse(typeof(CreatureType ), split[0]);
			value[1] = (int)Enum.Parse(typeof(AnimationType), split[1]);
			value[2] = int.Parse(split[2]);
			value[3] = int.Parse(split[3]);
			value[4] = int.Parse(split[4]);

			int[] key = new int[5];
			key[0] = 0;
			key[1] = key[0] + (((value[0] + 1) & 0xff) << 24);
			key[2] = key[1] + (((value[1] + 1) & 0xff) << 16);
			key[3] = key[2] + (((value[2] + 1) & 0xff) <<  8);
			key[4] = key[3] + (((value[3] + 1) & 0xff) <<  0);
			
			for (int k = 4 - 1; -1 < k; k--) {
				if (!creatureSizeMap.ContainsKey(key[k])) creatureSizeMap.Add(key[k], 0);
				creatureSizeMap[key[k]]++;
				if (k == 0 || creatureSizeMap.ContainsKey(key[k - 1])) break;
			}
			if (!creatureSizeMap.ContainsKey(key[4])) creatureSizeMap[key[4]] = value[4];
			if (1 < creatureSizeMap[key[3]]) creatureSizeMap[key[4]] += creatureSizeMap[key[4] - 1];

			creatureDataMap.Add(key[4], new CreatureData() {
				position = new Vector3(0, 0, 0),
				rotation = new Quaternion(0, 0, 0, 1),
				scale    = new Vector3(pair.Value.size.x, pair.Value.size.y, 1) / pixelPerUnit,

				tiling   = new Vector2(pair.Value.tiling.x, pair.Value.tiling.y),
				offset   = new Vector2(pair.Value.offset.x, pair.Value.offset.y),
				color    = new Vector3(1, 1, 1),
				emission = 0,
				alpha    = 1,
			});
		}
	}

	void LoadParticleMap() {
		float pixelPerUnit = UIManager.Instance.PixelPerUnit;
		particleSizeMap.Clear();
		particleDataMap.Clear();
		if (m_ParticleAtlasMap) foreach (var pair in m_ParticleAtlasMap.AtlasMap) {
			// ParticleType_Index_Duration
			string[] split = pair.Key.Split('_');
			if (split.Length != 3) continue;

			int[] value = new int[3];
			value[0] = (int)Enum.Parse(typeof(ParticleType), split[0]);
			value[1] = int.Parse(split[1]);

			int[] key = new int[3];
			key[0] = 0;
			key[1] = key[0] + (((value[0] + 1) & 0xff) << 24);
			key[2] = key[1] + (((value[1] + 1) & 0xff) << 16);

			for (int k = 2 - 1; -1 < k; k--) {
				if (!particleSizeMap.ContainsKey(key[k])) particleSizeMap.Add(key[k], 0);
				particleSizeMap[key[k]]++;
				if (k == 0 || particleSizeMap.ContainsKey(key[k - 1])) break;
			}
			if (!particleSizeMap.ContainsKey(key[2])) particleSizeMap[key[2]] = value[2];
			if (1 < particleSizeMap[key[1]]) particleSizeMap[key[2]] += particleSizeMap[key[2] - 1];

			particleDataMap.Add(key[2], new ParticleData() {
				position = new Vector3(0, 0, 0),
				scale    = new Vector3(pair.Value.size.x, pair.Value.size.y, 1) / pixelPerUnit,

				tiling   = new Vector2(pair.Value.tiling.x, pair.Value.tiling.y),
				offset   = new Vector2(pair.Value.offset.x, pair.Value.offset.y),
				color    = new Vector3(1, 1, 1),
				emission = 0,
				alpha    = 1,
			});
		}
	}



	GPUBatcher<CreatureData>   creatureBatcher;
	GPUBatcher<ParticleData>   particleBatcher;
	GPUBatcher<ShadowOnlyData> shadowOnlyBatcher;

	void ConstructGPUBatcher() {
		creatureBatcher   = new GPUBatcher<CreatureData  >(m_CreatureMaterial,   m_QuadMesh,   0);
		particleBatcher   = new GPUBatcher<ParticleData  >(m_ParticleMaterial,   m_QuadMesh,   0);
		shadowOnlyBatcher = new GPUBatcher<ShadowOnlyData>(m_ShadowOnlyMaterial, m_SphereMesh, 0);
		creatureBatcher  .param.layer = LayerMask.NameToLayer("Entity");
		particleBatcher  .param.layer = LayerMask.NameToLayer("Entity");
		shadowOnlyBatcher.param.layer = LayerMask.NameToLayer("Entity");
		creatureBatcher  .param.receiveShadows = false;
		particleBatcher  .param.receiveShadows = false;
		shadowOnlyBatcher.param.receiveShadows = false;
		creatureBatcher  .param.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		particleBatcher  .param.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		shadowOnlyBatcher.param.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
	}

	void DestructGPUBatcher() {
		creatureBatcher  ?.Dispose();
		particleBatcher  ?.Dispose();
		shadowOnlyBatcher?.Dispose();
	}

	Func<int, int> func;

	void DrawCreature() {
		float cameraYaw = GetYaw(CameraManager.Rotation);
		foreach (Creature creature in Creature.GetList()) {
			CreatureType  creatureType  = creature.CreatureType;
			AnimationType animationType = creature.AnimationType;

			float relativeYaw   = GetYaw(creature.transform.rotation) - cameraYaw;
			int   numDirections = GetCreatureSize(creatureType, animationType);
			GetDirection(relativeYaw, numDirections, out int direction, out bool xflip);

			int count = GetCreatureSize(creatureType, animationType, direction);
			int total = GetCreatureSize(creatureType, animationType, direction, count - 1);
			int value = (int)(creature.Offset * 1000) % total;
			func = i => GetCreatureSize(creatureType, animationType, direction, i);
			int index = GetIndex(count, value, func);

			CreatureData data = GetCreatureData(creatureType, animationType, direction, index);
			data.position = creature.transform.position;
			data.rotation = CameraManager.Rotation;
			if (xflip) {
				data.offset.x += data.tiling.x;
				data.tiling.x *= -1;
			}
			data.alpha = creature.TransitionOpacity;

			creatureBatcher.Add(data);
			shadowOnlyBatcher.Add(new ShadowOnlyData() {
				position = creature.transform.position,
				rotation = new Vector4(0, 0, 0, 1),
				scale    = creature.transform.localScale,
			});
		}
		creatureBatcher  ?.Draw ();
		shadowOnlyBatcher?.Draw ();
		creatureBatcher  ?.Clear();
		shadowOnlyBatcher?.Clear();
	}

	void DrawParticle() {
		float cameraYaw = GetYaw(CameraManager.Rotation);
		foreach (Particle particle in Particle.GetList()) {
			ParticleType particleType = particle.ParticleType;

			int count = GetParticleSize(particleType);
			int total = GetParticleSize(particleType, count - 1);
			int value = (int)(particle.Offset * 1000) % total;
			func = i => GetParticleSize(particleType, i);
			int index = GetIndex(count, value, func);

			ParticleData data = GetParticleData(particleType, index);
			data.position = particle.transform.position;
			data.rotation = CameraManager.Rotation;
			data.alpha = particle.TransitionOpacity;

			particleBatcher.Add(data);
		}
		particleBatcher.Draw();
		particleBatcher.Clear();
	}



	// Lifecycle

	void Start() {
		LoadCreatureMap();
		LoadParticleMap();
	}

	void LateUpdate() {
		DrawCreature();
		DrawParticle();
	}

	void OnEnable () => ConstructGPUBatcher();
	void OnDisable() =>  DestructGPUBatcher();
}



// ====================================================================================================
// Native List
// ====================================================================================================

public struct NativeList<T> : IDisposable where T : struct {

	// Fields

	NativeArray<T> narray;
	int            length;



	// Properties

	public T this[int index] {
		get => narray[index];
		set {
			narray[index] = value;
			length = Mathf.Max(length, index + 1);
		}
	}

	public int Length => length;

	public int Capacity {
		get => narray.Length;
		set {
			value  = Mathf.Max(value, 4);
			length = Mathf.Min(value, length);
			NativeArray<T> narrayTemp = new NativeArray<T>(value, Allocator.Persistent);
			if (0 < length) NativeArray<T>.Copy(narray, narrayTemp, length);
			narray.Dispose();
			narray = narrayTemp;
		}
	}



	// Constructor, Destructor

	public NativeList(int capacity = 64) {
		narray = new NativeArray<T>(Mathf.Max(capacity, 4), Allocator.Persistent);
		length = 0;
	}

	public void Dispose() => narray.Dispose();



	// Methods

	public NativeArray<T> GetArray() => narray;

	public void Add(T value) => Insert(length, value);

	public void Insert(int index, T value) {
		if (Capacity < index + 1) Capacity = Mathf.Max(Capacity + 1, Capacity * 2);
		if (0 < length - index) NativeArray<T>.Copy(narray, index, narray, index + 1, length - index);
		narray[index] = value;
		length += 1;
	}

	public void AddRange(NativeList<T> list) => InsertRange(length, list);

	public void InsertRange(int index, NativeList<T> list) {
		int i = list.Length;
		if (Capacity < length + i) Capacity = Mathf.Max(Capacity + i, Capacity * 2);
		if (0 < length - index) NativeArray<T>.Copy(narray, index, narray, index + i, length - index);
		NativeArray<T>.Copy(list.GetArray(), 0, narray, index, i);
		length += i;
	}

	public void RemoveAt(int index) => RemoveRange(index, 1);

	public void RemoveRange(int index, int count) {
		int i = Mathf.Min(count, length - index);
		NativeArray<T>.Copy(narray, index + i, narray, index, length - index - i);
		length -= i;
	}

	public void Clear() => length = 0;
}



// ====================================================================================================
// GPU Batcher
// ====================================================================================================

public class GPUBatcher<T> : IDisposable where T : unmanaged {

	// Constants

	const GraphicsBuffer.Target Args       = GraphicsBuffer.Target.IndirectArguments;
	const GraphicsBuffer.Target Structured = GraphicsBuffer.Target.Structured;



	// Fields

	Mesh renderMesh;
	int  stride;
	int  propID;

	NativeList<int> narrayArgs;
	GraphicsBuffer  bufferArgs;
	NativeList<T>   narrayStructured;
	GraphicsBuffer  bufferStructured;

	public RenderParams param;

	int i;
	int j;



	// Properties

	public int Length => narrayStructured.Length;

	public int Capacity {
		get => narrayStructured.Capacity;
		set => narrayStructured.Capacity = value;
	}



	// Constructor, Destructor

	public GPUBatcher(Material material, Mesh mesh, int submesh) {
		renderMesh = mesh;
		stride     = Marshal.SizeOf<T>();
		propID     = Shader.PropertyToID($"_{typeof(T).Name}");

		narrayArgs = new NativeList<int>(5) {
			[0] = (int)mesh.GetIndexCount(submesh),
			[1] = 0,
			[2] = (int)mesh.GetIndexStart(submesh),
			[3] = (int)mesh.GetBaseVertex(submesh),
			[4] = 0
		};
		bufferArgs = new GraphicsBuffer(Args, narrayArgs.Capacity, sizeof(int));
		bufferArgs.SetData(narrayArgs.GetArray(), 0, 0, narrayArgs.Length);

		narrayStructured = new NativeList<T>(64);
		bufferStructured = new GraphicsBuffer(Structured, narrayStructured.Capacity, stride);
		bufferStructured.SetData(narrayStructured.GetArray(), 0, 0, narrayStructured.Length);

		param = new RenderParams(material) {
			worldBounds       = new Bounds(Vector3.zero, Vector3.one * 1024),
			matProps          = new MaterialPropertyBlock(),
			shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
			receiveShadows    = true
		};
		param.matProps.SetBuffer(propID, bufferStructured);
	}

	public void Dispose() {
		narrayArgs.Dispose();
		bufferArgs.Release();
		narrayStructured.Dispose();
		bufferStructured.Release();
	}



	// Methods

	public void Add(T value) => Insert(Length, value);
	
	public void Insert(int index, T value) {
		narrayStructured.Insert(index, value);
		i = Mathf.Min(i, index );
		j = Mathf.Max(j, Length);
	}

	public void AddRange(NativeList<T> value) => InsertRange(Length, value);

	public void InsertRange(int index, NativeList<T> value) {
		narrayStructured.InsertRange(index, value);
		i = Mathf.Min(i, index );
		j = Mathf.Max(j, Length);
	}

	public void RemoveAt(int index) => RemoveRange(index, 1);

	public void RemoveRange(int index, int count) {
		narrayStructured.RemoveRange(index, count);
		i = Mathf.Min(i, index );
		j = Mathf.Max(j, Length);
	}

	public void Clear() {
		narrayStructured.Clear();
		i = 0;
		j = 0;
	}

	public void Draw() {
		if (Length == 0) return;
		if (narrayArgs[1] != Length) {
			narrayArgs[1]  = Length;
			bufferArgs.SetData(narrayArgs.GetArray(), 0, 0, narrayArgs.Length);
		}
		if (bufferStructured.count != Capacity) {
			bufferStructured.Release();
			bufferStructured = new GraphicsBuffer(Structured, Capacity, stride);
			param.matProps.SetBuffer(propID, bufferStructured);
			i = 0;
			j = Length;
		}
		if (i < j) {
			bufferStructured.SetData(narrayStructured.GetArray(), i, i, j - i);
			i = Length;
			j = 0;
		}
		Graphics.RenderMeshIndirect(in param, renderMesh, bufferArgs);
	}
}
