using OWML.ModHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using UnityEngine.Windows.WebCam;

namespace First_Test_Mod.src
{
    [HarmonyPatch]
    internal class PortalController : MonoBehaviour
    {

        public Camera camera;
        public GameObject renderPlane;
        private Material cameraMaterial;
        public PortalController linkedPortal;
        public bool linkedToSelf = false;

        private Renderer portalRenderer;

        public static OWCamera playerCamera;
        private static Shader portalShader;
        private static List<Camera> cameras = new List<Camera>();

        public void Awake()
        {
            portalRenderer = GetComponent<Renderer>();
            portalShader = First_Test_Mod.Instance.shaderBundle.LoadAsset<Shader>("Assets/Custom Prefabs/PortalShader.shader");
        }

        public void Start()
        {
            Debug.Log("In start function of portal");
            if (playerCamera == null)
                playerCamera = Locator.GetActiveCamera();

            if (camera == null)
                camera = gameObject.AddComponent<Camera>();
            cameras.Add(camera);

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

            Vector3 newPos;
            Quaternion newRot;
            Vector3 playerInLocal;
            if (linkedToSelf)
            {
                playerInLocal = transform.InverseTransformPoint(playerCamera.transform.position);
                newPos = new Vector3(-playerInLocal.x, playerInLocal.y, -playerInLocal.z);
                newPos = transform.TransformPoint(newPos);
            }
            else
            {
                //playerInLocal = linkedPortal.transform.InverseTransformPoint(playerCamera.transform.position);
                playerInLocal = transform.InverseTransformPoint(playerCamera.transform.position);
                if (linkedPortal == null)
                    return;
                playerInLocal = new Vector3(-playerInLocal.x, playerInLocal.y, -playerInLocal.z);
                newPos = linkedPortal.transform.TransformPoint(playerInLocal);
            }

            Vector3 outputPortalUp;
            if (linkedToSelf)
            {
                outputPortalUp = transform.rotation * Vector3.up;
                Quaternion yawDifference;
                yawDifference = Quaternion.AngleAxis(180, outputPortalUp);
                Vector3 forwardCameraDirection = yawDifference * (playerCamera.transform.forward);
                newRot = Quaternion.LookRotation(forwardCameraDirection, outputPortalUp);
            }
            else
            {
                if (linkedPortal == null)
                    return;
                outputPortalUp = linkedPortal.transform.rotation * Vector3.up;
                Vector3 difference = playerCamera.transform.forward - transform.forward;
                newRot = Quaternion.LookRotation(-linkedPortal.transform.forward, outputPortalUp) * transform.InverseTransformRotation(playerCamera.transform.rotation);

            }

            camera.transform.position = newPos;
            camera.transform.rotation = newRot;
        }

        public void OnDestroy()
        {
            cameras.Remove(camera);
        }

        public static void linkPortals(PortalLinks links)
        {
            foreach (KeyValuePair<string, string> link in links.links)
            {
                GameObject entrance_portal = GameObject.Find(link.Key);
                GameObject exit_portal = GameObject.Find(link.Value);
                if (entrance_portal == null)
                {
                    First_Test_Mod.Instance.ModHelper.Console.WriteLine($"Error: Failed to find portal with name {link.Key}");
                    continue;
                }
                if (exit_portal == null)
                {
                    First_Test_Mod.Instance.ModHelper.Console.WriteLine($"Error: Failed to find portal with name {link.Value}");
                    continue;
                }
                PortalController entr_portal_controller = entrance_portal.GetComponent<PortalController>();
                PortalController exit_portal_controller = exit_portal.GetComponent<PortalController> ();
                if (entr_portal_controller == null)
                {
                    First_Test_Mod.Instance.ModHelper.Console.WriteLine($"Entrance portal with name {link.Key} does not have a portal controller.");
                    continue;
                }
                if (exit_portal_controller == null)
                {
                    First_Test_Mod.Instance.ModHelper.Console.WriteLine($"Exit portal with name {link.Value} does not have a portal controller.");
                    continue;
                }
                First_Test_Mod.Instance.ModHelper.Console.WriteLine($"Linking {link.Key} to {link.Value}");
                entr_portal_controller.linkedPortal = exit_portal_controller;
                entr_portal_controller.linkedToSelf = false;

            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerCameraController), nameof(PlayerCameraController.UpdateFieldOfView))]
        public static void matchFieldOfView()
        {
            if (playerCamera == null)
                return;
            foreach (Camera cam in PortalController.cameras)
                cam.fieldOfView = playerCamera.fieldOfView;
        }

    }
}