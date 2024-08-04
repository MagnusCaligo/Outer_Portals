using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace First_Test_Mod.src
{
    internal class PortalCamera : MonoBehaviour
    {

        public GameObject renderScreen;
        private OWCamera playerCamera;
        private Transform shipTransform;
        public GameObject parentObject;

        public Camera camera;
        public Material cameraMaterial;
        public void Start()
        {
            playerCamera = Locator.GetActiveCamera();
            shipTransform = Locator.GetShipTransform();

            if (camera.targetTexture != null)
                camera.targetTexture.Release();
            camera.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
            cameraMaterial.mainTexture = camera.targetTexture;
        }

        public void Update()
        {
            Vector3 playerInLocal = parentObject.transform.InverseTransformPoint(playerCamera.transform.position);
            //Vector3 playerOffsetFromPortal = -1 * (playerCamera.transform.position - parentObject.transform.position);
            //Vector3 newPos = parentObject.transform.position + playerOffsetFromPortal;
            Vector3 newPos = new Vector3(-playerInLocal.x, playerInLocal.y, -playerInLocal.z);
            newPos = parentObject.transform.TransformPoint(newPos);
            //Vector3 newPos = mirrorPoint - playerCamera.transform.position - mirrorPoint;


            Quaternion differenceBetweenPlayerAndPortal = Quaternion.Inverse(parentObject.transform.rotation) * playerCamera.transform.rotation;
            Vector3 parentObjectUp = parentObject.transform.rotation * Vector3.up;
            //Quaternion newRot = Quaternion.LookRotation(transform.localPosition, renderScreen.transform.localPosition) * Quaternion.Euler(0, 180, 0);

            //Quaternion cameraInLocal = parentObject.transform.InverseTransformRotation(playerCamera.transform.rotation);
            Quaternion cameraInLocal = Locator._timberHearth.transform.rotation * parentObject.transform.rotation;
            //Quaternion newRot = Quaternion.Inverse(cameraInLocal);

            float angularDifference = Quaternion.Angle(playerCamera.transform.rotation, parentObject.transform.rotation);

            Quaternion yawDifference = Quaternion.AngleAxis(180, parentObjectUp);
            Vector3 forwardCameraDirection = yawDifference * (playerCamera.transform.forward);

            transform.position = newPos;
            transform.rotation = playerCamera.transform.rotation * Quaternion.AngleAxis(180, Vector3.up);
            transform.rotation = Quaternion.LookRotation(forwardCameraDirection, parentObjectUp);
            //transform.localRotation = Quaternion.LookRotation(forwardCameraDirection, Vector3.up);
            //transform.rotation = differenceBetweenPlayerAndPortal;
        }

    }
}

/* This shit works!
 * 
 *             Vector3 playerOffsetFromPortal = -1 * (playerCamera.transform.position - renderScreen.transform.position);
            Vector3 newPos = renderScreen.transform.position + playerOffsetFromPortal;


            Quaternion differenceBetweenPlayerAndPortal = Quaternion.Inverse(parentObject.transform.rotation) * playerCamera.transform.rotation;
            Vector3 parentObjectUp = parentObject.transform.rotation * Vector3.up;
            //Quaternion newRot = Quaternion.LookRotation(transform.localPosition, renderScreen.transform.localPosition) * Quaternion.Euler(0, 180, 0);

            //Quaternion cameraInLocal = parentObject.transform.InverseTransformRotation(playerCamera.transform.rotation);
            Quaternion cameraInLocal = Locator._timberHearth.transform.rotation * parentObject.transform.rotation;
            //Quaternion newRot = Quaternion.Inverse(cameraInLocal);

            float angularDifference = Quaternion.Angle(playerCamera.transform.rotation, parentObject.transform.rotation);

            Quaternion yawDifference = Quaternion.AngleAxis(180, parentObjectUp);
            Vector3 forwardCameraDirection = yawDifference * (playerCamera.transform.forward);

            transform.position = newPos;
            transform.rotation = playerCamera.transform.rotation * Quaternion.AngleAxis(180, Vector3.up);
            transform.rotation = Quaternion.LookRotation(forwardCameraDirection, parentObjectUp);
            //transform.localRotation = Quaternion.LookRotation(forwardCameraDirection, Vector3.up);
            //transform.rotation = differenceBetweenPlayerAndPortal;
 * 
 */