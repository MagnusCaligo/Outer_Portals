using HarmonyLib;
using Microsoft.SqlServer.Server;
using NewHorizons.Components;
using NewHorizons.Handlers;
using NewHorizons.Utility;
using NewHorizons.Utility.OWML;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace OuterPortals.src
{
    // see https://github.com/TerrificTrifid/ow-nh-quasar-project/blob/main/QuasarProject/PortalController.cs as well
    [HarmonyPatch]
    public class PortalController : MonoBehaviour
    {

        public Camera camera;
        public GameObject renderPlane;
        public GameObject playerHelmetBox;
        public GameObject extraPlanesActivationVolume;
        public PortalController linkedPortal;
        public bool linkedToSelf = false;
        public String sectorName;
        public GameObject teleportationPlane;
        public GameObject occupantVolume;
        public GameObject PortalSectorDetector;
        public float teleportationPlaneOffset = 0.0f;

        // Adjustable Configs for individual portals
        public int portalMaximumRecursion  = 3;     // By default, don't render portals in portals
        public int portalMaxRenderDistance = 50;    // How close the camera (player or another portal) has to be before this portal renders
        public int portalFarClipPlane      = 1000;  // How far back should the camera render
        public bool doScaling              = false; // Whether to scale occupants as they travel through
        public Material maxRecursionBackupMaterial;

        private static readonly List<Camera> cameras = new List<Camera>();
        private static List<PortalController> portalControllers = new List<PortalController>();
        private bool lastVisibility = false;
        private VisibilityObject visibilityObject;
        private bool doTransformations = true;
        private bool shouldEnableHelmet = false;
        private List<OWRigidbody> teleportationOccupants;
        private SectorDetector sectorDetector;
        private OWCamera owCamera;
        private bool skipTeleportOneFrame = false;

        // Corners for calculating clipping
        private List<Vector3> corners;
        private static Camera playerCamera;
        
        private static readonly Quaternion halfTurn = Quaternion.Euler(0.0f, 180.0f, 0.0f);

        private Material cameraMaterial = new Material(OuterPortals.portalShader);
        private RenderTexture cameraRenderTexture = new RenderTexture(Screen.width, Screen.height, 24);

        public void Start()
        {

            setupMaxRecursiveMaterial();

            var sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name == transform.parent.name);
            Locator.GetPlayerCamera().GetComponentInParent<PlanetaryFogImageEffect>().enabled = false;

            // Setup Corners
            float radiusOfPortal = transform.localScale.x * (renderPlane.transform.localScale.x / 2f);
            corners = new List<Vector3>();
            Mesh quadMesh = gameObject.GetComponentInChildren<MeshFilter>().mesh;
            foreach (var vert in quadMesh.vertices)
            {
                corners.Add(Vector3.Scale(vert, gameObject.transform.localScale));
            }

            if (playerCamera == null)
                playerCamera = Locator.GetPlayerCamera().mainCamera;

            // Camera Stuff
            {
                cameras.Add(camera);
                camera.targetTexture = cameraRenderTexture;
                cameraMaterial.mainTexture = cameraRenderTexture;
                owCamera = gameObject.GetComponentsInChildren<OWCamera>()[0];
                //owCamera.gameObject.AddComponent<PlanetaryFogImageEffect>().fogShader = Shader.Find("Hidden/PlanetaryFogImageEffect");
                //owCamera.gameObject.GetAddComponent<PostProcessingBehaviour>();
                //owCamera._postProcessingSettings = Locator.GetPlayerCamera()._postProcessingSettings;
                owCamera._postProcessingSettings = Locator.GetPlayerCamera().postProcessingSettings;

                camera.farClipPlane = portalFarClipPlane;
                var playerOWCam = Locator.GetPlayerBody().GetComponentInChildren<OWCamera>();
                if (!playerOWCam.onThisPreCull.GetCallbacks().Contains(checkVisibilityOfPortalsFromPlayerCamera))
                {
                    playerOWCam.onThisPreCull.AddListener(checkVisibilityOfPortalsFromPlayerCamera);
                }
                var probeOWCam = Locator.GetProbe().GetComponentInChildren<OWCamera>();
                if (!probeOWCam.onThisPreCull.GetCallbacks().Contains(checkVisibilityOfPortalsFromPlayerCamera))
                {
                    probeOWCam.onThisPreCull.AddListener(checkVisibilityOfPortalsFromPlayerCamera);
                }

                camera.enabled = false;
                owCamera.enabled = false;
            }

            renderPlane.GetComponent<MeshRenderer>().sharedMaterial = cameraMaterial;
            foreach (var planeMesh in playerHelmetBox.GetComponentsInChildren<MeshRenderer>())
                planeMesh.sharedMaterial = cameraMaterial;

            visibilityObject = renderPlane.GetAddComponent<VisibilityObject>();
            teleportationOccupants = new List<OWRigidbody>();

            if (sectorDetector == null) {
                sectorDetector = PortalSectorDetector.GetComponent<SectorDetector>();
            }

            var triggerVolume = occupantVolume.GetComponent<OWTriggerVolume>();
            if (triggerVolume != null)
            {
                triggerVolume.OnEntry += onEntryTeleporationPlane;
                triggerVolume.OnExit += onLeaveTeleportationPlane;
            }

            var activationVolume = extraPlanesActivationVolume.GetComponent<OWTriggerVolume>();
            if (activationVolume != null)
            {
                triggerVolume.OnEntry += onEntryExtraPlanes;
                triggerVolume.OnExit += onExitExtraPlanes;
            }

            // Start invisible
            OnInvisible();
        }

        public void OnEnable()
        {
            portalControllers.Add(this);  // Keep a list of portals so we can iterate when recursive rendering
        }

        public void OnDisable()
        {
            portalControllers.Remove(this);  // Keep a list of portals so we can iterate when recursive rendering
        }

        public void onEntryTeleporationPlane(GameObject obj)
        {
            OWCollider component = obj.GetComponent<OWCollider>();
            if (component.CompareTag("PlayerDetector") || component.CompareTag("ProbeDetector"))
            {
                var body = obj.GetComponentInParent<OWRigidbody>();
                teleportationOccupants.Add(body);
            }
        }

        public VisibilityObject GetVisibilityObject()
        {
            return visibilityObject;
        }

        public void onLeaveTeleportationPlane(GameObject obj)
        {
            OWCollider component = obj.GetComponent<OWCollider>();
            if (component.CompareTag("PlayerDetector") || component.CompareTag("ProbeDetector"))
            {
                var body = obj.GetComponentInParent<OWRigidbody>();
                if (teleportationOccupants.Contains(body))
                    teleportationOccupants.Remove(body);
            }
        }

        public void onEntryExtraPlanes(GameObject obj)
        {
            if (obj.CompareTag("PlayerDetector"))
            {
                shouldEnableHelmet = true;
            }
        }

        public void onExitExtraPlanes(GameObject obj)
        {
            if (obj.CompareTag("PlayerDetector"))
            {
                shouldEnableHelmet = false;
            }

        }

        public void UpdateTeleportOccupants()
        {

            if (skipTeleportOneFrame)
            {
                skipTeleportOneFrame = false;
                return;
            }

            // iterate backwards since we remove
            for (var i = teleportationOccupants.Count - 1; i >= 0; i--)
            {

                var occupant = teleportationOccupants[i];
                var direction = Vector3.zero;

                var pos = occupant.GetPosition();

                // Logic for handling occupants traveling through the volume. 
                // Might need to adjust to use game delta time if running into teleportation issues.
                Plane tpPlane = new Plane(teleportationPlane.transform.up, teleportationPlane.transform.position);
                float distance = tpPlane.GetDistanceToPoint(pos);

                // If the occupant won't pass the plane, ignore it
                if (distance > 0f) continue;

                direction = occupant.transform.GetAttachedOWRigidbody().GetVelocity() - transform.GetAttachedOWRigidbody().GetVelocity();

                //if (Vector3.Dot(teleportationPlane.transform.up, direction) < 0f)
                {
                    Quaternion rotationDifference;
                    Transform linkedPortalTransform;
                    if (linkedToSelf || linkedPortal == null)
                    {
                        linkedPortalTransform = transform;
                    }
                    else
                    {
                        rotationDifference = transform.rotation * Quaternion.Inverse(linkedPortal.transform.rotation) * Quaternion.AngleAxis(180, linkedPortal.transform.up);
                        linkedPortalTransform = linkedPortal.transform;
                    }
                    var oldPos = occupant.GetPosition();
                    var relPos = transform.ToRelPos(oldPos);
                    var relRot = transform.ToRelRot(occupant.GetRotation());

                    var relVel = Vector3.zero;
                    relVel = transform.ToRelVel(occupant.GetVelocity(), oldPos);

                    var relAngVel = transform.ToRelAngVel(occupant.GetAngularVelocity());
                    var newPos = linkedPortalTransform.FromRelPos(halfTurn * relPos);
                    occupant.SetPosition(newPos);
                    occupant.SetRotation(linkedPortalTransform.FromRelRot(halfTurn * relRot));

                    occupant.SetVelocity(linkedPortalTransform.FromRelVel(halfTurn * relVel, newPos));
                    occupant.SetAngularVelocity(linkedPortalTransform.FromRelAngVel(halfTurn * relAngVel));

                    if (!Physics.autoSyncTransforms) Physics.SyncTransforms(); // or else "Player grounded spherecast" complains
                    
                    if (linkedToSelf)
                    {
                        teleportationOccupants.RemoveAt(i);
                    }

                    if (occupant.CompareTag("Player"))
                    {
                        var fa = Locator.GetPlayerBody().GetComponent<ForceApplier>();
                        if (fa != null)
                            fa.SkipNextFrame();
                        Locator.GetPlayerBody().GetComponent<AlignPlayerWithForce>().SkipNextFrame();
                        linkedPortal.skipTeleportOneFrame = true;
                    }
                    if (doScaling)
                    {
                        Vector3 scaleChange = occupant.transform.localScale - (transform.localScale - linkedPortalTransform.localScale);
                        occupant.transform.localScale = scaleChange;
                    }
                }
            }
        }

        // Made based off of this forum post: https://discussions.unity.com/t/how-do-i-render-only-a-part-of-the-cameras-view/23686/2
        public void SetScissorRect(Camera cam, Rect r)
        {
            //Matrix4x4 m = Locator.GetPlayerCamera().mainCamera.projectionMatrix;
            cam.ResetProjectionMatrix();
            Matrix4x4 m = cam.projectionMatrix;
            cam.rect = r;
            cam.aspect = playerCamera.aspect; // does this need to be set here?
            
            Matrix4x4 m2 = Matrix4x4.TRS(new Vector3((1 / r.width - 1), (1 / r.height - 1), 0), Quaternion.identity, new Vector3(1 / r.width, 1 / r.height, 1));
            Matrix4x4 m3 = Matrix4x4.TRS(new Vector3(-r.x * 2 / r.width, -r.y * 2 / r.height, 0), Quaternion.identity, Vector3.one);
            cam.projectionMatrix = m3 * m2 * m;

        }

        // Written based off of this article: https://www.turiyaware.com/a-solution-to-unitys-camera-worldtoscreenpoint-causing-ui-elements-to-display-when-object-is-behind-the-camera/
        public void CalculateViewportRect()
        {
            return; // TEMP: setting rect lags the game a ton
            
            List<Vector3> points_on_screen = new List<Vector3>();
            corners.ForEach(x => points_on_screen.Add(playerCamera.WorldToScreenPoint(transform.TransformPoint(x))));

            Vector3 initialPoint = points_on_screen.OrderBy(x => x.z).Last();

            float xMin = initialPoint.x;
            float xMax = xMin;
            float yMin = initialPoint.y;
            float yMax = yMin;

            points_on_screen.ForEach(point =>
            {
                // if behind the camera, they coords get flipped around, so fix that here
                // it doesnt work all the time, but it does most of the time
                if (point.z <= 0)
                {
                    if (point.x <= Screen.width / 2f)
                        point.x = Screen.width;
                    else if (point.x > Screen.width / 2f)
                        point.x = 0;
                    if (point.y <= Screen.height / 2f)
                        point.y = Screen.height;
                    else if (point.y > Screen.height / 2f)
                        point.y = 0;
                }

                if (point.x < xMin)
                    xMin = point.x;
                if (point.x > xMax)
                    xMax = point.x;
                if (point.y < yMin)
                    yMin = point.y;
                if (point.y > yMax)
                    yMax = point.y;
            });

            xMin = Mathf.Clamp(xMin / Screen.width, 0, 1);
            xMax = Mathf.Clamp(xMax / Screen.width, 0, 1);
            yMin = Mathf.Clamp(yMin / Screen.height, 0, 1);
            yMax = Mathf.Clamp(yMax / Screen.height, 0, 1);

            Rect rect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);

            SetScissorRect(camera, rect);
        }

        public void FixedUpdate()
        {
            UpdateTeleportOccupants();
            doMovePlayerHelmetBox();
        }

        public void calculateRelativeCamera(Vector3 camera_position, Quaternion camera_rotation, Transform portal, out Vector3 out_camera_position, out Quaternion out_camera_rotation)
        {
            // Calculates where the camera should be on the otherside of the portal

            // apply transformation based on player camera
            {
                var relPos = transform.ToRelPos(camera_position);
                var relRot = transform.ToRelRot(camera_rotation);

                out_camera_position = portal.FromRelPos(halfTurn * relPos);
                out_camera_rotation = portal.FromRelRot(halfTurn * relRot);
            }
        }

        public void doMovePlayerHelmetBox()
        {
            var activationVolume = extraPlanesActivationVolume.GetComponent<OWTriggerVolume>();
            shouldEnableHelmet =  activationVolume.IsTrackingObject(Locator.GetPlayerDetector());
            // Move player helmet box
            var helmetPos = Vector3.ProjectOnPlane(playerCamera.transform.position, transform.forward);
            playerHelmetBox.transform.position = helmetPos + Vector3.Dot(transform.position, transform.forward)*transform.forward;
        }

        public void doMoveCameraRelativeToPosition(Vector3 camera_position, Quaternion camera_rotation, PortalController output_portal)
        {
            // Adjust teleportation plane depending on direction player is facing
            // If the player is facing backwards, we need to move the teleportation back a little bit to prevent the camera from clipping through the portal
            float adjustment = (Vector3.Dot(camera_rotation * Vector3.up, transform.up) + 1f) / 2f;
            teleportationPlane.transform.SetLocalPositionZ(teleportationPlaneOffset);

            Vector3 new_position;
            Quaternion new_rotation;
            calculateRelativeCamera(camera_position, camera_rotation, output_portal.transform, out new_position, out new_rotation);
            camera.transform.position = new_position;
            camera.transform.rotation = new_rotation;

            calculateClipPlane(output_portal);
        }

        public void calculateClipPlane(PortalController output_portal)
        {
            // Calculate clip distance to maximize camera through portal while minimizing rendering stuff between camera and portal
            Plane clip = new Plane(camera.transform.forward, camera.transform.position);

            // Find Closest Corner
            Vector3 closestCorner = output_portal.corners.OrderBy(x => clip.GetDistanceToPoint(output_portal.renderPlane.transform.TransformPoint(x))).First();
            closestCorner = output_portal.renderPlane.transform.TransformPoint(closestCorner);

            // Calculate distance between camera and plane
            float closestDistance = clip.GetDistanceToPoint(closestCorner);
            closestDistance = closestDistance < 0.1f ? 0.1f : closestDistance;

            camera.nearClipPlane = closestDistance;
        }

        public void checkVisibilityOfOtherPortals(int ttl)
        {
            if (ttl <= 0)
            {
                return;
            }
            if (linkedToSelf)
                return;
            foreach (PortalController pc in portalControllers)
            {
                if (pc.enabled == false ) continue;
                var distance = (linkedPortal.transform.position - pc.transform.position).magnitude;
                if (distance > pc.portalMaxRenderDistance)
                    continue;
                var visible = pc.GetVisibilityObject().CheckVisibilityFromProbe(owCamera);
                if (visible && Vector3.Dot(linkedPortal.transform.position - pc.transform.position, pc.transform.forward) <= 0f)
                {
                    PortalController linkedPortal = pc.linkedPortal;
                    if (pc.linkedPortal == null)
                        linkedPortal = pc;

                    Vector3 camera_location = Vector3.zero;
                    Quaternion camera_rotation = Quaternion.identity;
                    Vector3 oldCameraLocation = pc.camera.transform.position;
                    Quaternion oldCameraRotation = pc.camera.transform.rotation;
                    pc.OnVisible();
                    pc.doMoveCameraRelativeToPosition(camera.transform.position, camera.transform.rotation, linkedPortal);

                    // Only render recursive portals if the portal is configured for it
                    if (pc.portalMaximumRecursion >= ttl)
                        pc.checkVisibilityOfOtherPortals(ttl - 1);
                    if( ttl -1 <= 0 )
                        renderPlane.GetComponent<MeshRenderer>().sharedMaterial = maxRecursionBackupMaterial;


                    pc.calculateClipPlane(linkedPortal);
                    pc.owCamera.Render();
                    pc.camera.transform.position = oldCameraLocation;
                    pc.camera.transform.rotation = oldCameraRotation;
                    renderPlane.GetComponent<MeshRenderer>().sharedMaterial = cameraMaterial;

                    continue;
                }
            }

        }

        public static void checkVisibilityOfPortalsFromPlayerCamera(OWCamera cam)
        {
            // Need to render in two different passes. The first past to render all the portals visible to the portal, the second pass to render portals visible to the player.
            foreach (PortalController pc in portalControllers)
            {
                if (!pc.enabled) continue;
                var distance = (cam.transform.position - pc.transform.position).magnitude; 
                if (distance > pc.portalMaxRenderDistance) continue;
                if (pc.GetVisibilityObject().IsVisible() && Vector3.Dot(cam.transform.position - (pc.transform.position), pc.transform.forward) < 0.1f)
                {
                    Transform output_portal_transform;
                    PortalController controller;
                    if (pc.linkedToSelf || pc.linkedPortal == null)
                    {
                        output_portal_transform = pc.transform;
                        controller = pc;
                    }
                    else
                    {
                        output_portal_transform = pc.linkedPortal.transform;
                        controller = pc.linkedPortal;
                    }

                    pc.OnVisible();
                    pc.doMoveCameraRelativeToPosition(cam.transform.position, cam.transform.rotation, controller);
                    pc.playerHelmetBox.SetActive(false);
                    pc.checkVisibilityOfOtherPortals(pc.portalMaximumRecursion);
                    if (pc.linkedPortal != null)
                        pc.calculateClipPlane(pc.linkedPortal);
                    pc.renderPlane.GetComponent<MeshRenderer>().sharedMaterial = pc.cameraMaterial;
                    continue;
                }
            }
            foreach (PortalController pc in portalControllers)
            {
                var distance = (cam.transform.position - pc.transform.position).magnitude;
                if (!pc.enabled || distance > pc.portalMaxRenderDistance)
                {
                    pc.OnInvisible();
                    continue;
                }
                // Enable "helmet" if the player is looking at the portal
                if (Vector3.Dot(playerCamera.transform.forward, pc.transform.forward) > -0.5) // Have to increase it a bit, in case they enter from an angle
                    pc.playerHelmetBox.SetActive(pc.shouldEnableHelmet);
                // Make sure the player is in front of the portal. We compare to 1 here as we give 1 meter wiggle room in case the player is behind the portal in the helmet.
                // The difference in position between the pc and the camera is NOT normalized, so the dot product can be greater and less than 1 and -1
                if (pc.GetVisibilityObject().IsVisible() && Vector3.Dot(cam.transform.position - pc.transform.position, pc.transform.forward) < 1f)
                {
                    Transform output_portal_transform;
                    PortalController controller;
                    if (pc.linkedToSelf || pc.linkedPortal == null)
                    {
                        output_portal_transform = pc.transform;
                        controller = pc;
                    }
                    else
                    {
                        output_portal_transform = pc.linkedPortal.transform;
                        controller = pc.linkedPortal;
                    }
                    pc.OnVisible();
                    pc.doMoveCameraRelativeToPosition(cam.transform.position, cam.transform.rotation, controller);
                    if (pc.linkedPortal != null)
                        pc.calculateClipPlane(pc.linkedPortal);
                    pc.owCamera.Render();
                    continue;
                }
                pc.OnInvisible();
            }
        }

        public void setupMaxRecursiveMaterial()
        {
            // Replace shaders. Borrowed from NewHorizons: https://github.com/Outer-Wilds-New-Horizons/new-horizons/blob/main/NewHorizons/Utility/Files/AssetBundleUtilities.cs#L139-L157
            if (maxRecursionBackupMaterial == null)
            {
                maxRecursionBackupMaterial = new Material(Shader.Find("Standard"));
                maxRecursionBackupMaterial.color = Color.black;
            }
            else
            {
                var replacementShader = Shader.Find(maxRecursionBackupMaterial.shader.name);
                if (replacementShader != null)
                {
                        // preserve override tag and render queue (for Standard shader)
                        // keywords and properties are already preserved
                        if (maxRecursionBackupMaterial.renderQueue != maxRecursionBackupMaterial.shader.renderQueue)
                        {
                            var renderType = maxRecursionBackupMaterial.GetTag("RenderType", false);
                            var renderQueue = maxRecursionBackupMaterial.renderQueue;
                            maxRecursionBackupMaterial.shader = replacementShader;
                            maxRecursionBackupMaterial.SetOverrideTag("RenderType", renderType);
                            maxRecursionBackupMaterial.renderQueue = renderQueue;
                        }
                        else
                        {
                            maxRecursionBackupMaterial.shader = replacementShader;
                        }
                }
            }

        }

        public void OnVisible()
        {
            if (lastVisibility)
                return;
            lastVisibility = true;
            doTransformations = true;

            if (linkedPortal == null)
                return;
            
            var linkedPortalSectorName = linkedPortal.sectorName;
            if (linkedPortalSectorName == null)
                return;
            var sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name == linkedPortalSectorName);
            AstroObject astroObject = null;

            // Find the astro object of the sector
            while (astroObject == null && sector != null)
            {
                // some of SectorStreaming is distance based, so have to do it ourselves
                astroObject = sector.GetComponentInParent<AstroObject>();
                if (astroObject == null)
                {
                    sector = sector.GetParentSector();
                    continue;
                }
                var streamingGroup = StreamingHandler.GetStreamingGroup(astroObject.GetAstroObjectName());
                if (streamingGroup != null)
                {
                    streamingGroup.RequestRequiredAssets();
                    break;
                }
            }

            sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name == linkedPortalSectorName);

            // the same strategy NomaiRemoteCameraPlatform uses
            while (sector != null)
            {
                sector.AddOccupant(sectorDetector);
                sector = sector.GetParentSector();
            }
        }
        
        public void OnInvisible()
        {
            if (lastVisibility == false)
                return;
            lastVisibility = false;

            doTransformations = false;
            shouldEnableHelmet = false;
            playerHelmetBox.SetActive(false);

            if (linkedPortal == null)
                return;
            
            var linkedPortalSectorName = linkedPortal.sectorName;
            if (linkedPortalSectorName == null)
                return;
            var sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name == linkedPortalSectorName);
            AstroObject astroObject = null;

            // Find the astro object of the sector
            while (astroObject == null && sector != null)
            {
                // some of SectorStreaming is distance based, so have to do it ourselves
                astroObject = sector.GetComponentInParent<AstroObject>();
                if (astroObject == null)
                {
                    sector = sector.GetParentSector();
                    continue;
                }
                var streamingGroup = StreamingHandler.GetStreamingGroup(astroObject.GetAstroObjectName());
                if (streamingGroup != null)
                {
                    streamingGroup.ReleaseRequiredAssets();
                    break;
                }
            }

            sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name == linkedPortalSectorName);

            while (sector != null)
            {
                sector.RemoveOccupant(sectorDetector);
                sector = sector.GetParentSector();
            }
        }

        public void OnDestroy()
        {
            cameras.Remove(camera);
            if (portalControllers.Contains(this))
                portalControllers.Remove(this);
            DestroyImmediate(cameraRenderTexture);
            DestroyImmediate(cameraMaterial);
            OnInvisible(); // to deallocate
        }

        public void linkPortal(String portalName)
        {
            GameObject portal = GameObject.Find(portalName);

            if (portal == null)
            {
                NHLogger.Log($"Failed to link portal {name} to {portalName}");
                return;
            }
            this.linkedPortal = portal.GetComponentInChildren<PortalController>();
            this.linkedToSelf = false;
        }

        // TODO: this can probably be moved into Update
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