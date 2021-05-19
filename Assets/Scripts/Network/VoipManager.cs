using UnityEngine;
using System.Collections;
using UnityEngine.Audio;

public class VoipManager : MonoBehaviour
{
    [Header("Audio Quality")]
    [SerializeField] FrequencyModes Frequency = FrequencyModes._8000;
    enum FrequencyModes {_44100 = 44100,_22050 = 22050, _8000 = 8000};
    [SerializeField] [Range(0.01f,0.5f)] float buffer = 0.3f;

    [Header("Audio Levels")]
    [SerializeField] [Range(0,1f)] float levelsOUT;
    [SerializeField] [Range(0,1f)] float levelThreshold = 0.5f;

    [Header("Audio Data")]
    public bool microphoneAvailable;
    public bool clientAvailable;
    public float dataSentMB;

    [Header("Audio Sources")]
    public AudioSource voice;
    public AudioMixerGroup voipMixer;

    float bufferTimer;
    float dataSize;
    AudioClip micOUT;
    int lastSample;
    const int clipLength = 5;
    const int micChannels = 1;
    float micCutTimer;

    void Start() {
        microphoneAvailable = Microphone.devices.Length > 0;
        clientAvailable = false;
        if (!microphoneAvailable) { return; }
        micOUT = Microphone.Start(null,true,clipLength,(int)Frequency);
        StartCoroutine(detectClientAvatarVoice());
    }

    IEnumerator detectClientAvatarVoice() {
        WaitForSeconds wait = new WaitForSeconds(1);

        while (!clientAvailable) {
            GameObject clientA = null; // obj to parent voice to.
            if (clientA) {
                voice = clientA.AddComponent < AudioSource>();
                voice.spatialBlend = 1;
                voice.loop = true;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.maxDistance = 15;
                voice.minDistance = 1;
                voice.dopplerLevel = 0;
                voice.volume = 1;
                voice.playOnAwake = false;
                voice.outputAudioMixerGroup = voipMixer;

                clientAvailable = true;
            }
            yield return wait;
        }
        voice.clip = AudioClip.Create("voipClip",clipLength * (int)Frequency,micChannels,(int)Frequency,false);
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.M)) { AvatarNetworkManager.testMic = !AvatarNetworkManager.testMic; }
    }

    void FixedUpdate() {
        if (clientAvailable) {audioIn();}
        if (microphoneAvailable) {micOut();}
    }
    void micOut() {
        bufferTimer += Time.deltaTime;
        if (bufferTimer > buffer) {
            bufferTimer = 0;
            int pos = Microphone.GetPosition(null);
            int diff = pos - lastSample;
            if (diff > 0) {
                float[] samples = new float[diff * micChannels];
                micOUT.GetData(samples,lastSample);
                byte[] ba = ToByteArray(samples);
                levelsOUT = formatDB(ComputeDB(samples,0,ref diff));
                micCutTimer = levelsOUT > levelThreshold ? 1 : micCutTimer - (Time.fixedDeltaTime * 10f);
                micCutTimer = Mathf.Clamp01(micCutTimer);
                if (micCutTimer > 0) {
                    AvatarNetworkManager.sendMicData(ba);
                    dataSize += ba.Length;
                    dataSentMB = Mathf.Round(dataSize / 10240f)/100f;
                }
            }
           
            lastSample = pos;
        }
        else { AvatarNetworkManager.sendMicData(null);}
    }

    void audioIn() {
        byte[] sampleIn = AvatarNetworkManager.getMicData();
        AvatarNetworkManager.clearMicData();
        if (sampleIn.Length == 0) {
            voice.loop = false;
            return;
        }
        float[] f = ToFloatArray(sampleIn);
        voice.clip = AudioClip.Create("",f.Length,micChannels,(int)Frequency,false);
        voice.clip.SetData(f,0);
        if (!voice.isPlaying) voice.Play();
    }


    public byte[] ToByteArray(float[] floatArray) {
        int len = floatArray.Length * 4;
        byte[] byteArray = new byte[len];
        int pos = 0;
        foreach (float f in floatArray) {
            byte[] data = System.BitConverter.GetBytes(f);
            System.Array.Copy(data,0,byteArray,pos,4);
            pos += 4;
        }
        return byteArray;
    }

    public float[] ToFloatArray(byte[] byteArray) {
        int len = byteArray.Length / 4;
        float[] floatArray = new float[len];
        for (int i = 0; i < byteArray.Length; i += 4) {
            floatArray[i / 4] = System.BitConverter.ToSingle(byteArray,i);
        }
        return floatArray;
    }
   
    public static float ComputeDB(float[] buffer,int offset,ref int length) {

        float sos = 0f;
        float val;
        if (offset + length > buffer.Length) {
            length = buffer.Length - offset;
        }
        for (int i = 0; i < length; i++) {
            val = buffer[offset];
            sos += val * val;
            offset++;
        }
        // return sqrt of average
        float rms = Mathf.Sqrt(sos / length);
        // could divide rms by reference power, simplified version here with ref power of 1f.
        // will return negative values: 0db is the maximum.
        return 10 * Mathf.Log10(rms);
    }

    public static float formatDB(float dbVal) {
        return Mathf.Clamp01((dbVal + 30 ) / 30f);
    }

    void OnDestroy() {
        Microphone.End(null);
    }
}
