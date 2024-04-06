using System;
using System.EnterpriseServices;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Epic.OnlineServices.UI;
using HarmonyLib;
using JetBrains.Annotations;
using Mono.Cecil;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.SceneManagement;

namespace project1;

public class project1 : ModBehaviour
{
	private void Awake()
	{
		Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
		Instance = this;
	}

	private void Start()
	{
		///	I apologize for my inexperience in coding and commenting for readability
		///	It's my first mod, don't judge too hard :]
		ModHelper.Console.WriteLine($"{nameof(project1)}: Resource management loaded!", MessageType.Success);

		// Check the scene loaded when a scene finishes loading
		LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
		{
			//	Checks if the scene is SolarSystem
		    if (loadScene == OWScene.SolarSystem)
	   		{
				//	Finds GameObjects
	   			GameObject shipBody = GameObject.Find("Ship_Body");
				if (shipBody != null) 
				{
					shipResources = shipBody.GetComponent<ShipResources>();
					landingPadManager = shipBody.GetComponent<LandingPadManager>();
				}
				
				GameObject globalManagers = GameObject.Find("GlobalManagers");
				if (globalManagers != null)
				{
					playerState = globalManagers.GetComponent<PlayerState>();
					locator = globalManagers.GetComponent<Locator>();
					if (locator != null)
					{
						GameObject shipDetector = GameObject.Find("ShipDetector");
						if (shipDetector != null)
						{
							shipOxygenDetector = shipDetector.AddComponent<OxygenDetector>();
						}
					}
				}

				GameObject gaugeSystems = GameObject.Find("GaugeSystems");
				if (gaugeSystems != null)
				{
					shipOxygenGauge = gaugeSystems.GetComponent<ShipOxygenGauge>();
					shipFuelGauge = gaugeSystems.GetComponent<ShipFuelGauge>();
				}
				
				playerDetector = GameObject.Find("PlayerDetector");
				if (playerDetector != null)
				{
					playerOxygenDetector = playerDetector.GetComponent<OxygenDetector>();
				}

				GameObject shipAtmosphereVolume = GameObject.Find("ShipAtmosphereVolume");
				if (shipAtmosphereVolume != null)
				{
					shipOxygenVolume = shipAtmosphereVolume.GetComponent<OxygenVolume>();
				}

				GameObject playerRecoveryPointObject = GameObject.Find("PlayerRecoveryPoint");
				if (playerRecoveryPointObject != null)
				{
					playerRecoveryPoint = playerRecoveryPointObject.GetComponent<PlayerRecoveryPoint>();
				}

				GameObject mainElectricitySystemObject = GameObject.Find("MainElectricalSystem");
				if (mainElectricitySystemObject != null)
				{
					mainElectricalSystem = mainElectricitySystemObject.GetComponent<ElectricalSystem>();
				}
			}  	
	    };
	}
	private void Update()
	{
		// 	Function calls
		if (shipResources != null)
		{
			Functions.ShipRefuel();
			Functions.OxygenRefill();
		}
	}

	//	Variables list & initialize objects
	public static bool IsLanded;
	public static bool IsLandedPast;
	public static project1 Instance;
	public static ShipResources shipResources;
	public static ShipFuelGauge shipFuelGauge;
	public static ShipOxygenGauge shipOxygenGauge;
	public static OxygenDetector shipOxygenDetector;
	public static OxygenDetector playerOxygenDetector;
	public static OxygenVolume shipOxygenVolume;
	public static PlayerState playerState;
	public static PlayerRecoveryPoint playerRecoveryPoint;
	public static GameObject playerDetector;
	public static ElectricalSystem mainElectricalSystem;
	public static LandingPadManager landingPadManager;
	public static Locator locator;
	public static Detector detector;	
	public static float RefuelAmount = 0.2f;
	public static float OxygenRefillAmount = 0.18f;
	
	

