using OWML.ModHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace First_Test_Mod.src
{
    internal class PortalController : MonoBehaviour
    {

        public Camera camera;
        public GameObject renderPlane;
        private Material cameraMaterial;
        public PortalController linkedPortal;
        public bool linkedToSelf = false;

        private Renderer portalRenderer;

        public OWCamera playerCamera;
        private static Shader portalShader;

        public void Awake()
        {
            portalRenderer = GetComponent<Renderer>();
            portalShader = First_Test_Mod.Instance.shaderBundle.LoadAsset<Shader>("Assets/Custom Prefabs/PortalShader.shader");
        }

        public void Start()
        {
            Debug.Log("In start function of portal");
            playerCamera = Locator.GetActiveCamera();

            if (camera == null)
                camera = gameObject.AddComponent<Camera>();

            Debug.Log("Creating Material");
            if (portalShader == null)
            {
                Debug.LogError("Shader not found! Setting to empty one.");
                portalShader = Shader.Find("Unlit/Color");
            }
            cameraMaterial = new Material(portalShader);

            if (camera.targetTexture != null)
                camera.targetTexture.Release();
            Debug.Log("Creating Render Texture");
            camera.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
            Debug.Log("Setting target texture");
            cameraMaterial.mainTexture = camera.targetTexture;
            Debug.Log("Checking texture set: " + (camera.targetTexture != null));

            renderPlane.GetComponent<MeshRenderer>().material = cameraMaterial;

        }

        public void Update()
        {
            Vector3 playerInLocal = transform.InverseTransformPoint(playerCamera.transform.position);
            Vector3 newPos = new Vector3(-playerInLocal.x, playerInLocal.y, -playerInLocal.z);
            newPos = transform.TransformPoint(newPos);

            Vector3 parentObjectUp = transform.rotation * Vector3.up;
            Quaternion yawDifference = new Quaternion();
            if (linkedToSelf)
            {
                yawDifference = Quaternion.AngleAxis(180, parentObjectUp);
            }
            Vector3 forwardCameraDirection = yawDifference * (playerCamera.transform.forward);

            camera.transform.position = newPos;
            camera.transform.rotation = Quaternion.LookRotation(forwardCameraDirection, parentObjectUp);
        }

    }
}