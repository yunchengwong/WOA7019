using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class NoteData
{
    public string content;
    public float latitude;
    public float longitude;
    public Vector3 localPosition; // Position relative to the camera at the time of saving
    public Quaternion localRotation; // World rotation at the time of saving
}

public class webCamScript : MonoBehaviour
{
    public GameObject webcamPlane;
    public Button addButton;
    public GameObject notePrefab;
    public float placementDistance = 1.5f;

    private WebCamTexture webCamTexture;
    private bool locationServiceInitialized = false;

    void Start()
    {
        if (Application.isMobilePlatform)
        {
            GameObject cameraParent = new GameObject("camParent");
            cameraParent.transform.position = this.transform.position;
            this.transform.parent = cameraParent.transform;
            cameraParent.transform.Rotate(Vector3.right, 90);

            // Start location service
            StartLocationService();
        }

        Input.gyro.enabled = true;

        addButton.onClick.AddListener(OnButtonDown);

        // Request camera permission and initialize webcam
        if (Application.isMobilePlatform)
        {
#if UNITY_IOS
            AsyncOperation permissionRequest = Application.RequestUserAuthorization(UserAuthorization.WebCam);
            permissionRequest.completed += (result) => {
                if (permissionRequest.allowSceneActivation)
                {
                    InitializeWebcam();
                }
                else
                {
                    Debug.LogError("Camera access denied on iOS.");
                }
            };
#elif UNITY_ANDROID
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                InitializeWebcam();
            }
            else
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                // Consider adding a callback for when permission is granted/denied
            }
#else
            InitializeWebcam();
#endif
        }
        else
        {
            InitializeWebcam();
        }
    }

    void StartLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.Log("Location services are disabled by the user.");
            // Optionally display a UI message to the user to enable location services
            return;
        }

        Input.location.Start();

        // Wait until the location service initializes or times out
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            System.Threading.Thread.Sleep(1000);
            maxWait--;
        }

        if (maxWait < 1)
        {
            Debug.Log("Timed out trying to get location information.");
            return;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.Log("Unable to determine device location.");
            return;
        }

        locationServiceInitialized = true;
        Debug.Log("Location service initialized.");
    }

    void InitializeWebcam()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length > 0)
        {
            int backCamIndex = -1;
            for (int i = 0; i < devices.Length; i++)
            {
                if (!devices[i].isFrontFacing)
                {
                    backCamIndex = i;
                    break;
                }
            }

            string cameraName = (backCamIndex != -1) ? devices[backCamIndex].name : devices[0].name;
            webCamTexture = new WebCamTexture(cameraName);
            webcamPlane.GetComponent<MeshRenderer>().material.mainTexture = webCamTexture;

            if (Application.isMobilePlatform)
            {
                webcamPlane.transform.localEulerAngles = new Vector3(0, 0, -webCamTexture.videoRotationAngle);

                float videoRatio = (float)webCamTexture.width / (float)webCamTexture.height;
                float planeRatio = webcamPlane.transform.localScale.x / webcamPlane.transform.localScale.y;

                if (videoRatio > planeRatio)
                {
                    webcamPlane.transform.localScale = new Vector3(planeRatio / videoRatio * webcamPlane.transform.localScale.x, webcamPlane.transform.localScale.y, 1);
                }
                else
                {
                    webcamPlane.transform.localScale = new Vector3(webcamPlane.transform.localScale.x, videoRatio / planeRatio * webcamPlane.transform.localScale.y, 1);
                }

                if (webCamTexture.videoVerticallyMirrored)
                {
                    webcamPlane.transform.localScale = new Vector3(-webcamPlane.transform.localScale.x, webcamPlane.transform.localScale.y, 1);
                }
            }

            webCamTexture.Play();
        }
        else
        {
            Debug.LogWarning("No webcams found!");
        }
    }

    void Update()
    {
        Quaternion cameraRotation = new Quaternion(Input.gyro.attitude.x, Input.gyro.attitude.y, -Input.gyro.attitude.z, -Input.gyro.attitude.w);
        this.transform.localRotation = cameraRotation;

        if (webCamTexture != null && !webCamTexture.isPlaying)
        {
            webCamTexture.Play();
        }
    }

    void OnButtonDown()
    {
        float latitude = 0f;
        float longitude = 0f;

        if (locationServiceInitialized && Input.location.status == LocationServiceStatus.Running)
        {
            latitude = Input.location.lastData.latitude;
            longitude = Input.location.lastData.longitude;
        }
        else
        {
            Debug.LogWarning("Location services not available or not initialized. Using default coordinates (0, 0).");
            // Optionally provide a fallback mechanism, like asking the user for input
        }

        // Note now faces the direction of the device (current rotation)
        Vector3 placementPosition = transform.position + transform.forward * placementDistance;
        GameObject noteInstance = Instantiate(notePrefab, placementPosition, transform.rotation);
        TextMeshPro noteText = noteInstance.GetComponentInChildren<TextMeshPro>();

        // Temporarily parent the note to the camera to get its localPosition
        noteInstance.transform.SetParent(transform, true); // Keep world position and rotation
        Vector3 localPosRelativeToCamera = noteInstance.transform.localPosition;
        noteInstance.transform.SetParent(null, true); // Unparent

        // Save the world position and world rotation of the note
        NoteData noteData = new NoteData
        {
            content = "New Note", // You might want to get actual content later
            latitude = latitude,
            longitude = longitude,
            localPosition = localPosRelativeToCamera,
            localRotation = noteInstance.transform.rotation // Store the world rotation
        };

        noteText.text = noteData.localPosition + "+" + noteData.localRotation + "\nLat: " + latitude + ", Lon: " + longitude;
    }

    void OnDisable()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Stop();
        }
        if (Input.location.status == LocationServiceStatus.Running)
        {
            Input.location.Stop();
        }
    }
}