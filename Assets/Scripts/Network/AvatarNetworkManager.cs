using System;
using System.Collections;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Linq;

public class AvatarNetworkManager:MonoBehaviour {

    [Header("Network Debug")]

    public connectionStatuses connectionStatus = connectionStatuses.Disconnected;
    public enum connectionStatuses { Disconnected, Connecting, Connected, ConnectedHost, ConnectedClient, Error }
    [SerializeField] bool TX; // transmit
    [SerializeField] bool RX; // receive

    public dataTypes dataTypeSent = dataTypes.NA;
    public dataTypes dataTypeReceived = dataTypes.NA;
    public enum dataTypes { NA, M1, F1, M2, F2, generic, worldObj1, voice}
    [SerializeField] string lastReceived;
    [SerializeField] string lastSent;
    public string targetIP;
    public int port;
    public string thisIP;
    int hostSeed;
    UdpState s;
    [SerializeField] [Range(0,1f)] float sendRate = 0.2f;
    float sendRateStamp;

    public static AvatarNetworkManager instance;

    [Header("Model 1")]
    [SerializeField] RDM_AvatarExtentsTracking MrightHand;
    [SerializeField] RDM_AvatarExtentsTracking MleftHand;
    [SerializeField] RDM_AvatarExtentsTracking Mhead;

    [Header("Model 2")]
    [SerializeField] RDM_AvatarExtentsTracking MrightHand2;
    [SerializeField] RDM_AvatarExtentsTracking MleftHand2;
    [SerializeField] RDM_AvatarExtentsTracking Mhead2;

    [Header("Scene Data")]
    public float deltaDistance;
    dataTypes clientType = dataTypes.NA;
    dataTypes clientTypeHist = dataTypes.NA;
    string[] dataSplitSet, MposData, MrotData;

    float readDistance;
    
    Vector3 readPosH, readPosR, readPosL;
    Quaternion readRotH, readRotR, readRotL;

    // "connection" things
    IPEndPoint remoteEndPoint;
    UdpClient client;
    byte[] dataIn;
    bool messageReceived;
    AsyncCallback ascb;
    int clientavatarbackup;

    //microphoneData
    static byte[] micData;
    public static bool testMic;

    private void Awake() {
        instance = this;
        //reduces errors when running and clicking out of window
        Application.runInBackground = true;
        //start time seed to determine which instance is Host. Fist starting instance is host.
        DateTime epochStart = new DateTime(2018,1,1,0,0,0,DateTimeKind.Utc);
        hostSeed = (int)( DateTime.UtcNow - epochStart ).TotalSeconds;
        micData = new byte[0];
        testMic = false;
    }

    void Start() {
        //Begin connection routine
        StartCoroutine(initConnectAction());
    }

    private void Update() {
        updateDataIn();
        lerpReadPositionType();

        sendRateStamp += Time.deltaTime * sendRate;
        if (sendRateStamp >= 0.01f) {
            sendRateStamp = 0;
            updateDataOut();
        }

        
    }

    private void OnGUI() {
        //Monitor Sending and Receiving
        GUI.Label(new Rect(10,0,500,20),String.Concat(connectionStatus.ToString(),( TX == true ? " TX" : "" )));
        GUI.Label(new Rect(10,25,500,20),String.Concat(connectionStatus.ToString(),(RX == true?" RX" : "")));
        GUI.Label(new Rect(10,50,500,20),lastReceived);
        GUI.Label(new Rect(10,75,500,20),clientType.ToString());
        //GUI.Label(new Rect(10,100,500,20),micData.Length > 0 == true ? " VOIP IN" : "" );
    }

    //recurring data out
    void updateDataOut() {
        if (client == null) { TX = false; return; } // abandon if no client
        if (dataTypeSent == dataTypes.worldObj1) //send the position of train
        {
            sendData(lastSent,dataTypeSent);
            dataTypeSent = dataTypes.NA;
        }
        else if(connectionStatus == connectionStatuses.ConnectedHost || connectionStatus == connectionStatuses.ConnectedClient) {

            lastSent = writePositions(null,null,null);
            sendData(lastSent,dataTypeSent);
        }
    }


    //send unique command. must have identifiable characters for receiving end.
    void sendGenericMessage(string m) {
        Debug.Log("[Network Manager] Sending Generic Message: " + m);
        lastSent = m;
        sendData(lastSent,dataTypes.generic);
    }

