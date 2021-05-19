using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.XR;
using System;

public class DataLogger : MonoBehaviour
{
    public static DataLogger instance;

    [Header("Data Objects")]
    [SerializeField] Camera head;
    [SerializeField] ICATEmpaticaBLEClient empatica;

    [Header("Study Information")]
    public bool paused = true;
    public string EMDSessionTag = "S1";
    string EMDSessionName;
    string EMDSessionPath;

    [Header("Data Collection")]
    [SerializeField, Range(0.1f,2f)] float EMDCollectInterval = 0.25f;
    float sceneTime;
    [SerializeField] float sceneTimer;
    float framecounter;
    const int EMDBufferSize = 30;
    int buffercounter;
    EMDtype[] EMDBuffer;
    bool dataStarted;

    bool empaticaBufferCheck;
    DateTime epochStart;
    float headYaw;
    float headPitch;
    float headBank;
    float headForward;
    float headLateral;
    float headVertical;
    int blinkHist;
    int closedHist;

    [Header("Data Collection Debug")]
    [SerializeField] string dataUsedPercent;
    [SerializeField, Range(0,0.5f)] float matchingTolerance = 0.1f;
    [SerializeField] bool autoMatchTolerance = true;
    [SerializeField] EmpaticaType[] empaticaBuffer;
    int dataCollected;
    int dataUsed;
    float dataUse;
    float lastGSR = 0;
    float lastIBI = 0;
    float lastHR = 0;
    StreamWriter f;
   

    private void Awake() {
        instance = this;
        paused = true;
    }

    void Start()
    {
        if (!head) { head = Camera.main;}
        epochStart = new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
        sceneTime = 0;
        initializeEMD();
    }

    private void Update() {
        sceneTimer = sceneTime;
        if (!paused) { captureEMD(); }
        else { diagUpdate(); }
        sceneTime += Time.deltaTime;
    }

    public void begin() {
        paused = false;
        sceneTime = 0;

    }

   
    void initializeEMD() {
        dataStarted = false;
        EMDBuffer = new EMDtype[EMDBufferSize];
    }

    void diagUpdate() {
        framecounter += Time.deltaTime;
        if (framecounter < EMDCollectInterval) { return; }
        framecounter = 0;

        bool dataValid = false;
        empaticaBuffer = empaticaDataParse(empatica.getValue());
        if (empaticaBuffer.Length > 0) { dataValid = empaticaBuffer[0].isValid(); }

    }

    void captureEMD() {
        framecounter += Time.deltaTime;
        if (framecounter < EMDCollectInterval) { return; }
        framecounter = 0;

      
        //head angle
        headYaw = head.transform.localRotation.y * 90f;
        headPitch = -head.transform.localRotation.x * 90f;
        headBank = head.transform.localRotation.z * 90f;
        headForward = head.transform.localPosition.z;
        headLateral = head.transform.localPosition.x;
        headVertical = head.transform.localPosition.y;


        //Write to File
        DateTime time = DateTime.UtcNow;

        EMDBuffer[buffercounter].timestamp = ( ( time - epochStart ).TotalMilliseconds ) / 1000d;
        EMDBuffer[buffercounter].time = time;
        EMDBuffer[buffercounter].sceneTime = sceneTime;
        EMDBuffer[buffercounter].headYaw = headYaw;
        EMDBuffer[buffercounter].headPitch = headPitch;
        EMDBuffer[buffercounter].headBank = headBank;
        EMDBuffer[buffercounter].headForward = headForward;
        EMDBuffer[buffercounter].headVertical = headVertical;
        EMDBuffer[buffercounter].headLateral = headLateral;

        empaticaBuffer = empaticaDataParse(empatica.getValue());
        if (empaticaBuffer.Length > 0) {
            dataUsed = 0;
            for (int i = 0; i < EMDBuffer.Length; i++) {
                for (int k = 0; k < empaticaBuffer.Length; k++) {
                    if (timeStampCompare(EMDBuffer[i].timestamp,empaticaBuffer[k].timestamp)) {
                        if (EMDBuffer[i].empaticaEntry(empaticaBuffer[k])) {
                            dataUsed++;
                        }
                    }
                }
            }

            if (dataCollected > 0) {
                dataUse = ( (float)dataUsed ) / dataCollected;
                dataUsedPercent = ( dataUse * 100f ) + "%";
                if (autoMatchTolerance) {
                    if (dataUse == 1) { }//perfect. You did it!
                    else if (dataUse == 0) { }
                    else if (dataUse > 0.85f) { matchingTolerance -= 0.01f; }
                    else { matchingTolerance += 0.01f; }
                }
            }
            else { dataUsedPercent = "No Data"; }
        }

        buffercounter++;
        if (buffercounter >= EMDBufferSize) {
            buffercounter = 0;
            //fill in blank data for empatica
            //format a string array for writing quickly
            for (int i = 0; i < EMDBuffer.Length; i++) {
                if (EMDBuffer[i].eGSR > 0) { lastGSR = EMDBuffer[i].eGSR; }
                if (EMDBuffer[i].eIBI > 0) { lastIBI = EMDBuffer[i].eIBI; }
                if (EMDBuffer[i].eHR > 0) { lastHR = EMDBuffer[i].eHR; }
                if (EMDBuffer[i].eGSR == 0) { EMDBuffer[i].eGSR = lastGSR; }
                if (EMDBuffer[i].eIBI == 0) { EMDBuffer[i].eIBI = lastIBI; }
                if (EMDBuffer[i].eHR == 0) { EMDBuffer[i].eHR = lastHR; }
                if (!dataStarted) {
                    EMDSessionName = string.Concat(DateTime.Now.ToString("yyyy-MM-dd-HH"),"_",EMDSessionTag,"_EMD",".csv");
                    EMDSessionPath = string.Concat(Application.streamingAssetsPath,"/",EMDSessionName);
                }
                using (f = File.AppendText(EMDSessionPath)) {
                    if (!dataStarted) {
                        f.WriteLine("Clock,Time, HeadYaw, Head Pitch, Head Bank, Head Forward, Head Lateral, Head Vertical, e_HR,e_IBI,e_GSR, nausea, surface");
                        dataStarted = true;
                    }
                    f.WriteLine(EMDBuffer[i].formatT());
                }
            }
            EMDBuffer = new EMDtype[EMDBufferSize];
        }
    }

