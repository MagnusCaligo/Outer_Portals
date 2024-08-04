using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace First_Test_Mod;
[HarmonyPatch]
public class First_Test_Mod : ModBehaviour
{
	public static First_Test_Mod Instance;
public INewHorizons NewHorizons;

    private AssetBundle bundle;

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

    // Example of accessing game code.
    OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen); // We start on title screen
    //bundle = ModHelper.Assets.LoadBundle("assets/magsbundle");
    LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;

}

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GhostData), nameof(GhostData.FixedUpdate_Data))]
    public static bool GhostData_FixedUpdate_Data_Prefix(GhostData __instance)
    {
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Campfire), nameof(Campfire.OnPressInteract))]
    public static void Campfire_OnPressInteract_Prefix(Campfire __instance)
    {
        First_Test_Mod.Instance.ModHelper.Console.WriteLine($"Sector is {__instance.GetSector()}");
        First_Test_Mod.Instance.ModHelper.Console.WriteLine($"Sector is {__instance.GetType()}");

    }


public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
    {
        if (newScene != OWScene.SolarSystem) return;
        ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);

        GlobalMessenger.AddListener("WakeUp", () => { StartCoroutine("helper_function"); });

        /*
        var prefab = bundle.LoadAsset("Assets/Custom Prefabs/Mirror.prefab");
        GameObject.Instantiate(prefab);
        prefab = bundle.LoadAsset("Assets/Custom Prefabs/BasicTestObject.prefab");
        GameObject.Instantiate(prefab);
        */

    }

    public IEnumerator helper_function()
    {
        First_Test_Mod.Instance.ModHelper.Console.WriteLine("Calling Helper function");
        yield return new WaitForSeconds(3);
        movement_unlocked();
        
    }

   // [HarmonyPostfix]
    //[HarmonyPatch(typeof(PlayerCharacterController), nameof(PlayerCharacterController.OnPlayerResurrection))]
    public static void movement_unlocked()
    {

        First_Test_Mod.Instance.ModHelper.Console.WriteLine("Should be putting on suit");
        DreamLanternItem newLantern = new DreamLanternItem();
        Locator.GetPlayerSuit().SuitUp(false, false, true);
        Locator.GetPlayerController().SetDreamLantern(newLantern);
   
    }
}