    public static void sendMicData(byte[] micOut) {
        if (testMic) { micData = micOut; return; }
        if (micOut == null || micOut.Length == 0) { return; }
        if (instance) {
            instance.sendData(addByteToArray(micOut,(byte)dataTypes.voice));
        }
    }

    public static byte[] getMicData() {
        if (micData != null) {return micData;}
        else {return new byte[0];}
    }

    public static void clearMicData() {
        micData = new byte[0];
    }

    public static byte[] addByteToArray(byte[] bArray,byte newByte) {
        byte[] newArray = new byte[bArray.Length + 1];
        bArray.CopyTo(newArray,1);
        newArray[0] = newByte;
        return newArray;
    }

    public static byte[] removeByteFromArray(byte[] bArray) {
        return bArray.Skip(1).ToArray();
    }

    //bytes then send
    void sendData(string m, dataTypes type) {
        byte[] data = addByteToArray(Encoding.UTF8.GetBytes(m),(byte)type);
        try {
            TX = !string.IsNullOrEmpty(m);
            client.Send(data,data.Length,remoteEndPoint);
        }
        catch (Exception e) {
            TX = false;
            Debug.LogError(e);
            return;
        }
    }

    void sendData(byte[] m) {
        if (remoteEndPoint == null) { return; }
        
        client.Send(m,m.Length,remoteEndPoint);
    }

    //receive data must wait for "messageReceived" which is controlled by the Callback: ReceiveCallback()
    void updateDataIn() {
        RX = false;
        if (client == null) { return; }
        //if not initialized, initialize
        if (s.stateE == null || port == 0) {
            IPEndPoint e = new IPEndPoint(IPAddress.Any,port);
            s = new UdpState {
                stateE = e,
                stateU = client
            };
            ascb = new AsyncCallback(ReceiveCallback);
            client.BeginReceive(ascb,s);
        }
        //get data, switch recieved back to false, wait for listener;
        if (messageReceived) {
            processInData(dataIn);
            client.BeginReceive(ascb,s);
            messageReceived = false;
        }
    }

    void processInData(byte[] d) {
        lastReceived = string.Empty;
        if (d.Length > 0) {
            RX = true;
            byte byte1 = d[0];

            dataTypeReceived = (dataTypes)byte1;
            //microphone data
            if ((dataTypes)byte1 == dataTypes.voice) {
                micData = removeByteFromArray(d);
            }
            //common data type 1: train can be repleased for common updates, like synching states  
            else if ((dataTypes)byte1 == dataTypes.worldObj1) {
               
            }
            //generic data types are one-off commands, usually headed with a letter
            else if ((dataTypes) byte1 == dataTypes.generic) {
                lastReceived = Encoding.UTF8.GetString(removeByteFromArray(d));
                dataSplitSet = lastReceived.Split(':');
                if (dataSplitSet[0] == "H") { //Host request
                    if (dataSplitSet.Length == 2) {
                        RX_establishHost(int.Parse(dataSplitSet[1]));
                    }
                }
                else if (dataSplitSet[0] == "I") { //Start Session Command
                    if (dataSplitSet.Length == 2) {
                        RX_StartSession(int.Parse(dataSplitSet[1]));
                    }
                }
                else if (dataSplitSet[0] == "C") { //Match IP
                    if (dataSplitSet.Length == 2) {
                        RX_establishConnection(dataSplitSet[1]);
                    }
                }
            }
            // limb inputs fall into multiple categories for this project. Should be switched to a common data type as above.
            else {
                lastReceived = Encoding.UTF8.GetString(removeByteFromArray(d));
                dataSplitSet = lastReceived.Split(':');
                if (dataSplitSet.Length >= 2) {
                    MposData = dataSplitSet[0].Split('|');
                    MrotData = dataSplitSet[1].Split('|');
                    if ((dataTypes)byte1 == dataTypes.M1) { clientType = dataTypes.M1; }
                    else if ((dataTypes)byte1 == dataTypes.F1) { clientType = dataTypes.F1; }
                    else if ((dataTypes)byte1 == dataTypes.M2) { clientType = dataTypes.M2; }
                    else if ((dataTypes)byte1 == dataTypes.F2) { clientType = dataTypes.F2; }
                    readPositions(MposData,MrotData);
                }
                if (clientType != clientTypeHist) {  }
                clientTypeHist = clientType;
            }
        }
    }

    //generic test to see if connection accepted as host or client
    public bool networkReady() {
        return connectionStatus == connectionStatuses.ConnectedClient || connectionStatus == connectionStatuses.ConnectedHost;
    }