    bool timeStampCompare(double ts1,double ts2) {
        return Mathf.Abs((float)( ts1 - ts2 )) < matchingTolerance;
    }

    EmpaticaType[] empaticaDataParse(string dIn) {
        if (string.IsNullOrEmpty(dIn)) { return new EmpaticaType[0]; }
        dataCollected = 0;
        dIn = dIn.Replace("\r",string.Empty);
        string[] d = dIn.Split("\n"[0]);
        EmpaticaType[] et = new EmpaticaType[d.Length];
        for (int i = 0; i < d.Length; i++) {
            et[i] = new EmpaticaType();
            if (et[i].newET(d[i])) { dataCollected++; };
        }
        return et;
    }

    struct EMDtype {
        public double timestamp;
        public DateTime time;
        public float sceneTime;
        public float headYaw;
        public float headPitch;
        public float headBank;
        public float headForward;
        public float headLateral;
        public float headVertical;
        public float eHR;
        public float eIBI;
        public float eGSR;
        public float sickLevel;

        public bool empaticaEntry(EmpaticaType et) {
            if (et.gsrVal > 0) { eGSR = et.gsrVal; return true; }
            else if (et.ibiVal > 0) { eIBI = et.ibiVal; return true; }
            else if (et.hrVal > 0) { eHR = et.hrVal; return true; }
            et.clearVals();
            return false;
        }

        public string formatT() {
            return string.Concat(
            time.ToString("HH:mm:ss"),
            ",",sceneTime.ToString("F2"),
            ",",headYaw,
            ",",headPitch,
            ",",headBank,
            ",",headForward,
            ",",headLateral,
            ",",headVertical,
            ",",eHR,
            ",",eIBI,
            ",",eGSR,
            ",",sickLevel
            );
        }
    }

    [System.Serializable]
    public struct EmpaticaType {
        public double timestamp;
        public float gsrVal;
        public float ibiVal;
        public float hrVal;

        public bool newET(string inputData) {
            if (string.IsNullOrEmpty(inputData)) { return false; }
            if (inputData.Contains("OK")) { return false; }
            if (inputData.Contains("ERR")) { return false; }
            if (inputData.Contains("Gsr")) {
                string[] d = inputData.Split(' ');
                if (d.Length == 3) {
                    timestamp = double.Parse(d[1]);
                    gsrVal = float.Parse(d[2]);
                    return true;
                }
            }
            else if (inputData.Contains("Ibi")) {
                string[] d = inputData.Split(' ');
                if (d.Length == 3) {
                    timestamp = double.Parse(d[1]);
                    ibiVal = float.Parse(d[2]);
                    return true;
                }
            }
            else if (inputData.Contains("Hr")) {
                string[] d = inputData.Split(' ');
                if (d.Length == 3) {
                    timestamp = double.Parse(d[1]);
                    hrVal = float.Parse(d[2]);
                    return true;
                }
            }
            return false;
        }

        public bool isValid() {
            return gsrVal > 0 || ibiVal > 0 || hrVal > 0;
        }

        public void clearVals() {
            timestamp = 0;
            gsrVal = 0;
            ibiVal = 0;
            hrVal = 0;
        }
    }
}
