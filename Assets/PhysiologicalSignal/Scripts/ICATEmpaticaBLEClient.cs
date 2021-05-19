/* -- ICAT's Empatica Bluetooth Low Energy(BLE) Comm Client -- *
 * ----------------------------------------------------------- *
 * 0. Attach this to main camera or any empty game object
 * 1. On launch, it tries to connect to the localhost/port20 
 * 	  (You have to change it to your own ip/port combination).
 * 2. Enter the Device ID and connect to device.
 * 3. Select the data streams to log and hit "Log Data"
 * 4. Hit Ctrl+Shift+Z to disconnect at anytime.
 * 
 * Written By: Deba Saha (dpsaha@vt.edu)
 * Virginia Tech, USA.  */

 /* Slightly modified by Sungchul */

using UnityEngine;
using System.Collections;
using System.Text.RegularExpressions;
using System; 
using System.IO;
using System.Diagnostics;

using Debug = UnityEngine.Debug;

public class ICATEmpaticaBLEClient : MonoBehaviour {	
	//variables	
	private TCPConnection myTCP;	
	private string streamSelected;
	public string msgToServer;
	public string connectToServer;
	
    private string savefilename;

    //flag to indicate device conection status
    private bool deviceConnected = false;

	//flag to indicate if data to be logged to file
	[SerializeField] bool logToFile = false;

    private string currentData;

	void Awake() {		
		//add a copy of TCPConnection to this game object		
		myTCP = gameObject.AddComponent<TCPConnection>();		
	}
	
	void Start () {
         savefilename = string.Concat(Application.streamingAssetsPath, "/", DateTime.Now.ToString("yyyy-MM-dd-HH"), "_Edata.csv");

		//DisplayTimerProperties ();
		if (myTCP.socketReady == false) {			
			Debug.Log("Attempting to connect..");
			//Establish TCP connection to server
			myTCP.setupSocket();
		}
        StartCoroutine(delayInit());
	}

    IEnumerator delayInit()
    {
        while(!(myTCP.socketReady == true && deviceConnected == true))
        {
            yield return null;
        }
        SendToServer("device_subscribe gsr ON");
        SendToServer("device_subscribe ibi ON");
    }


	void Update() {

        
		//once TCP connection has been made, connect to Empatica device
		if (myTCP.socketReady == true && deviceConnected == false){
            connectToServer = "5D3A64";
            //connectToServer = "A01DE3";
            SendToServer("device_connect " + connectToServer);
            //Debug.Log("Connected to Empatica. Press Ctrl+Shift+Z to disconnect Empatica at any time");
        }
	}
   

    public string getValue() {
        string ss = SocketResponse();
        if (myTCP.socketReady == true && deviceConnected == true){return ss;}
        else { return "NA"; }
    }

    public string getValue2()
    {
        return currentData;
    }

	//socket reading script	
	string SocketResponse() {

		string serverSays = myTCP.readSocket();	
		if (serverSays != "") {		
			if (myTCP.socketReady == true && deviceConnected == true && logToFile == true){
                //streamwriter for writing to file
                currentData = serverSays;
                using (StreamWriter sw = File.AppendText(savefilename)){
					sw.WriteLine(serverSays);
				}
			}else{
				//Debug.Log("[SERVER]" + serverSays);
				string serverConnectOK = @"R device_connect OK";
				//Check if server response was device_connect OK
				if (string.CompareOrdinal(Regex.Replace(serverConnectOK,@"\s",""),Regex.Replace(serverSays.Substring(0,serverConnectOK.Length),@"\s","")) == 0){
					deviceConnected = true; 
				}
			}
		} 
        return serverSays;
	}

	//send message to the server	
	public void SendToServer(string str) {		 
		myTCP.writeSocket(str);		
		//Debug.Log ("[CLIENT] " + str);		
	}

	//Method To check Stopwatch properties
	void DisplayTimerProperties()
	{
		// Display the timer frequency and resolution.
		if (Stopwatch.IsHighResolution){
			Debug.Log("Operations timed using the system's high-resolution performance counter.");
		}else{
			Debug.Log("Operations timed using the DateTime class.");
		}
		
		long frequency = Stopwatch.Frequency;
		Debug.Log(string.Format("Timer frequency in ticks per second = {0}",frequency));
		long nanosecPerTick = (1000L*1000L*1000L) / frequency;
		Debug.Log(string.Format("Timer is accurate within {0} nanoseconds",nanosecPerTick));
	}
}