    //receive callback.  This was created specifically for Unity to remove multithreading. Callback uses asynchronous receives.

    public void ReceiveCallback(IAsyncResult ar) {
        UdpClient u = ( (UdpState)( ar.AsyncState ) ).stateU;
        IPEndPoint e = ( (UdpState)( ar.AsyncState ) ).stateE;
        dataIn = u.EndReceive(ar,ref e);
        messageReceived = true;
    }

    //are you the host in this connection?
    public bool isHost() {
        return connectionStatus == connectionStatuses.ConnectedHost;
    }

    public void TX_StartSession(int s) {
        if (connectionStatus == connectionStatuses.ConnectedHost) {
            sendGenericMessage(string.Concat("I:",s));
        }
    }
    void TX_establishConnection() {
       
        sendGenericMessage("C:" + thisIP);
    }
    void TX_establishHost() {
        sendGenericMessage("H:" + hostSeed);
    }

    void RX_StartSession(int s) {
        Debug.Log("[NetworkManager] Receiving RX_StartSession");
      
    }
    void RX_establishConnection(string ipIn) {
        Debug.Log("[NetworkManager] Receiving RX_establishConnection");
        if (connectionStatus == connectionStatuses.Connecting) {
            if (targetIP == ipIn) { connectionStatus = connectionStatuses.Connected; }
        }
    }
    void RX_establishHost(float seed) {
        Debug.Log("[NetworkManager] Receiving RX_establishHost");
        if (connectionStatus == connectionStatuses.Connected){
            if (seed < hostSeed) {
                connectionStatus = connectionStatuses.ConnectedClient;
            }
            else {
                connectionStatus = connectionStatuses.ConnectedHost;
            }
        }
    }

    //transform position and rotation turned into a string.
    string writePositions(RDM_AvatarExtentsTracking head,RDM_AvatarExtentsTracking L_hand,RDM_AvatarExtentsTracking R_hand) {
        //Reading the AvatarExtentsTracking moved the end to where the VR effectors are, then records the local position. This is so we can track local position only for the extents, before they are sent through the network.
        head.read();
        L_hand.read();
        R_hand.read();
        string data = string.Concat(
            R_hand.transform.localPosition.ToString("F3")
            ,"|",L_hand.transform.localPosition.ToString("F3")
            ,"|",head.transform.localPosition.ToString("F3")
            ,":",R_hand.transform.rotation.ToString("F3")
            ,"|",L_hand.transform.rotation.ToString("F3")
            ,"|",head.transform.rotation.ToString("F3"));
        return data;
    }

    //project specific listener for client model type swaps
   

    //read split data into pos and rots
    void readPositions(string[] posData,string[] RotData) {
        readPosR = StringToVector3(posData[0]);
        readPosL = StringToVector3(posData[1]);
        readPosH = StringToVector3(posData[2]);

        readRotR = StringToQuaternion(RotData[0]);
        readRotL = StringToQuaternion(RotData[1]);
        readRotH = StringToQuaternion(RotData[2]);
    }

    //pos and rots are updated irregularly, but this function lerps in between updates. Speed is set to 20, but should be balanced between receive rates and interpolation speed.
    void lerpReadPositions(Transform head,Transform L_hand,Transform R_hand) {
        R_hand.localPosition = Vector3.Lerp(R_hand.localPosition,readPosR,Time.deltaTime * 20f);
        L_hand.localPosition = Vector3.Lerp(L_hand.localPosition,readPosL,Time.deltaTime * 20f);
        head.localPosition = Vector3.Lerp(head.localPosition,readPosH,Time.deltaTime * 20f);

        R_hand.rotation = Quaternion.Lerp(R_hand.rotation,readRotR,Time.deltaTime * 20f);
        L_hand.rotation = Quaternion.Lerp(L_hand.rotation,readRotL,Time.deltaTime * 20f);
        head.rotation = Quaternion.Lerp(head.rotation,readRotH,Time.deltaTime * 20f);
    }

    //finds specific model to lerp

    void lerpReadPositionType() {
        if (clientType == dataTypes.M1) {
            lerpReadPositions(Mhead.transform,MleftHand.transform,MrightHand.transform);
        }
        else if (clientType == dataTypes.F1) {
            lerpReadPositions(Mhead.transform,MleftHand.transform,MrightHand.transform);
        }
        else if (clientType == dataTypes.M2) {
            lerpReadPositions(Mhead2.transform,MleftHand2.transform,MrightHand2.transform);
        }
        else if (clientType == dataTypes.F2) {
            lerpReadPositions(Mhead2.transform,MleftHand2.transform,MrightHand2.transform);
        }

    }

