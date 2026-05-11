using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditorInternal;
#endif

public class RecordTransformHierarchy : MonoBehaviour
{
    public AnimationClip clip;
    public bool record = false;

#if UNITY_EDITOR
    private GameObjectRecorder m_Recorder;

    void Start()
    {
        // Create recorder and record the script GameObject.
        m_Recorder = new GameObjectRecorder(gameObject);

        // Bind all the Transforms on the GameObject and all its children.
        m_Recorder.BindComponentsOfType<Transform>(gameObject, true);
        // m_Recorder.BindComponentsOfType<Rigidbody>(gameObject, true);
        // m_Recorder.BindComponentsOfType<ParticleSystem>(gameObject, true);
        // m_Recorder.BindComponentsOfType<Collider>(gameObject, true);
        // m_Recorder.BindComponentsOfType<WheelCollider>(gameObject, true);
        // m_Recorder.BindComponentsOfType<MeshCollider>(gameObject, true);
        // m_Recorder.BindComponentsOfType<VehicleController>(gameObject,true);
        // m_Recorder.BindComponentsOfType<SuspensionManager>(gameObject,true);
        // m_Recorder.BindComponentsOfType<MonoBehaviour>(gameObject, true);
    }

    private void SaveRecordingAndPersist()
    {
        if (clip == null || m_Recorder == null || !m_Recorder.isRecording)
            return;

        m_Recorder.SaveToClip(clip);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
    }

    void LateUpdate()
    {
        if (clip == null || m_Recorder == null)
            return;

        if (record)
        {
            m_Recorder.TakeSnapshot(Time.deltaTime);
        }
        else if (m_Recorder.isRecording)
        {
            SaveRecordingAndPersist();
            m_Recorder.ResetRecording();
        }
    }

    void OnDisable()
    {
        SaveRecordingAndPersist();
    }
#else
    void Start() { }
    void LateUpdate() { }
    void OnDisable() { }
#endif
}
