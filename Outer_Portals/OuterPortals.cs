using OuterPortals.src;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using NewHorizons;
using NewHorizons.Utility.OuterWilds;
using NewHorizons.Utility.OWML;
using System.Runtime.CompilerServices;

namespace OuterPortals;
[HarmonyPatch]
public class OuterPortals : ModBehaviour
{
    public static OuterPortals Instance;
    public INewHorizons NewHorizons;

    public static Shader portalShader;

    public void Awake()
    {
        Instance = this;
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        // You won't be able to access OWML's mod helper in Awake.
        // So you probably don't want to do anything here.
        // Use Start() instead.
    }

    public void Start()
    {
        // Starting here, you'll have access to OWML's mod helper.
        ModHelper.Console.WriteLine($"My mod {nameof(OuterPortals)} is loaded!", MessageType.Success);

        // Get the New Horizons API and load configs
        NewHorizons = ModHelper.Interaction.TryGetModApi<INewHorizons>("xen.NewHorizons");
        NewHorizons.LoadConfigs(this);

        new Harmony("Mags.OuterPortals").PatchAll(Assembly.GetExecutingAssembly());

        {
            var shaderBundle = ModHelper.Assets.LoadBundle("assets/portal/portal_shaders");
            portalShader = shaderBundle.LoadAsset<Shader>("Assets/Custom Prefabs/PortalShader.shader");
            if (portalShader == null)
            {
                Debug.LogError("Shader not found! Setting to empty one.");
                portalShader = Shader.Find("Unlit/Color");
            }
        }

        // Example of accessing game code.
        OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen); // We start on title screen
        LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;

        var api = ModHelper.Interaction.TryGetModApi<INewHorizons>("xen.NewHorizons");
        api.GetStarSystemLoadedEvent().AddListener((name) =>
            {
                ModHelper.Console.WriteLine($"Body: {name} Loaded!");
                PortalConfigs data = (PortalConfigs) api.QuerySystem(typeof(PortalConfigs), "$.extras.PortalConfigs");
                if (data != null) {
                    ModHelper.Console.WriteLine("Found Portal Link Data");

                    foreach (var portalConfig in data.Portals)
                    {

                        GameObject portal = GameObject.Find(portalConfig.name);
                        if (portal == null)
                        {
                            NHLogger.Log($"Failed to find portal {portalConfig.name}");
                            continue;
                        }
                        PortalController pc = portal.GetComponentInChildren<PortalController>();
                        if (portalConfig.linkedPortal != null)
                        {
                            NHLogger.Log($"Linking portal: {portalConfig.linkedPortal}");
                            pc.linkPortal(portalConfig.linkedPortal);
                        }
                        pc.sectorName = portalConfig.sector;
                        pc.portalMaximumRecursion = portalConfig.portalMaxRecursion;
                        pc.portalMaxRenderDistance = portalConfig.portalMaxRenderDistance;
                        pc.portalFarClipPlane = portalConfig.portalFarClipPlane;
                    }
                }
                else
                    ModHelper.Console.WriteLine("No Portal Links found!");
            });
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerResources), nameof(PlayerResources.UpdateFuel))]
    public static bool fuelOverride(PlayerResources __instance)
    {
        __instance._currentFuel = PlayerResources._maxFuel;
        return false;

    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ProbeCamera), nameof(ProbeCamera.TakeSnapshot))]
    public static bool probePreRender(ProbeCamera __instance)
    {
        PortalController.checkVisibilityOfPortalsFromPlayerCamera(__instance._camera);
        return true;
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(ProbeLauncher), nameof(ProbeLauncher.LaunchProbe))]
    public static void onLaunchProbe()
    {

        // Set Probe layer so its visible in the portals
        var probe = Locator.GetProbe().transform.Find("CameraPivot/Geometry/Props_HEA_Probe_ANIM/Props_HEA_Probe");
        if (probe != null)
            probe.gameObject.layer = Layer.Default;
    }


    public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
    {
        if (newScene != OWScene.SolarSystem) return;
        ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);

        GlobalMessenger.AddListener("WakeUp", () => { StartCoroutine(helper_function()); });
    }

    public IEnumerator helper_function()
    {
        OuterPortals.Instance.ModHelper.Console.WriteLine("Calling Helper function");
        yield return new WaitForSeconds(3);
        movement_unlocked();
    }

    public static void movement_unlocked()
    {
        OuterPortals.Instance.ModHelper.Console.WriteLine("Should be putting on suit");
        Locator.GetPlayerSuit().SuitUp(false, false, true);   
    }
}