    public static Vector3 StringToVector3(string sVector) {
        // Remove the parentheses
        if (sVector.StartsWith("(") && sVector.EndsWith(")")) {
            sVector = sVector.Substring(1,sVector.Length - 2);
        }

        // split the items
        string[] sArray = sVector.Split(',');

        // store as a Vector3
        Vector3 result = new Vector3(
            float.Parse(sArray[0]),
            float.Parse(sArray[1]),
            float.Parse(sArray[2]));

        return result;
    }

    public static Quaternion StringToQuaternion(string sQuaternion) {
        // Remove the parentheses
        if (sQuaternion.StartsWith("(") && sQuaternion.EndsWith(")")) {
            sQuaternion = sQuaternion.Substring(1,sQuaternion.Length - 2);
        }

        // split the items
        string[] sArray = sQuaternion.Split(',');

        // store as a Vector3
        Quaternion result = new Quaternion(
            float.Parse(sArray[0]),
            float.Parse(sArray[1]),
            float.Parse(sArray[2]),
            float.Parse(sArray[3]));

        return result;
    }

    //start automatic connection process. You will need a UI to manually enter an IP and port.
    IEnumerator initConnectAction() {
        connectionStatus = connectionStatuses.Disconnected;
        yield return null;
        thisIP = GetIP(ADDRESSFAM.IPv4);

        //overall wait interval for scanning. Can add a counter for a timeout if needed.
        WaitForSeconds wait = new WaitForSeconds(0.5f);
        yield return new WaitForSeconds(5);
        //wait if IP or port are empty
        while (string.IsNullOrEmpty(targetIP) || port == 0) {
            yield return null;
        }
        //begin connection process
        client = new UdpClient(port);

        connectionStatus = connectionStatuses.Connecting;
        while (connectionStatus == connectionStatuses.Connecting) {
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(targetIP),port);
            PlayerPrefs.SetString("IP",string.Concat(targetIP,":",port));
            Debug.Log("[NetworkManager] Waiting on connection to targetIP");
            TX_establishConnection();
            yield return wait;
        }
        while (connectionStatus == connectionStatuses.Connected) {
            Debug.Log("[NetworkManager] Waiting on Host/Client establishment...");
            TX_establishConnection();
            TX_establishHost();
            yield return wait;
        }
        while (connectionStatus != connectionStatuses.Disconnected) {
            TX_establishHost();
            if (connectionStatus == connectionStatuses.ConnectedHost) {
                Debug.Log("[NetworkManager] Host Methods");
                hostMethods();
                yield return wait;
            }
            else if (connectionStatus == connectionStatuses.ConnectedClient) {
                Debug.Log("[NetworkManager] Client Methods");
                clientMethods();
                yield return wait;
            }
        }
    }

    void hostMethods() {
        // send train data once every second;
       
        dataTypeSent = dataTypes.worldObj1;
    }

    void clientMethods() {
       
    }

    // sendData
    private bool sendString(string message) {
        try {
            byte[] data = Encoding.UTF8.GetBytes(message);
            client.Send(data,data.Length,remoteEndPoint);
            return true;
        }
        catch (Exception err) {
            print(err.ToString());
            return false;
        }
    }

    private void OnDestroy() {
        if (client != null) { client.Dispose(); }
    }

    public static string GetIP(ADDRESSFAM Addfam) {
        //Return null if ADDRESSFAM is Ipv6 but Os does not support it
        if (Addfam == ADDRESSFAM.IPv6 && !Socket.OSSupportsIPv6) {
            return null;
        }

        string output = "";

        foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces()) {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

            if (( item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || item.NetworkInterfaceType == NetworkInterfaceType.Ethernet ) && item.OperationalStatus == OperationalStatus.Up)
#endif 
            {
                foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses) {
                    //IPv4
                    if (Addfam == ADDRESSFAM.IPv4) {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork) {
                            output = ip.Address.ToString();
                        }
                    }

                    //IPv6
                    else if (Addfam == ADDRESSFAM.IPv6) {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6) {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
        }
        return output;
    }
}

public enum ADDRESSFAM {
    IPv4, IPv6
}

public struct UdpState {
    public UdpClient stateU;
    public IPEndPoint stateE;
}