	public class Functions
	{
		static public void ShipRefuel()
		{
			landingPadManager.IsLanded(); // Call's the landing check to update boolean (Failsafe)
			float fractionaryFuel = shipResources.GetFractionalFuel(); // Get ship's fractional fuel
			IsLanded = landingPadManager.IsLanded();	// Checks if ship is landed

			//	Refueling and gauges manager
			if (IsLanded)
			{
				shipResources.SetFuel(shipResources._currentFuel += RefuelAmount);	// Increase fuel when landed
				shipFuelGauge._indicatorLight.SetEmissionColor(Color.green);	//	Turn gauge green light on when refueling
			}
			else if (0.0001f <= fractionaryFuel && fractionaryFuel <= 0.3f && !IsLanded)
			{
				shipFuelGauge._indicatorLight.SetEmissionColor(Color.red); // Turn on warning light when low fuel
			}
			else if (shipResources != null && !IsLanded && fractionaryFuel > 0.3f && !IsLanded)
			{ 
				shipFuelGauge._indicatorLight.SetEmissionColor(Color.black); // Turn light off when not refueling
			}

			// Electrical systems & Refill station manager
			if (shipResources._currentFuel <= 0)
			{
				playerRecoveryPoint._refuelsPlayer = false;	// Disables refueling when ship is out of fuel
				mainElectricalSystem.SetPowered(false);	// Disables electrical systems when ship is out of fuel
			}
			else if (shipResources._currentFuel > 0 && shipFuelGauge._shipDestroyed == false)
			{
				playerRecoveryPoint._refuelsPlayer = true;	// Activates refueling station in case of refueling
				mainElectricalSystem.SetPowered(true);	// Enables electrical system in case of refueling
			}
		}
		static public void OxygenRefill()
		{
			bool isOxygenAvailable = shipOxygenDetector.GetDetectOxygen();	// Check oxygen availabilty
			bool playerHasOxygenVolume = playerOxygenDetector._activeVolumes.Contains(shipOxygenVolume);	// Check if player has ship's OxygenVolume
			float fractionaryOxygen = shipResources.GetFractionalOxygen();	// Get fractional oxygen
			bool isPlayerInShip = PlayerState.IsInsideShip();	// Check if player is inside ship

			// Oxygen refill & Gauge lights manager
			if (isOxygenAvailable)
			{
				shipResources.SetOxygen(shipResources._currentOxygen += OxygenRefillAmount);	// Increase oxygen if it's available
				shipOxygenGauge._indicatorLight.SetEmissionColor(Color.green);	// Turn gauge green light on when refilling
			}
			else if (0.001f <= fractionaryOxygen && fractionaryOxygen <= 0.16f && !isOxygenAvailable)
			{
				shipOxygenGauge._indicatorLight.SetEmissionColor(Color.red);	// Turn red light on critical oxygen
			}
			else if (0.161 <= fractionaryOxygen && fractionaryOxygen <= 0.33f && !isOxygenAvailable)
			{
				shipOxygenGauge._indicatorLight.SetEmissionColor(Color.yellow);	//Turn yellow light on low fuel
			}
			else if (!isOxygenAvailable && fractionaryOxygen > 0.33f)
			{ 
				shipOxygenGauge._indicatorLight.SetEmissionColor(Color.black); // Turn light off when oxygen is not available
			}

			// OxygenVolumes manager
			if (shipResources._currentOxygen == 0 && playerHasOxygenVolume && isPlayerInShip || !isPlayerInShip)
			{
				playerOxygenDetector._activeVolumes.Remove(shipOxygenVolume); // Remove ship's OxygenVolume from player
			}

			else if (shipResources._currentOxygen > 0 && !playerHasOxygenVolume && isPlayerInShip)
			{
				playerOxygenDetector._activeVolumes.Add(shipOxygenVolume); // Add ship's OxygenVolume to player in case of refill
			}
		}
	}

	//	Create Harmony patches class
	[HarmonyPatch]
	public class HarmonyPatches
	{

		//	Create a patch to modify the amount of fuel used
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ShipResources), nameof(ShipResources.DrainFuel))]
		public static void ShipFuelUsageMultiplier(ref float amount)
		{
			// Calculate the increased fuel usage
			amount = amount * 7;
		}

		//	Create a patch to increase oxygen consumption on the ship
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ShipResources), nameof(ShipResources.DrainOxygen))]
		public static void ShipOxygenDrainMultiplier(ref float amount)
		{
			//	Calculate increased oxygen drain
			bool isOxygenAvailable = shipOxygenDetector.GetDetectOxygen();
			if (isOxygenAvailable)
			{
				amount *= 0f;
			}
			else
			{
				amount *= 110f;
			}
		}
	}
}