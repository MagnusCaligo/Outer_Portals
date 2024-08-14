using First_Test_Mod.src;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using NewHorizons;

namespace First_Test_Mod;
[HarmonyPatch]
public class First_Test_Mod : ModBehaviour
{
    public static First_Test_Mod Instance;
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
        ModHelper.Console.WriteLine($"My mod {nameof(First_Test_Mod)} is loaded!", MessageType.Success);

        // Get the New Horizons API and load configs
        NewHorizons = ModHelper.Interaction.TryGetModApi<INewHorizons>("xen.NewHorizons");
        NewHorizons.LoadConfigs(this);

        new Harmony("Mags.First Test Mod").PatchAll(Assembly.GetExecutingAssembly());

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
                var data = api.QuerySystem<PortalLinks>("$.extras.PortalLinks");
                if (data != null) {
                    ModHelper.Console.WriteLine("Found Portal Link Data");
                    PortalController.linkPortals(data);
                }
                else
                    ModHelper.Console.WriteLine("No Portal Links found!");
            });
    }



    public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
    {
        if (newScene != OWScene.SolarSystem) return;
        ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);

        GlobalMessenger.AddListener("WakeUp", () => { StartCoroutine(helper_function()); });
    }

    public IEnumerator helper_function()
    {
        First_Test_Mod.Instance.ModHelper.Console.WriteLine("Calling Helper function");
        yield return new WaitForSeconds(3);
        movement_unlocked();
    }


    public static void movement_unlocked()
    {
        First_Test_Mod.Instance.ModHelper.Console.WriteLine("Should be putting on suit");
        Locator.GetPlayerSuit().SuitUp(false, false, true);   
    }
}

